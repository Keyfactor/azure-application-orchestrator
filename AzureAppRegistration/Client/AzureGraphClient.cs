using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Azure.Identity;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AzureAppRegistration.Client
{
    public abstract class AzureGraphClient<T>
    {
        protected AzureGraphClient(AzureSettings properties)
        {
            Log = LogHandler.GetClassLogger<AzureApplicationClient>();
            Log.LogDebug("Initializing Azure Application Client");
            
            string[] scopes = { "https://graph.microsoft.com/.default" };
            
            TokenCredentialOptions options = new TokenCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud
            };
            
            ClientSecretCredential clientSecretCredential = new ClientSecretCredential(
                properties.TenantId, properties.ApplicationId, properties.ClientSecret, options);
            
            ClientCertificateCredential clientCertificateCredential = new ClientCertificateCredential(
                properties.TenantId, properties.ApplicationId, new X509Certificate2("C:\\Users\\hroszell\\Downloads\\terraform_test_cert.pfx", "TEVN6peCxyz4"));

            GraphClient = new GraphServiceClient(clientCertificateCredential, scopes);

            Log.LogDebug("Successfully created Graph client with ClientSecretCredential and scope \"{Scopes}\"", scopes[0]);
            
            _properties = properties;
            
            TypeString = typeof(T).Name;
        }
        
        protected GraphServiceClient GraphClient { get; }

        public string ApplicationId
        {
            set {
                ObjectId = GetObjectId(value);
                _applicationId = value;
            }
            get => _applicationId;
        }

        protected string ObjectId
        {
            get
            {
                if (string.IsNullOrEmpty(_objectId))
                    throw new Exception("ApplicationId must be set before retrieving ObjectId");
                return _objectId;
            }
            private set => _objectId = value;
        }
        
        public string TypeString { get; }

        private string _applicationId;
        private string _objectId;

        protected ILogger Log { get; }
        private readonly AzureSettings _properties;

        protected Application GetApplication()
        {
            Log.LogDebug("Retrieving application for application ID \"{ApplicationId}\"", ApplicationId);
            
            Application app;
            
            try
            {
                app = GraphClient.Applications[ObjectId].GetAsync(
                    requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new[] { "id","appId","keyCredentials","passwordCredentials" };
                }
                    ).Result;
            }
            catch (AggregateException ex)
            {
                Log.LogError("Error retrieving application for application ID \"{ApplicationId}\": {Message}", ApplicationId, ex.Message);
                throw;
            }
            
            return app;
        }

        protected ServicePrincipal GetServicePrincipal()
        {
            Log.LogDebug("Retrieving service principal for application ID \"{ApplicationId}\"", ApplicationId);

            ServicePrincipal sp;

            try
            {
                sp = GraphClient.ServicePrincipals[ObjectId].GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new[] { "id","appId","keyCredentials","passwordCredentials" };
                }).Result;
            }
            catch (AggregateException ex)
            {
                Log.LogError("Error retrieving service principal for application ID \"{ApplicationId}\": {Message}", ApplicationId, ex.Message);
                throw;
            }

            return sp;
        }

        private string GetObjectId(string applicationId)
        {
            // If T is Application, get the object ID from the application ID
            if (typeof(T) == typeof(Application))
            {
                ApplicationCollectionResponse apps;
                try
                {
                    apps = GraphClient.Applications.GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Filter = $"(appId eq '{applicationId}')";
                        requestConfiguration.QueryParameters.Top = 1;
                    }).Result;
                } catch (AggregateException e)
                {
                    Log.LogError("Unable to query MS Graph for Application \"{AppId}\": {Error}", applicationId, e.Message);
                    throw;
                }
                
                if (apps?.Value == null || apps.Value.Count == 0 || string.IsNullOrEmpty(apps.Value.FirstOrDefault()?.Id))
                {
                    throw new Exception($"Application with Application ID \"{applicationId}\" not found in tenant \"{_properties.TenantId}\"");
                }

                return apps.Value.FirstOrDefault()?.Id;
            }
            
            // If T is ServicePrincipal, get the object ID from the service principal ID
            if (typeof(T) == typeof(ServicePrincipal))
            {
                ServicePrincipalCollectionResponse sps;
                try
                {
                    sps = GraphClient.ServicePrincipals.GetAsync(requestConfiguration =>
                    {
                        requestConfiguration.QueryParameters.Filter = $"(appId eq '{applicationId}')";
                        requestConfiguration.QueryParameters.Top = 1;
                    }).Result;
                } catch (AggregateException e)
                {
                    Log.LogError("Unable to query MS Graph for ServicePrincipal \"{AppId}\": {Error}", applicationId, e.Message);
                    throw;
                }
                
                if (sps?.Value == null || sps.Value.Count == 0 || string.IsNullOrEmpty(sps.Value.FirstOrDefault()?.Id))
                {
                    throw new Exception($"Service Principal with Application ID \"{applicationId}\" not found in tenant \"{_properties.TenantId}\"");
                }

                return sps.Value.FirstOrDefault()?.Id;
            }
            
            throw new Exception($"Invalid type \"{typeof(T)}\" specified for AzureGraphClient");
        }

        /**
         * Returns a dictionary of [AppID, ObjectID] for all service principals in the tenant
         */
        public Dictionary<string, string> GetServicePrincipals()
        {
            Dictionary<string, string> appIds = new Dictionary<string, string> ();
            
            Log.LogDebug("Retrieving service principals in tenant with ID \"{TenantId}\"", _properties.TenantId);
            ServicePrincipalCollectionResponse sps;
            try
            {
                sps = GraphClient.ServicePrincipals.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 999;
                }).Result;
            }
            catch (AggregateException e)
            {
                Log.LogError("Unable to retrieve service principals for tenant ID \"{TenantId}\": {Error}", _properties.TenantId, e.Message);
                throw;
            }
            
            if (sps?.Value == null || sps.Value.Count == 0)
            {
                Log.LogWarning("No service principals found for tenant ID \"{TenantId}\"", _properties.TenantId);
                return appIds;
            }
            
            foreach (ServicePrincipal sp in sps.Value)
            {
                Log.LogDebug("    Found service principal \"{ApplicationName}\" ({ApplicationId})", sp.DisplayName, sp.Id);
                
                if (sp.AppId == null)
                {
                    Log.LogWarning("      Service principal \"{ApplicationName}\" ({ApplicationId}) does not have an AppID", sp.DisplayName, sp.Id);
                    continue;
                }
                
                appIds.Add(sp.AppId, sp.Id);
            }

            return appIds;
        }
        
        /**
         * Returns a dictionary of [AppID, ObjectID] for all application registrations in the tenant.
         */
        public Dictionary<string, string> GetApplications()
        {
            Dictionary<string, string> appIds = new Dictionary<string, string> ();
            
            Log.LogDebug("Retrieving application registrations for tenant ID \"{TenantId}\"", _properties.TenantId);
            ApplicationCollectionResponse apps;
            try
            {
                apps = GraphClient.Applications.GetAsync((requestConfiguration) =>
                {
                    requestConfiguration.QueryParameters.Top = 999;
                }).Result;
            }
            catch (AggregateException e)
            {
                Log.LogError("Unable to retrieve application registrations for tenant ID \"{TenantId}\": {Error}", _properties.TenantId, e.Message);
                throw;
            }
            
            if (apps?.Value == null || apps.Value.Count == 0)
            {
                Log.LogWarning("No application registrations found for tenant ID \"{TenantId}\"", _properties.TenantId);
                return appIds;
            }
            
            foreach (Application app in apps.Value)
            {
                Log.LogDebug("    Found application \"{ApplicationName}\" ({ApplicationId})", app.DisplayName, app.Id);
                
                if (app.AppId == null)
                {
                    Log.LogWarning("      Application \"{ApplicationName}\" ({ApplicationId}) does not have an AppID", app.DisplayName, app.Id);
                    continue;
                }
                
                appIds.Add(app.AppId, app.Id);
            }

            return appIds;
        }

        public IEnumerable<CurrentInventoryItem> GetInventory()
        {
            Log.LogDebug("Retrieving {T} certificates for application ID \"{ApplicationId}\"", TypeString, ApplicationId);

            List<KeyCredential> keyCredentials;

            if (typeof(T) == typeof(Application))
            {
                keyCredentials = GetApplication().KeyCredentials;
            }
            else if (typeof(T) == typeof(ServicePrincipal))
            {
                keyCredentials = GetServicePrincipal().KeyCredentials;
            }
            else
            {
                throw new Exception($"Invalid type \"{typeof(T)}\" specified for AzureGraphClient");
            }
            
            if (keyCredentials == null || keyCredentials.Count == 0)
            {
                Log.LogWarning("No key credentials found for application ID \"{ApplicationId}\"", ApplicationId);
                return new List<CurrentInventoryItem>();
            }
            
            // Create a map of strings containing CustomKeyIdentifier to ensure that only one certificate is returned for each key
            Dictionary<string, bool> keyIdMap = new Dictionary<string, bool>();
            List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();

            foreach (KeyCredential keyCredential in keyCredentials)
            {
                X509Certificate2 certificate = GetCertificateFromKeyCredential(keyCredential);
                if (certificate == null)
                {
                    Log.LogWarning("Unable to retrieve certificate for key credential with DisplayName \"{DisplayName}\" ({CustomKeyId})", keyCredential.DisplayName, keyCredential.CustomKeyIdentifier);
                    continue;
                }
                string thumbprint = certificate.Thumbprint;
                
                if (thumbprint == null)
                {
                    Log.LogWarning("Unable to retrieve thumbprint for certificate with CustomKeyIdentifier \"{CustomKeyIdentifier}\"", keyCredential.CustomKeyIdentifier);
                    continue;
                }
                
                // If the thumbprint is already in the map, skip it
                if (keyIdMap.ContainsKey(thumbprint))
                {
                    continue;
                }
                keyIdMap[thumbprint] = true;

                // Assemble the certificates for the inventory
                List<string> certificates = new List<string> { Convert.ToBase64String(certificate.Export(X509ContentType.Cert)) };

                CurrentInventoryItem inventoryItem = new CurrentInventoryItem()
                {
                    Alias = keyCredential.DisplayName,
                    PrivateKeyEntry = false,
                    ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                    UseChainLevel = true,
                    Certificates = certificates
                };
                
                Log.LogDebug("    Found certificate called \"{CertificateName}\" ({ResourceId})", keyCredential.DisplayName, keyCredential.KeyId);
                inventoryItems.Add(inventoryItem);
            }

            return inventoryItems;
        }
        
        protected List<KeyCredential> DeepCopyKeyList(List<KeyCredential> keyCredentials)
        {
            List<KeyCredential> deepKeyList;
            if (keyCredentials == null)
            {
                deepKeyList = new List<KeyCredential>();
            }
            else
            {
                deepKeyList = keyCredentials.Select(keyCredential => new KeyCredential
                    {
                        CustomKeyIdentifier = keyCredential.CustomKeyIdentifier,
                        DisplayName = keyCredential.DisplayName,
                        Key = keyCredential.Key,
                        Type = keyCredential.Type,
                        Usage = keyCredential.Usage,
                    })
                    .ToList();
            }

            return deepKeyList;
        }

        protected IEnumerable<PasswordCredential> DeepCopyPasswordList(List<PasswordCredential> passwordList)
        {
            // Deep copy the password list
            List<PasswordCredential> deepPasswordList;
            if (passwordList == null)
            {
                deepPasswordList = new List<PasswordCredential>();
            }
            else
            {
                deepPasswordList = passwordList.Select(passwordCredential => new PasswordCredential
                    {
                        CustomKeyIdentifier = passwordCredential.CustomKeyIdentifier,
                        DisplayName = passwordCredential.DisplayName,
                        EndDateTime = passwordCredential.EndDateTime,
                        KeyId = passwordCredential.KeyId,
                        SecretText = passwordCredential.SecretText,
                        StartDateTime = passwordCredential.StartDateTime,
                    })
                    .ToList();
            }

            return deepPasswordList;
        }

        protected X509Certificate2 GetCertificateFromKeyCredential(KeyCredential keyCredential)
        {
            // TODO this might not work
            if (keyCredential.Key == null || keyCredential.Key.Length == 0)
            {
                Log.LogWarning("Key credential with CustomKeyIdentifier \"{CustomKeyIdentifier}\" has no key data", keyCredential.CustomKeyIdentifier);
                return null;
            }
            // Get the certificate from the key credential
            X509Certificate2 certificate;
            try
            {
                certificate = new X509Certificate2(keyCredential.Key);
            }
            catch (Exception e)
            {
                Log.LogWarning("Unable to serialize certificate for key credential with CustomKeyIdentifier \"{CustomKeyIdentifier}\": {Error}", keyCredential.CustomKeyIdentifier, e.Message);
                return null;
            }

            return certificate;
        }

        protected static X509Certificate2 SerializeCertificate(string certificateData, string password)
        {
            byte[] rawData = Convert.FromBase64String(certificateData);
            return new X509Certificate2(rawData, password, X509KeyStorageFlags.Exportable);
        }

        protected string CalculateJwt(X509Certificate2 certificate)
        {
            string aud = $"00000002-0000-0000-c000-000000000000";
            
            X509Certificate2 signingCert = new X509Certificate2("C:\\Users\\hroszell\\Downloads\\terraform_test_cert.pfx", "TEVN6peCxyz4");
            
            // aud and iss are the only required claims.
            Dictionary<string, object> claims = new Dictionary<string, object>()
            {
                { "aud", aud },
                { "iss", _properties.ApplicationId }
            };
            
            // token validity should not be more than 10 minutes
            DateTime now = DateTime.UtcNow;
            SecurityTokenDescriptor securityTokenDescriptor = new SecurityTokenDescriptor
            {
                Claims = claims,
                NotBefore = now,
                Expires = now.AddMinutes(10),
                SigningCredentials = new X509SigningCredentials(signingCert)
            };

            JsonWebTokenHandler handler = new JsonWebTokenHandler();
            string token = handler.CreateToken(securityTokenDescriptor);
            return token;
        }
    }
}
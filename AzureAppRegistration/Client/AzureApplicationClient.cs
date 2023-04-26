using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Azure.Identity;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace AzureAppRegistration.Client
{
    public class AzureApplicationClient
    {
        public AzureApplicationClient()
        {
            string[] scopes = { "https://graph.microsoft.com/.default" };
            
            GraphClient = new GraphServiceClient(new DefaultAzureCredential(), scopes);
        }
        public AzureApplicationClient(AzureProperties properties)
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

            GraphClient = new GraphServiceClient(clientSecretCredential);
            
            Log.LogDebug("Successfully created Graph client with ClientSecretCredential and scope \"{Scopes}\"", scopes[0]);
        }

        public string ApplicationId { get; set; }

        private GraphServiceClient GraphClient { get; }
        
        private ILogger Log { get; }

        public IEnumerable<CurrentInventoryItem> GetApplicationCertificates()
        {
            List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();
            Log.LogDebug("Retrieving certificates for application ID \"{ApplicationId}\"", ApplicationId);

            //ServicePrincipal servicePrincipal = GetServicePrincipal();
            Application servicePrincipal = GetApplication();
            
            if (servicePrincipal.KeyCredentials == null)
            {
                Log.LogWarning("No key credentials found for application ID \"{ApplicationId}\"", ApplicationId);
                return inventoryItems;
            }
            
            // Create a map of strings containing CustomKeyIdentifier to ensure that only one certificate is returned for each key
            Dictionary<string, bool> keyIdMap = new Dictionary<string, bool>();

            foreach (KeyCredential keyCredential in servicePrincipal.KeyCredentials)
            {
                X509Certificate2 certificate = GetCertificateFromKeyCredential(keyCredential);
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

        public void AddApplicationCertificate(string certificateName, string certificateData, string certificatePassword, bool isSamlPreferred=false)
        {
            // certificateData is a base64 encoded PFX certificate
            X509Certificate2 certificate = SerializeCertificate(certificateData, certificatePassword);
            // Calculate the SHA256 hash of the certificate's thumbprint
            byte[] keyHash = GetSha256FromThumbprint(certificate.Thumbprint);
            Log.LogDebug("Adding certificate called \"{CertificateName}\" to application ID \"{ApplicationId}\" (key hash {Hash})", certificateName, ApplicationId, Encoding.UTF8.GetString(keyHash));
            
            // Get the service principal object
            ServicePrincipal servicePrincipal = GetServicePrincipal();

            // Don't override existing certificates/key passwords
            List<KeyCredential> keys = servicePrincipal.KeyCredentials ?? new System.Collections.Generic.List<KeyCredential>();
            List<PasswordCredential> passwords = servicePrincipal.PasswordCredentials ?? new System.Collections.Generic.List<PasswordCredential>();

            // First, create an asymmetric certificate key credential containing the certificate's public key
            // The usage should be "Verify"
            Guid pubKeyGuid = Guid.NewGuid();
            Log.LogDebug("    Creating AsymmetricX509Cert key credential for certificate \"{CertificateName}\" ({Guid})", certificateName, pubKeyGuid);
            keys.Add(new KeyCredential
            {
                DisplayName = certificateName,
                Type = "AsymmetricX509Cert",
                Usage = "Verify",
                CustomKeyIdentifier = keyHash,
                StartDateTime = DateTime.Parse(certificate.GetEffectiveDateString()).ToUniversalTime(),
                EndDateTime = DateTimeOffset.Parse(certificate.GetExpirationDateString()),
                KeyId = pubKeyGuid,
                Key = Encoding.UTF8.GetBytes(Convert.ToBase64String(certificate.Export(X509ContentType.Cert))),
            });

            // Create a GUID to represent the key ID and to link the key to the certificate
            Guid privKeyGuid = Guid.NewGuid();
            
            // Next, create a symmetric certificate key credential containing the public and private key
            // The usage should be "Sign"
            Log.LogDebug("    Creating X509CertAndPassword (symmetric) key credential for certificate \"{CertificateName}\" ({Guid})", certificateName, privKeyGuid);
            keys.Add(new KeyCredential
            {
                DisplayName = certificateName,
                Type = "X509CertAndPassword",
                Usage = "Sign",
                CustomKeyIdentifier = keyHash,
                StartDateTime = DateTime.Parse(certificate.GetEffectiveDateString()).ToUniversalTime(),
                EndDateTime = DateTimeOffset.Parse(certificate.GetExpirationDateString()),
                KeyId = privKeyGuid,
                Key = Encoding.UTF8.GetBytes(Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, certificatePassword))),
            });
            
            // Finally, create a password credential containing the certificate's password
            Log.LogDebug("    Creating Password credential for certificate \"{CertificateName}\" ({Guid})", certificateName, privKeyGuid);
            passwords.Add(new PasswordCredential
            {
                CustomKeyIdentifier = keyHash,
                KeyId = privKeyGuid,
                StartDateTime = DateTime.Parse(certificate.GetEffectiveDateString()).ToUniversalTime(),
                EndDateTime = DateTimeOffset.Parse(certificate.GetExpirationDateString()),
                SecretText = certificatePassword,
            });

            // Create a service principal object with the updated key and password credentials
            ServicePrincipal requestBody = new ServicePrincipal
            {
                KeyCredentials = keys,
                PasswordCredentials = passwords,
            };
            
            // Update the service principal object
            Log.LogDebug("    Updating service principal object for application ID \"{ApplicationId}\"", ApplicationId);
            ServicePrincipal result = GraphClient.ServicePrincipals[ApplicationId].PatchAsync(requestBody).Result;

            Console.WriteLine(result);
        }
        
        public void ReplaceApplicationCertificate(string certificateName, string certificateData, string certificatePassword)
        {
            // Get the service principal object
            ServicePrincipal servicePrincipal = GetServicePrincipal();
            
            Log.LogDebug("Replacing certificate called \"{CertificateName}\" for application ID \"{ApplicationId}\"", certificateName, ApplicationId);
            
            // First, determine if the current certificate is the preferred SAML certificate
            string replacementPreferredSamlCertificateThumbprint = "";
            if (servicePrincipal.KeyCredentials != null)
            {
                KeyCredential currentCertificate = servicePrincipal.KeyCredentials.FirstOrDefault(c => c.DisplayName == certificateName);
                // If the current certificate is the preferred SAML certificate, set the replacement certificate as the preferred SAML certificate
                if (GetCertificateFromKeyCredential(currentCertificate).Thumbprint ==
                    GetPreferredSamlSignerThumbprint())
                {
                    X509Certificate2 replacementCertificate = SerializeCertificate(certificateData, certificatePassword);
                    replacementPreferredSamlCertificateThumbprint = replacementCertificate.Thumbprint;
                }
            }

            // Then, add the new certificate
            AddApplicationCertificate(certificateName, certificateData, certificatePassword);
            
            // Finally, remove the old certificate
            RemoveApplicationCertificate(certificateName, replacementPreferredSamlCertificateThumbprint);
        }
        
        public void RemoveApplicationCertificate(string thumbprint, string replacementPreferredSamlCertificateThumbprint="")
        {
            // Get the service principal object
            ServicePrincipal servicePrincipal = GetServicePrincipal();
            
            // Don't delete existing certificates/key passwords unless they match the certificateName
            List<KeyCredential> keys = servicePrincipal.KeyCredentials ?? new System.Collections.Generic.List<KeyCredential>();
            List<PasswordCredential> passwords = servicePrincipal.PasswordCredentials ?? new System.Collections.Generic.List<PasswordCredential>();

            // If the current preferred SAML certificate is being removed, set the preferred SAML certificate to the replacement
            string currentPreferredSamlCertificateThumbprint = GetPreferredSamlSignerThumbprint();
            if (currentPreferredSamlCertificateThumbprint == thumbprint)
            {
                Log.LogDebug("    Preferred SAML certificate ({Thumbprint}) is being removed", thumbprint);
                if (replacementPreferredSamlCertificateThumbprint == "")
                {
                    // If no replacement is specified, set the preferred SAML certificate to the first certificate in the list
                    X509Certificate2 replacementCert = GetCertificateFromKeyCredential(keys[0]);
                    replacementPreferredSamlCertificateThumbprint = replacementCert.Thumbprint;
                    
                    Log.LogDebug("    No replacement preferred SAML certificate specified, setting preferred SAML certificate to \"{CertificateName}\" ({Thumbprint})", replacementCert.Subject, replacementCert.Thumbprint);
                }
                SetPreferredSamlSignerThumbprint(replacementPreferredSamlCertificateThumbprint);
            }
            
            // Store GUIDs of keys to delete
            Dictionary<string, bool> guidMap = new Dictionary<string, bool>();
            
            // Find certificates that match the thumbprint, and store their GUIDs
            foreach (KeyCredential keyCredential in keys)
            {
                // Get the certificate from the key credential
                X509Certificate2 certificate = GetCertificateFromKeyCredential(keyCredential);
                if (certificate == null)
                {
                    continue;
                }
                
                // If the thumbprint doesn't match, skip it
                if (certificate.Thumbprint != thumbprint)
                {
                    continue;
                }
                
                Log.LogDebug("    Removing key credential \"{DisplayName}\" ({KeyId})", keyCredential.DisplayName, keyCredential.KeyId);

                keys.Remove(keyCredential);
                
                // Store the GUID of the key to delete
                guidMap[keyCredential.KeyId.ToString()] = true;
            }
            
            // Find password credentials that match the GUIDs of the keys to delete
            foreach (PasswordCredential passwordCredential in passwords)
            {
                // If the GUID doesn't match, skip it
                if (!guidMap.ContainsKey(passwordCredential.KeyId.ToString()))
                {
                    continue;
                }
                
                Log.LogDebug("    Removing password credential for certificate \"{CertificateName}\" ({Guid})", passwordCredential.CustomKeyIdentifier, passwordCredential.KeyId);
                
                passwords.Remove(passwordCredential);
            }
            
            // Create a service principal object with the updated key and password credentials
            ServicePrincipal requestBody = new ServicePrincipal
            {
                KeyCredentials = keys,
                PasswordCredentials = passwords,
            };
            
            // Update the service principal object
            Log.LogDebug("    Updating service principal object for application ID \"{ApplicationId}\"", ApplicationId);
            GraphClient.ServicePrincipals[ApplicationId].PatchAsync(requestBody);
        }

        public bool ApplicationCertificateExists(string name)
        {
            ServicePrincipal servicePrincipal = GetServicePrincipal();
            
            return servicePrincipal.KeyCredentials != null && servicePrincipal.KeyCredentials.Any(c => c.DisplayName == name);
        }
        
        private string GetPreferredSamlSignerThumbprint()
        {
            ServicePrincipal sp = GetServicePrincipal();

            return sp.PreferredTokenSigningKeyThumbprint;
        }
        
        private void SetPreferredSamlSignerThumbprint(string preferredSamlSignerThumbprint)
        {
            ServicePrincipal sp = GetServicePrincipal();
            
            // Create a service principal object with the updated key and password credentials
            ServicePrincipal requestBody = new ServicePrincipal
            {
                PreferredTokenSigningKeyThumbprint = preferredSamlSignerThumbprint,
            };
            
            // Update the service principal object
            Log.LogDebug("    Updating service principal object for application ID \"{ApplicationId}\"", ApplicationId);
            ServicePrincipal result = GraphClient.ServicePrincipals[ApplicationId].PatchAsync(requestBody).Result;
        }

        private X509Certificate2 GetCertificateFromKeyCredential(KeyCredential keyCredential)
        {
            // TODO this might not work
            // Get the certificate from the key credential
            X509Certificate2 certificate = null;
            try
            {
                certificate = new X509Certificate2(keyCredential.Key);
            }
            catch (Exception e)
            {
                Log.LogWarning("Unable to serialize certificate for key credential with CustomKeyIdentifier \"{CustomKeyIdentifier}\"", keyCredential.CustomKeyIdentifier);
                return null;
            }
            
            return certificate;
        }

        private Application GetApplication()
        {
            if (ApplicationId == null)
            {
                throw new Exception("Application ID not set");
            }
            
            Log.LogDebug("Retrieving application for application ID \"{ApplicationId}\"", ApplicationId);
            
            Application app;
            
            try
            {
                app = GraphClient.Applications[ApplicationId].GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new string[] { "id", "appId", "displayName" };
                }).Result;
            }
            catch (AggregateException ex)
            {
                Log.LogError("Error retrieving application for application ID \"{ApplicationId}\": {Message}", ApplicationId, ex.Message);
                throw;
            }
            
            return app;
        }

        private ServicePrincipal GetServicePrincipal()
        {
            if (ApplicationId == null)
            {
                throw new Exception("Application ID not set");
            }
                 
            Log.LogDebug("Retrieving service principal for application ID \"{ApplicationId}\"", ApplicationId);

            ServicePrincipal sp;

            try
            {
                sp = GraphClient.ServicePrincipals[ApplicationId].GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Select = new string[] { "id", "appId", "displayName", "keyCredentials", "passwordCredentials" };
                }).Result;
            }
            catch (AggregateException ex)
            {
                Log.LogError("Error retrieving service principal for application ID \"{ApplicationId}\": {Message}", ApplicationId, ex.Message);
                throw;
            }

            return sp;
        }
        
        private static X509Certificate2 SerializeCertificate(string certificateData, string password)
        {
            byte[] rawData = Convert.FromBase64String(certificateData);
            return new X509Certificate2(rawData, password, X509KeyStorageFlags.Exportable);
        }
        
        // Generate hash from thumbprint.
        public static byte[] GetSha256FromThumbprint(string thumbprint)
        {
            byte[] message = Encoding.ASCII.GetBytes(thumbprint);
            SHA256Managed hashString = new SHA256Managed();
            return Encoding.UTF8.GetBytes(Convert.ToBase64String(hashString.ComputeHash(message)));
        }
    }
}
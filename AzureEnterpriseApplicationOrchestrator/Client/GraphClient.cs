// Copyright 2024 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Azure.Core;
using Azure.Identity;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace AzureEnterpriseApplicationOrchestrator.Client;

public class GraphClient : IAzureGraphClient {

    private ILogger _logger { get; set; }
    private TokenCredential _credential { get; set; }
    private string _tenantId { get; set; }
    private GraphServiceClient _graphClient { get; set; }
    private string _targetApplicationId { get; set; }

    // In Azure, the application and service principal are separate objects bound by
    // a single Application ID.
    private string _applicationObjectId { get; set; }
    private string _servicePrincipalObjectId { get; set; }

    // The Client can only be constructed by the Builder method
    // unless they use the constructor that passes a pre-configured
    // GraphServiceClient object. This is to ensure that the Client is always
    // constructed with a valid ArmClient object.
    private GraphClient()
    {
        _logger = LogHandler.GetClassLogger<GraphClient>();
    }

    private GraphClient(GraphServiceClient graphClient)
    {
        _graphClient = graphClient;
        _logger = LogHandler.GetClassLogger<GraphClient>();
    }

    public class Builder : IAzureGraphClientBuilder
    {
        private GraphClient _client = new();

        private string _tenantId { get; set; }
        private string _applicationId { get; set; }
        private string _clientSecret { get; set; }
        private X509Certificate2 _clientCertificate { get; set; }
        private string _targetApplicationId { get; set; }
        private Uri _azureCloudEndpoint { get; set; }

        public IAzureGraphClientBuilder WithTenantId(string tenantId)
        {
            _tenantId = tenantId;
            return this;
        }

        public IAzureGraphClientBuilder WithTargetApplicationId(string applicationId)
        {
            _targetApplicationId = applicationId;
            return this;
        }

        public IAzureGraphClientBuilder WithApplicationId(string applicationId)
        {
            _applicationId = applicationId;
            return this;
        }

        public IAzureGraphClientBuilder WithClientSecret(string clientSecret)
        {
            _clientSecret = clientSecret;
            return this;
        }

        public IAzureGraphClientBuilder WithClientCertificate(X509Certificate2 clientCertificate)
        {
            _clientCertificate = clientCertificate;
            return this;
        }

        public IAzureGraphClientBuilder WithAzureCloud(string azureCloud)
        {
            if (string.IsNullOrWhiteSpace(azureCloud)) 
            {
                azureCloud = "public";
            }

            switch (azureCloud.ToLower())
            {
                case "china":
                    _azureCloudEndpoint = AzureAuthorityHosts.AzureChina;
                    break;
                case "germany":
                    _azureCloudEndpoint = AzureAuthorityHosts.AzureGermany;
                    break;
                case "government":
                    _azureCloudEndpoint = AzureAuthorityHosts.AzureGovernment;
                    break;
                default:
                    _azureCloudEndpoint = AzureAuthorityHosts.AzurePublicCloud;
                    break;
            }

            return this;
        }

        public IAzureGraphClient Build()
        {
            ILogger logger = LogHandler.GetClassLogger<GraphClient>();
            logger.LogDebug($"Creating Graph Client for tenant ID '{_tenantId}' to target application ID '{_applicationId}'.");

            // Setting up credentials for Azure Resource Management.
            DefaultAzureCredentialOptions credentialOptions = new DefaultAzureCredentialOptions
            {
                AuthorityHost = _azureCloudEndpoint,
                              AdditionallyAllowedTenants = { "*" } 
            };

            TokenCredential credential;
            if (!string.IsNullOrWhiteSpace(_clientSecret)) 
            {
                credential = new ClientSecretCredential(
                        _tenantId, _applicationId, _clientSecret, credentialOptions
                        );
            }
            else if (_clientCertificate != null) 
            {
                credential = new ClientCertificateCredential(
                        _tenantId, _applicationId, _clientCertificate, credentialOptions
                        );
            }
            else 
            {
                throw new Exception("Client secret or client certificate must be provided.");
            }


            string[] scopes = { "https://graph.microsoft.com/.default" };

            // Creating Graph Client with the specified credentials.
            GraphServiceClient graphClient = new GraphServiceClient(credential, scopes);
            _client._graphClient = graphClient;
            _client._credential = credential;
            _client._tenantId = _tenantId;
            _client._targetApplicationId = _targetApplicationId;

            logger.LogTrace("Azure Resource Management client created.");
            return _client;
        }
    }

    private string GetApplicationObjectId()
    {
        if (_applicationObjectId != null)
        {
            _logger.LogTrace($"Application object ID already set. Returning cached value. [{_applicationObjectId}]");
            return _applicationObjectId;
        }

        ApplicationCollectionResponse apps;
        try
        {
            apps = _graphClient.Applications.GetAsync(requestConfiguration =>
                    {
                    requestConfiguration.QueryParameters.Filter = $"(appId eq '{_targetApplicationId}')";
                    requestConfiguration.QueryParameters.Top = 1;
                    }).Result;
        } catch (AggregateException e)
        {
            _logger.LogError($"Unable to query MS Graph for Application \"{_targetApplicationId}\": {e}");
            throw;
        }

        if (apps?.Value == null || apps.Value.Count == 0 || string.IsNullOrEmpty(apps.Value.FirstOrDefault()?.Id))
        {
            throw new Exception($"Application with Application ID \"{_targetApplicationId}\" not found in tenant \"{_tenantId}\"");
        }

        _applicationObjectId = apps.Value.FirstOrDefault()?.Id;
        return _applicationObjectId;
    }

    private string GetServicePrincipalObjectId()
    {
        if (_servicePrincipalObjectId != null)
        {
            _logger.LogTrace($"Service principal object ID already set. Returning cached value. [{_servicePrincipalObjectId}]");
            return _servicePrincipalObjectId;
        }

        ServicePrincipalCollectionResponse sps;
        try
        {
            sps = _graphClient.ServicePrincipals.GetAsync(requestConfiguration =>
                    {
                    requestConfiguration.QueryParameters.Filter = $"(appId eq '{_targetApplicationId}')";
                    requestConfiguration.QueryParameters.Top = 1;
                    }).Result;
        } catch (AggregateException e)
        {
            _logger.LogError($"Unable to query MS Graph for ServicePrincipal \"{_targetApplicationId}\": {e}");
            throw;
        }

        if (sps?.Value == null || sps.Value.Count == 0 || string.IsNullOrEmpty(sps.Value.FirstOrDefault()?.Id))
        {
            throw new Exception($"Service Principal with Application ID \"{_targetApplicationId}\" not found in tenant \"{_tenantId}\"");
        }

        _servicePrincipalObjectId = sps.Value.FirstOrDefault()?.Id;
        return _servicePrincipalObjectId;
    }

    public void AddApplicationCertificate(string certificateName, string certificateData)
    {
        // certificateData is a base64 encoded PFX certificate
        X509Certificate2 certificate = SerializeCertificate(certificateData, "");
        if (certificate.Thumbprint == null)
            throw new Exception("Could not calculate thumbprint for certificate");

        // Calculate the SHA256 hash of the certificate's thumbprint
        byte[] customKeyId = Encoding.UTF8.GetBytes(certificate.Thumbprint)[..32];

        _logger.LogDebug($"Adding certificate called \"{certificateName}\" to application ID \"{_targetApplicationId}\" (custom key ID {Encoding.UTF8.GetString(customKeyId)})");

        // Get the application object
        Application application = GetApplication();

        char[] certPem = PemEncoding.Write("CERTIFICATE", certificate.RawData);

        // Update the application object
        _logger.LogDebug($"Updating application object for application ID \"{_targetApplicationId}\"");
        try
        {
            _graphClient.Applications[GetApplicationObjectId()].PatchAsync(new Application
                    {
                    KeyCredentials = new List<KeyCredential>(DeepCopyKeyList(application.KeyCredentials))
                    {
                    new KeyCredential {
                    DisplayName = certificateName,
                    Type = "AsymmetricX509Cert",
                    Usage = "Verify",
                    CustomKeyIdentifier = customKeyId,
                    StartDateTime = DateTimeOffset.Parse(certificate.GetEffectiveDateString()),
                    EndDateTime = DateTimeOffset.Parse(certificate.GetExpirationDateString()),
                    KeyId = Guid.NewGuid(),
                    Key = System.Text.Encoding.UTF8.GetBytes(certPem)
                    }
                    }
                    }).Wait();
        }
        catch (AggregateException e)
        {
            _logger.LogError("Failed to update application with new certificates: {Message}", e.Message);
            throw;
        }

        _logger.LogDebug("Certificate added successfully");
    }

    public void RemoveApplicationCertificate(string certificateName)
    {
        Application application = GetApplication();

        // Don't delete existing certificates/key passwords unless they match the certificateName
        List<KeyCredential> keys = DeepCopyKeyList(application.KeyCredentials);

        List<KeyCredential> keysToKeep = new List<KeyCredential>();

        // Find certificates that match the thumbprint, and store their GUIDs
        foreach (KeyCredential keyCredential in keys)
        {
            if (keyCredential.DisplayName == certificateName)
            {
                _logger.LogDebug($"Removing key credential \"{keyCredential.DisplayName}\"");
                continue;
            }

            keysToKeep.Add(keyCredential);
        }

        _logger.LogDebug($"Updating application object for application ID \"{_targetApplicationId}\"");
        try
        {
            _graphClient.Applications[GetApplicationObjectId()].PatchAsync(new Application
                    {
                    KeyCredentials = keysToKeep
                    }).Wait();
        }
        catch (AggregateException e)
        {
            _logger.LogError($"Failed to update application with new certificates: {e}");
            throw;
        }
    }

    public OperationResult<IEnumerable<CurrentInventoryItem>> GetApplicationCertificates()
    {
        Application application = GetApplication();
        return InventoryFromKeyCredentials(application.KeyCredentials);
    }

    public bool ApplicationCertificateExists(string certificateName)
    {
        Application application = GetApplication();

        return application.KeyCredentials != null && application.KeyCredentials.Any(c => c.DisplayName == certificateName);
    }

    public void AddServicePrincipalCertificate(string certificateName, string certificateData, string certificatePassword)
    {
        // certificateData is a base64 encoded PFX certificate
        X509Certificate2 certificate = SerializeCertificate(certificateData, certificatePassword);
        if (certificate.Thumbprint == null)
            throw new Exception("Could not calculate thumbprint for certificate");

        // Calculate the SHA256 hash of the certificate's thumbprint
        byte[] customKeyId = Encoding.UTF8.GetBytes(certificate.Thumbprint)[..32];
        _logger.LogDebug($"Adding certificate called \"{certificateName}\" to application ID \"{_targetApplicationId}\" (custom key ID {Encoding.UTF8.GetString(customKeyId)})");

        // Create a GUID to represent the key ID and to link the key to the certificate
        Guid privKeyGuid = Guid.NewGuid();

        // Update the service principal object
        _logger.LogDebug($"Updating service principal object for application ID \"{_targetApplicationId}\"");
        try
        {
            _graphClient.ServicePrincipals[GetServicePrincipalObjectId()].PatchAsync(new ServicePrincipal
                    {
                    KeyCredentials = new List<KeyCredential>()
                    {
                    new KeyCredential {
                    DisplayName = certificateName,
                    Type = "AsymmetricX509Cert",
                    Usage = "Verify",
                    CustomKeyIdentifier = customKeyId,
                    StartDateTime = DateTimeOffset.Parse(certificate.GetEffectiveDateString()),
                    EndDateTime = DateTimeOffset.Parse(certificate.GetExpirationDateString()),
                    KeyId = Guid.NewGuid(),
                    Key = certificate.Export(X509ContentType.Cert)
                    },
                    new KeyCredential {
                    DisplayName = certificateName,
                    Type = "X509CertAndPassword",
                    Usage = "Sign",
                    CustomKeyIdentifier = customKeyId,
                    StartDateTime = DateTimeOffset.Parse(certificate.GetEffectiveDateString()),
                    EndDateTime = DateTimeOffset.Parse(certificate.GetExpirationDateString()),
                    KeyId = privKeyGuid,
                    Key = certificate.Export(X509ContentType.Pfx, certificatePassword)
                    }
                    },
                        PasswordCredentials = new List<PasswordCredential>()
                        {
                            new PasswordCredential
                            {
                                CustomKeyIdentifier = customKeyId,
                                KeyId = privKeyGuid,
                                StartDateTime = DateTimeOffset.Parse(certificate.GetEffectiveDateString()),
                                EndDateTime = DateTimeOffset.Parse(certificate.GetExpirationDateString()),
                                SecretText = certificatePassword,
                            }
                        }
                    }).Wait();
        } catch (AggregateException e)
        {
            _logger.LogWarning($"Failed to update service principal object: {e}");
            // TODO remove certificates to avoid leaving the service principal in a bad state
            throw;
        }

        // Update the preferred SAML certificate
        try
        {
            _graphClient.ServicePrincipals[GetServicePrincipalObjectId()].PatchAsync(new ServicePrincipal
                    {
                    PreferredTokenSigningKeyThumbprint = certificate.Thumbprint
                    }).Wait();
        }
        catch (AggregateException e)
        {
            _logger.LogWarning($"Failed to set preferred SAML certificate: {e}");
            // TODO remove certificates to avoid leaving the service principal in a bad state
            throw;
        }
    }

    public void RemoveServicePrincipalCertificate(string certificateName)
    {
        // Get the service principal object
        ServicePrincipal servicePrincipal = GetServicePrincipal();

        List<KeyCredential> keys = DeepCopyKeyList(servicePrincipal.KeyCredentials);
        IEnumerable<PasswordCredential> passwords = DeepCopyPasswordList(servicePrincipal.PasswordCredentials);

        // Store GUIDs of keys to delete
        Dictionary<string, bool> guidMap = new Dictionary<string, bool>();

        // Create a new list for keys we want to keep
        List<KeyCredential> keysToKeep = new List<KeyCredential>();
        List<PasswordCredential> passwordsToKeep = new List<PasswordCredential>();

        // Find certificates that match the thumbprint, and store their GUIDs
        foreach (KeyCredential keyCredential in keys)
        {
            if (keyCredential.DisplayName == certificateName)
            {
                _logger.LogDebug($"Removing key credential \"{keyCredential.DisplayName}\"");

                // Store the GUID of the key to delete
                if (keyCredential.Usage == "Sign" && keyCredential.CustomKeyIdentifier != null)
                    guidMap[Encoding.UTF8.GetString(keyCredential.CustomKeyIdentifier)] = true;

                continue;
            }

            keysToKeep.Add(keyCredential);
        }

        foreach (PasswordCredential passwordCredential in passwords.ToList())
        {
            if (passwordCredential.CustomKeyIdentifier != null && guidMap.ContainsKey(Encoding.UTF8.GetString(passwordCredential.CustomKeyIdentifier)))
            {
                _logger.LogDebug($"Removing password credential for certificate \"{passwordCredential.CustomKeyIdentifier}\" ({passwordCredential.KeyId})");
                continue;
            }

            passwordsToKeep.Add(passwordCredential);
        }

        // Update the service principal object
        _logger.LogDebug($"Updating service principal object for application ID \"{_targetApplicationId}\"");
        try
        {
            _graphClient.ServicePrincipals[GetServicePrincipalObjectId()].PatchAsync(new ServicePrincipal
                    {
                    KeyCredentials = keysToKeep,
                    PasswordCredentials = passwordsToKeep
                    });
        } catch (AggregateException e)
        {
            _logger.LogWarning($"Failed to update service principal object with updated certificate list: {e}");
            throw;
        }
    }

    public OperationResult<IEnumerable<CurrentInventoryItem>> GetServicePrincipalCertificates()
    {
        ServicePrincipal sp = GetServicePrincipal();
        return InventoryFromKeyCredentials(sp.KeyCredentials);
    }

    public bool ServicePrincipalCertificateExists(string certificateName)
    {
        ServicePrincipal servicePrincipal = GetServicePrincipal();

        return servicePrincipal.KeyCredentials != null && servicePrincipal.KeyCredentials.Any(c => c.DisplayName == certificateName);
    }

    OperationResult<IEnumerable<string>> IAzureGraphClient.DiscoverApplicationIds()
    {
        List<string> appIds = new();
        OperationResult<IEnumerable<string>> result = new(appIds);

        _logger.LogDebug($"Retrieving application registrations for tenant ID \"{_tenantId}\"");
        ApplicationCollectionResponse apps;
        try
        {
            apps = _graphClient.Applications.GetAsync((requestConfiguration) =>
                    {
                    requestConfiguration.QueryParameters.Top = 999;
                    }).Result;
        }
        catch (AggregateException e)
        {
            _logger.LogError($"Unable to retrieve application registrations for tenant ID \"{_tenantId}\": {e}");
            throw;
        }

        if (apps?.Value == null || apps.Value.Count == 0)
        {
            _logger.LogWarning($"No application registrations found for tenant ID \"{_tenantId}\"");
            return result;
        }

        foreach (Application app in apps.Value)
        {
            _logger.LogDebug($"Found application \"{app.DisplayName}\" ({app.Id})");

            if (app.AppId == null)
            {
                _logger.LogWarning($"Application \"{app.DisplayName}\" ({app.Id}) does not have an AppID");
                result.AddRuntimeErrorMessage($"Application \"{app.DisplayName}\" ({app.Id}) does not have an AppID");
                continue;
            }

            appIds.Add(app.AppId);
        }

        return result;
    }

    public IEnumerable<string> DiscoverApplicationIds()
    {
        List<string> appIds = new();

        _logger.LogDebug($"Retrieving application registrations for tenant ID \"{_tenantId}\"");
        ApplicationCollectionResponse apps;
        try
        {
            apps = _graphClient.Applications.GetAsync((requestConfiguration) =>
                    {
                    requestConfiguration.QueryParameters.Top = 999;
                    }).Result;
        }
        catch (AggregateException e)
        {
            _logger.LogError($"Unable to retrieve application registrations for tenant ID \"{_tenantId}\": {e}");
            throw;
        }

        if (apps?.Value == null || apps.Value.Count == 0)
        {
            _logger.LogWarning($"No application registrations found for tenant ID \"{_tenantId}\"");
            return appIds;
        }

        foreach (Application app in apps.Value)
        {
            _logger.LogDebug($"Found application \"{app.DisplayName}\" ({app.Id})");

            if (app.AppId == null)
            {
                _logger.LogWarning($"Application \"{app.DisplayName}\" ({app.Id}) does not have an AppID");
                continue;
            }

            appIds.Add(app.AppId);
        }

        return appIds;
    }

    private OperationResult<IEnumerable<CurrentInventoryItem>> InventoryFromKeyCredentials(List<KeyCredential> keyCredentials)
    {
        Dictionary<string, CurrentInventoryItem> inventoryItems = new();
        OperationResult<IEnumerable<CurrentInventoryItem>> result = new(inventoryItems.Values);

        if (keyCredentials == null || keyCredentials.Count == 0)
        {
            _logger.LogWarning($"No key credentials found for application ID \"{_targetApplicationId}\"");
            return result;
        }

        // Create a map of strings containing CustomKeyIdentifier to ensure that only one certificate is returned for each key
        // The boolean value is not used, but is required for the Dictionary type
        Dictionary<string, bool> keyIdMap = new Dictionary<string, bool>();

        // Create a map to track certificates that we failed to retrieve. The Add method will always
        // add two certificates to the service principal despite them having the same ID. We need to
        // track the ones that we failed to serialize, and remove them from the map when we do find the certificate.
        // Finally, we'll log a warning for any certificates that we failed to retrieve.
        Dictionary<string, string> failedCertificateMap = new Dictionary<string, string>();
        
        // Create a map to track certificates that we're confident have a private key entry in Azure.
        // Azure will never return the Private Key with the Graph API, but Keyfactor Command uses 
        // the presence of a private key to determine how Certificate Renewal should be handled.
        // We won't use the boolean value, but it's required for the Dictionary type.
        Dictionary<string, bool> privateKeyMap = new Dictionary<string, bool>();

        foreach (KeyCredential keyCredential in keyCredentials)
        {
            string customKeyIdentifier = Encoding.UTF8.GetString(keyCredential.CustomKeyIdentifier);
            
            if (!string.IsNullOrWhiteSpace(keyCredential.Usage) && keyCredential.Usage.Equals("Sign", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug($"Certificate with CustomKeyIdentifier \"{customKeyIdentifier}\" has a private key entry");
                privateKeyMap[customKeyIdentifier] = true;
            }
            // We only track the case where the private key exists because there will be several keyCredentials
            // for Service Principals where one is the certificate and the other is the private key.

            X509Certificate2 certificate = GetCertificateFromKeyCredential(keyCredential);
            if (certificate == null)
            {
                string message = $"Unable to retrieve certificate for key credential with DisplayName \"{keyCredential.DisplayName}\" ({customKeyIdentifier})";
                failedCertificateMap[customKeyIdentifier] = message;
                continue;
            }

            // Remove the failed certificate from the map if it's there
            if (failedCertificateMap.ContainsKey(customKeyIdentifier))
            {
                failedCertificateMap.Remove(customKeyIdentifier);
            }

            // If the thumbprint is already in the map, skip it
            if (keyIdMap.ContainsKey(customKeyIdentifier))
            {
                _logger.LogTrace($"Skipping certificate with CustomKeyIdentifier \"{customKeyIdentifier}\" because it's already in the inventory");
                continue;
            }
            keyIdMap[customKeyIdentifier] = true;

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

            _logger.LogDebug($"Found certificate called \"{keyCredential.DisplayName}\" ({customKeyIdentifier})");
            inventoryItems[customKeyIdentifier] = inventoryItem;
        }

        foreach (string key in keyIdMap.Keys)
        {
            // Remove won't throw an exception if the key isn't in the map
            failedCertificateMap.Remove(key);
        }

        foreach (string key in failedCertificateMap.Keys)
        {
            _logger.LogWarning(failedCertificateMap[key]);
            result.AddRuntimeErrorMessage(failedCertificateMap[key]);
        }
        
        foreach (string key in privateKeyMap.Keys)
        {
            if (inventoryItems.ContainsKey(key))
            {
                inventoryItems[key].PrivateKeyEntry = true;
            }
        }

        return result;
    }

    protected Application GetApplication()
    {
        _logger.LogDebug($"Retrieving application for application ID \"{_targetApplicationId}\"");

        Application app;

        try
        {
            app = _graphClient.Applications[GetApplicationObjectId()].GetAsync(
                    requestConfiguration =>
                    {
                    requestConfiguration.QueryParameters.Select = new[] { "id","appId","keyCredentials","passwordCredentials" };
                    }
                    ).Result;
        }
        catch (AggregateException ex)
        {
            _logger.LogError($"Error retrieving application for application ID \"{_targetApplicationId}\": {ex}");
            throw;
        }

        return app;
    }

    protected ServicePrincipal GetServicePrincipal()
    {
        _logger.LogDebug($"Retrieving service principal for application ID \"{_targetApplicationId}\"");

        ServicePrincipal sp;

        try
        {
            sp = _graphClient.ServicePrincipals[GetServicePrincipalObjectId()].GetAsync(requestConfiguration =>
                    {
                    requestConfiguration.QueryParameters.Select = new[] { "id","appId","keyCredentials","passwordCredentials" };
                    }).Result;
        }
        catch (AggregateException ex)
        {
            _logger.LogError($"Error retrieving service principal for application ID \"{_targetApplicationId}\": {ex}");
            throw;
        }

        return sp;
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
        string customKeyIdentifier = Encoding.UTF8.GetString(keyCredential.CustomKeyIdentifier);
        if (keyCredential.Key == null || keyCredential.Key.Length == 0)
        {
            _logger.LogWarning($"Key credential with KeyId \"{keyCredential.KeyId}\" has no key data");
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
            _logger.LogWarning($"Unable to serialize certificate for key credential with CustomKeyIdentifier \"{customKeyIdentifier}\": {e}");
            return null;
        }

        return certificate;
    }

    protected static X509Certificate2 SerializeCertificate(string certificateData, string password)
    {
        byte[] rawData = Convert.FromBase64String(certificateData);
        return new X509Certificate2(rawData, password, X509KeyStorageFlags.Exportable);
    }

}

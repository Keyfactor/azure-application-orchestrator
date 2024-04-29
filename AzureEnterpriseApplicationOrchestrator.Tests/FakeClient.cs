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

using AzureEnterpriseApplicationOrchestrator.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace AzureEnterpriseApplicationOrchestrator.Tests;

public class FakeClient : IAzureGraphClient
{

    public class FakeBuilder : IAzureGraphClientBuilder
    {
        private FakeClient _client = new FakeClient();

        public string? _tenantId { get; set; }
        public string? _targetApplicationId { get; set; }
        public string? _applicationId { get; set; }
        public string? _clientSecret { get; set; }
        public string? _azureCloudEndpoint { get; set; }

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

        public IAzureGraphClientBuilder WithAzureCloud(string azureCloud)
        {
            _azureCloudEndpoint = azureCloud;
            return this;
        }

        public IAzureGraphClient Build()
        {
            return _client;
        }
    }


    ILogger _logger = LogHandler.GetClassLogger<FakeClient>();

    public IEnumerable<string>? ApplicationIdsAvailableOnFakeTenant { get; set; }
    public Dictionary<string, string>? CertificatesAvailableOnFakeTarget { get; set; }

    public void AddApplicationCertificate(string certificateName, string certificateData)
    {
        _logger.LogDebug($"Adding certificate {certificateName} to fake application");

        if (CertificatesAvailableOnFakeTarget == null)
        {
            CertificatesAvailableOnFakeTarget = new Dictionary<string, string>();
        }

        _logger.LogDebug($"Adding certificate {certificateName} to fake application");

        CertificatesAvailableOnFakeTarget.Add(certificateName, certificateData);

        _logger.LogTrace($"Fake client has {CertificatesAvailableOnFakeTarget.Count} certificates in inventory");
    }

    public void AddServicePrincipalCertificate(string certificateName, string certificateData, string certificatePassword)
    {
        AddApplicationCertificate(certificateName, certificateData);
    }

    public OperationResult<IEnumerable<string>> DiscoverApplicationIds()
    {
        if (ApplicationIdsAvailableOnFakeTenant == null)
        {
            throw new Exception("Discover Application IDs method failure - no application ids set");
        }

        return new OperationResult<IEnumerable<string>>(ApplicationIdsAvailableOnFakeTenant);
    }

    public OperationResult<IEnumerable<CurrentInventoryItem>> GetApplicationCertificates()
    {
        _logger.LogDebug("Getting Application Certificates from fake Application");

        if (CertificatesAvailableOnFakeTarget == null)
        {
            throw new Exception("Get Application Certificate method failure - no inventory items set");
        }

        List<CurrentInventoryItem> inventoryItems = new List<CurrentInventoryItem>();
        OperationResult<IEnumerable<CurrentInventoryItem>> result = new(inventoryItems);

        foreach (KeyValuePair<string, string> cert in CertificatesAvailableOnFakeTarget)
        {
            inventoryItems.Add(new CurrentInventoryItem
            {
                Alias = cert.Key,
                PrivateKeyEntry = false,
                ItemStatus = OrchestratorInventoryItemStatus.Unknown,
                UseChainLevel = true,
                Certificates = new List<string> { cert.Value }
            });
        }

        _logger.LogDebug($"Fake client has {inventoryItems.Count} certificates in inventory");

        return result;
    }

    public OperationResult<IEnumerable<CurrentInventoryItem>> GetServicePrincipalCertificates()
    {
        return GetApplicationCertificates();
    }

    public void RemoveApplicationCertificate(string certificateName)
    {
        if (CertificatesAvailableOnFakeTarget == null || !CertificatesAvailableOnFakeTarget.ContainsKey(certificateName))
        {
            throw new Exception("Certificate not found");
        }

        _logger.LogDebug($"Removing certificate {certificateName} from fake Application");

        CertificatesAvailableOnFakeTarget.Remove(certificateName);

        _logger.LogTrace($"Fake client has {CertificatesAvailableOnFakeTarget.Count} certificates in inventory");
        return;
    }

    public void RemoveServicePrincipalCertificate(string certificateName)
    {
        RemoveApplicationCertificate(certificateName);
    }

    public bool ApplicationCertificateExists(string certificateName)
    {
        if (CertificatesAvailableOnFakeTarget != null)
        {
            return CertificatesAvailableOnFakeTarget.ContainsKey(certificateName);
        }

        return false;
    }

    public bool ServicePrincipalCertificateExists(string certificateName)
    {
        return ApplicationCertificateExists(certificateName);
    }
}

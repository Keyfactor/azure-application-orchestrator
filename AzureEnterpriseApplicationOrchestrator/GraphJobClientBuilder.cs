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
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureEnterpriseApplicationOrchestrator;

public class GraphJobClientBuilder<TBuilder> where TBuilder : IAzureGraphClientBuilder, new()
{
    public TBuilder _builder = new TBuilder();
    private ILogger _logger = LogHandler.GetClassLogger<GraphJobClientBuilder<TBuilder>>();

    public class CertificateStoreProperties
    {
        public string ServerUsername { get; set; }
        public string ServerPassword { get; set; }
        public string AzureCloud { get; set; }
    }

    public GraphJobClientBuilder<TBuilder> WithCertificateStoreDetails(CertificateStore details)
    {
        _logger.LogDebug($"Builder - Setting values from Certificate Store Details: {JsonConvert.SerializeObject(details)}");

        CertificateStoreProperties properties = JsonConvert.DeserializeObject<CertificateStoreProperties>(details.Properties);

        _logger.LogTrace($"Builder - ClientMachine  => TenantId:            {details.ClientMachine}");
        _logger.LogTrace($"Builder - StorePath      => TargetApplicationId: {details.StorePath}");
        _logger.LogTrace($"Builder - ServerUsername => ApplicationId:       {properties.ServerUsername}");
        _logger.LogTrace($"Builder - ServerPassword => ClientSecret:        {properties.ServerPassword}");
        _logger.LogTrace($"Builder - AzureCloud     => AzureCloud:          {properties.AzureCloud}");

        _builder
            .WithTenantId(details.ClientMachine)
            .WithApplicationId(properties.ServerUsername)
            .WithClientSecret(properties.ServerPassword)
            .WithTargetApplicationId(details.StorePath)
            .WithAzureCloud(properties.AzureCloud);

        return this;
    }

    public GraphJobClientBuilder<TBuilder> WithDiscoveryJobConfiguration(DiscoveryJobConfiguration config, string tenantId)
    {
        _logger.LogTrace($"Builder - tenantId       => TenantId: {tenantId}");
        _logger.LogTrace($"Builder - ServerUsername => ApplicationId: {config.ServerUsername}");
        _logger.LogTrace($"Builder - ServerPassword => ClientSecret: {config.ServerPassword}");

        _builder
            .WithTenantId(tenantId)
            .WithApplicationId(config.ServerUsername)
            .WithClientSecret(config.ServerPassword);

        return this;
    }

    public IAzureGraphClient Build()
    {
        return _builder.Build();
    }
}

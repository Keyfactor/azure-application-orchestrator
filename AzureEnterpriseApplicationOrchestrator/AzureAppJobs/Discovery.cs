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
using AzureEnterpriseApplicationOrchestrator.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace AzureEnterpriseApplicationOrchestrator.AzureAppJobs;

public class Discovery : IDiscoveryJobExtension
{
    public IAzureGraphClient Client { get; set; }
    public string ExtensionName => "AzureApp";

    private bool _clientInitializedByInjection = false;

    ILogger _logger = LogHandler.GetClassLogger<Discovery>();

    public JobResult ProcessJob(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate callback)
    {
        if (Client != null) _clientInitializedByInjection = true;

        _logger.LogDebug("Beginning Azure Application (App Registration/Application) Discovery Job");

        JobResult result = new JobResult
        {
            Result = OrchestratorJobStatusJobResult.Failure,
                   JobHistoryId = config.JobHistoryId
        };

        List<string> discoveredApplicationIds = new();

        foreach (var tenantId in TenantIdsToSearchFromJobConfig(config))
        {
            _logger.LogTrace($"Processing tenantId: {tenantId}");

            // If the client was not injected, create a new one with the tenant ID determied by
            // the TenantIdsToSearchFromJobConfig method
            if (!_clientInitializedByInjection)
            {
                Client = new GraphJobClientBuilder<GraphClient.Builder>()
                    .WithDiscoveryJobConfiguration(config, tenantId)
                    .Build();
            }

            try
            {
                var operationResult = Client.DiscoverApplicationIds();
                if (!operationResult.Success)
                {
                    result.FailureMessage += operationResult.ErrorMessage;
                    _logger.LogWarning(result.FailureMessage);
                    continue;
                }
                discoveredApplicationIds.AddRange(operationResult.Result);
            }catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing discovery job:\n {ex.Message}");
                result.FailureMessage = ex.Message;
                return result;
            }
        }

        try
        {
            callback(discoveredApplicationIds);
            result.Result = OrchestratorJobStatusJobResult.Success;
        } catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing discovery job:\n {ex.Message}");
            result.FailureMessage = ex.Message;
        }

        return result;
    }

    private IEnumerable<string> TenantIdsToSearchFromJobConfig(DiscoveryJobConfiguration config)
    {
        string directoriesToSearchAsString = config.JobProperties?["dirs"] as string;
        _logger.LogTrace($"Directories to search: {directoriesToSearchAsString}");

        if (string.IsNullOrEmpty(directoriesToSearchAsString) || string.Equals(directoriesToSearchAsString, "*"))
        {
            _logger.LogTrace($"No directories to search provided, using default tenant ID: {config.ClientMachine}");
            return new List<string> { config.ClientMachine };
        }

        List<string> tenantIdsToSearch = new();
        tenantIdsToSearch.AddRange(directoriesToSearchAsString.Split(','));
        tenantIdsToSearch.ForEach(tenantId => tenantId = tenantId.Trim());

        _logger.LogTrace($"Tenant IDs to search: {string.Join(',', tenantIdsToSearch)}");
        return tenantIdsToSearch;
    }
}

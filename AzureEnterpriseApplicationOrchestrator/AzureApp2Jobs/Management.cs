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
using AzureEnterpriseApplicationOrchestrator.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace AzureEnterpriseApplicationOrchestrator.AzureApp2Jobs;

public class Management : IManagementJobExtension
{
    public IAzureGraphClient Client { get; set; }
    public string ExtensionName => "AzureApp";

    ILogger _logger = LogHandler.GetClassLogger<Management>();

    public JobResult ProcessJob(ManagementJobConfiguration config)
    {
        _logger.LogDebug("Beginning Application 2 (App Registration/Application) Management Job");

        if (Client == null)
        {
            Client = new GraphJobClientBuilder<GraphClient.Builder>()
                .WithV2CertificateStoreDetails(config.CertificateStoreDetails)
                .Build();
        }

        JobResult result = new JobResult
        {
            Result = OrchestratorJobStatusJobResult.Failure,
            JobHistoryId = config.JobHistoryId
        };

        try
        {
            var operation = DetermineOperation(config);
            result.Result = operation switch
            {
                OperationType.Replace => ReplaceCertificate(config),
                OperationType.Add => AddCertificate(config),
                OperationType.Remove => RemoveCertificate(config),
                OperationType.DoNothing => OrchestratorJobStatusJobResult.Success,
                _ => throw new Exception($"Invalid Management operation type [{config.OperationType}]")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error processing job: {ex.Message}");
            result.FailureMessage = ex.Message;
        }

        return result;
    }

    private enum OperationType
    {
        Add,
        Remove,
        Replace,
        DoNothing,
        None
    }

    private OperationType DetermineOperation(ManagementJobConfiguration config)
    {
        if (config.OperationType == CertStoreOperationType.Add && config.Overwrite)
            return OperationType.Replace;

        if (config.OperationType == CertStoreOperationType.Add)
            return OperationType.Add;

        if (config.OperationType == CertStoreOperationType.Remove)
            return OperationType.Remove;

        return OperationType.None;
    }

    private OrchestratorJobStatusJobResult AddCertificate(ManagementJobConfiguration config)
    {
        _logger.LogDebug("Beginning AddCertificate operation");

        // The AzureApp Certificate Store Type doesn't support private key handling
        if (string.IsNullOrWhiteSpace(config.JobCertificate.PrivateKeyPassword) == false)
        {
            throw new Exception("Private key handling is not supported for AzureApp Certificate Store Type.");
        }

        if (string.IsNullOrWhiteSpace(config.JobCertificate.Alias))
        {
            throw new Exception("Certificate alias is required.");
        }

        _logger.LogTrace($"Adding certificate with alias [{config.JobCertificate.Alias}]");

        // Don't check if the certificate already exists; Command shouldn't allow non-unique
        // aliases to be added and if the certificate already exists, the operation should fail.

        Client.AddApplicationCertificate(
                config.JobCertificate.Alias,
                config.JobCertificate.Contents
                );

        _logger.LogDebug("AddCertificate operation complete");

        return OrchestratorJobStatusJobResult.Success;
    }

    private OrchestratorJobStatusJobResult ReplaceCertificate(ManagementJobConfiguration config)
    {
        _logger.LogDebug("Beginning ReplaceCertificate operation");

        RemoveCertificate(config);
        AddCertificate(config);

        _logger.LogDebug("ReplaceCertificate operation complete");

        return OrchestratorJobStatusJobResult.Success;
    }

    private OrchestratorJobStatusJobResult RemoveCertificate(ManagementJobConfiguration config)
    {
        _logger.LogDebug("Beginning RemoveCertificate operation");

        _logger.LogTrace($"Removing certificate with alias [{config.JobCertificate.Alias}]");

        // If the certificate doesn't exist, the operation should fail.

        Client.RemoveApplicationCertificate(config.JobCertificate.Alias);

        _logger.LogDebug("RemoveCertificate operation complete");

        return OrchestratorJobStatusJobResult.Success;
    }
}


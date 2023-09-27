// Copyright 2023 Keyfactor
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
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;
using Newtonsoft.Json;

namespace AzureEnterpriseApplicationOrchestrator.Client
{
    public class ServerConfig
    {
        public string ServerUsername { get; set; }
        public string ServerPassword { get; set; }
        public bool ServerUseSsl { get; set; }
        public string Type { get; set; }
    }
    public class AzureGraphJob<T> : IOrchestratorJobExtension
    {
        public string ExtensionName
        {
            get
            {
                if (typeof(T) == typeof(ServicePrincipal))
                {
                    return "AzureSP";
                }

                if (typeof(T) == typeof(Application))
                {
                    return "AzureApp";
                }
                
                throw new Exception("Invalid type: " + typeof(T) + ". Must be ServicePrincipal or Application.");
            }
        }
        
        private IAzureGraphClient Client { get; set; }
        ILogger _logger = LogHandler.GetClassLogger<AzureGraphJob<T>>();

        private void Initialize(CertificateStore details)
        {
            _logger.LogDebug("Certificate Store Configuration: {Details}", JsonConvert.SerializeObject(details));

            ServerConfig config = JsonConvert.DeserializeObject<ServerConfig>(details.Properties);
            
            AzureSettings azureProperties = new AzureSettings
            {
                TenantId = details.ClientMachine,
                ApplicationId = config.ServerUsername,
                ClientSecret = config.ServerPassword
            };

            if (typeof(T) == typeof(ServicePrincipal))
            {
                _logger.LogDebug("Initializing AzureServicePrincipalClient");
                Client = new AzureServicePrincipalClient(azureProperties)
                {
                    ApplicationId = details.StorePath
                };
            } else if (typeof(T) == typeof(Application))
            {
                _logger.LogDebug("Initializing AzureApplicationClient");
                Client = new AzureApplicationClient(azureProperties)
                {
                    ApplicationId = details.StorePath
                };
            }
            else
            {
                throw new Exception("Invalid type: " + typeof(T) + ". Must be ServicePrincipal or Application.");
            }
        }
        
        private void Initialize(DiscoveryJobConfiguration config)
        {
            ILogger logger = LogHandler.GetReflectedClassLogger(this);
            logger.LogDebug($"Discovery Job Configuration: {JsonConvert.SerializeObject(config)}");
            logger.LogDebug("Initializing AzureAppGatewayClient");
            AzureSettings azureProperties = new AzureSettings
            {
                TenantId = config.ClientMachine,
                ApplicationId = config.ServerUsername,
                ClientSecret = config.ServerPassword
            };
            
            if (typeof(T) == typeof(ServicePrincipal))
            {
                _logger.LogDebug("Initializing AzureServicePrincipalClient");
                Client = new AzureServicePrincipalClient(azureProperties);
            } else if (typeof(T) == typeof(Application))
            {
                _logger.LogDebug("Initializing AzureApplicationClient");
                Client = new AzureApplicationClient(azureProperties);
            }
            else
            {
                throw new Exception("Invalid type: " + typeof(T) + ". Must be ServicePrincipal or Application.");
            }
        }

        protected JobResult ProcessInventoryJob(InventoryJobConfiguration config, SubmitInventoryUpdate cb)
        {
            Initialize(config.CertificateStoreDetails);
            
            _logger.LogDebug("Beginning {Type} Inventory Job", Client.TypeString);
            
            JobResult result = new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                JobHistoryId = config.JobHistoryId
            };
            
            List<CurrentInventoryItem> inventoryItems;
            
            try
            {
                inventoryItems = Client.GetInventory().ToList();
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting {Type} Certificates: {Error}", Client.TypeString, ex.Message);
                result.FailureMessage = "Error getting " + Client.TypeString + " Certificates:\n" + ex.Message;
                return result;
            }
            
            _logger.LogDebug("Found {InventoryItemsCount} certificates in {Type}", inventoryItems.Count, Client.TypeString);
            
            cb.DynamicInvoke(inventoryItems);
            
            result.Result = OrchestratorJobStatusJobResult.Success;
            return result;
        }

        protected JobResult ProcessManagementJob(ManagementJobConfiguration config)
        {
            Initialize(config.CertificateStoreDetails);
            
            _logger.LogDebug("Beginning {Type} Management Job", Client.TypeString);
            
            JobResult result = new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                JobHistoryId = config.JobHistoryId
            };

            try
            {
                switch (config.OperationType)
                {
                    case CertStoreOperationType.Add:
                        _logger.LogDebug("Adding certificate to {Type}", Client.TypeString);
                        
                        // Ensure that the password is provided.
                        if (typeof(T) == typeof(ServicePrincipal) && string.IsNullOrWhiteSpace(config.JobCertificate.PrivateKeyPassword))
                        {
                            throw new Exception("Private key password must be provided.");
                        }
                        // Ensure that an alias is provided.
                        if (string.IsNullOrWhiteSpace(config.JobCertificate.Alias))
                        {
                            throw new Exception("Certificate alias is required.");
                        }
            
                        if (Client.CertificateExists(config.JobCertificate.Alias) && !config.Overwrite)
                        {
                            _logger.LogDebug("Certificate with alias \"{Alias}\" already exists in {Type}, and job was not configured to overwrite", config.JobCertificate.Alias, Client.TypeString);
                            throw new Exception($"Certificate with alias \"{config.JobCertificate.Alias}\" already exists in {Client.TypeString}, and job was not configured to overwrite");
                        }

                        if (config.Overwrite)
                        {
                            _logger.LogDebug("Overwrite is enabled, replacing certificate in {Type} called \"{Alias}\"", Client.TypeString, config.JobCertificate.Alias);
                            Client.ReplaceCertificate(config.JobCertificate.Alias, config.JobCertificate.Contents, config.JobCertificate.PrivateKeyPassword);
                        }
                        else if (typeof(T) == typeof(ServicePrincipal) && !config.Overwrite)
                        {
                            throw new Exception(
                                "Cannot perform Management Add for ServicePrincipal without overwrite enabled.");
                        }
                        else
                        {
                            _logger.LogDebug("Adding certificate to {Type}", Client.TypeString);
                            Client.AddCertificate(config.JobCertificate.Alias, config.JobCertificate.Contents, config.JobCertificate.PrivateKeyPassword);
                        }
                        
                        _logger.LogDebug("Add operation complete");
                        
                        result.Result = OrchestratorJobStatusJobResult.Success;
                        break;
                    case CertStoreOperationType.Remove:
                        _logger.LogDebug("Removing certificate from {Type}", Client.TypeString);

                        Client.RemoveCertificate(config.JobCertificate.Alias);
                        
                        _logger.LogDebug("Remove operation complete");
                        result.Result = OrchestratorJobStatusJobResult.Success;
                        break;
                    default:
                        _logger.LogDebug("Invalid management operation type: {OT}", config.OperationType);
                        throw new ArgumentOutOfRangeException();
                }
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job:\n {Message}", ex.Message);
                result.FailureMessage = ex.Message;
            }

            return result;
        }

        protected JobResult ProcessDiscoveryJob(DiscoveryJobConfiguration config, SubmitDiscoveryUpdate callback) {
            JobResult result = new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                JobHistoryId = config.JobHistoryId
            };
            
            Initialize(config);
            
            _logger.LogDebug("Beginning {Type} Discovery Job", Client.TypeString);

            try
            {
                callback(Client.DiscoverApplicationIds());
                result.Result = OrchestratorJobStatusJobResult.Success;
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job:\n {0}", ex.Message);
                result.FailureMessage = ex.Message;
            }

            return result;
        }
    }
}
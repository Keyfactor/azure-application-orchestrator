using System;
using System.Collections.Generic;
using System.Linq;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace AzureAppRegistration.Jobs
{
    public class Inventory : AzureApplicationJob<Inventory>, IInventoryJobExtension
    {
        ILogger _logger = LogHandler.GetClassLogger<Inventory>();

        public JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate cb)
        {
            _logger.LogDebug("Beginning App Gateway Inventory Job");
            
            JobResult result = new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                JobHistoryId = config.JobHistoryId
            };

            Initialize(config.CertificateStoreDetails);
            
            List<CurrentInventoryItem> inventoryItems;
            
            try
            {
                inventoryItems = ApplicationClient.GetApplicationCertificates().ToList();
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting App Gateway SSL Certificates: {Error}", ex.Message);
                result.FailureMessage = "Error getting App Gateway SSL Certificates:\n" + ex.Message;
                return result;
            }
            
            _logger.LogDebug("Found {InventoryItemsCount} certificates in App Gateway", inventoryItems.Count);
            
            cb.DynamicInvoke(inventoryItems);
            
            result.Result = OrchestratorJobStatusJobResult.Success;
            return result;
        }
    }
}
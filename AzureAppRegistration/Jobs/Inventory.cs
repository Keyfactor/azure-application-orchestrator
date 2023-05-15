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
    }
}
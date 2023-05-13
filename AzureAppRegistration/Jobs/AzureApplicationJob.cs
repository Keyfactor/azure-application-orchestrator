﻿using AzureAppRegistration.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureAppRegistration.Jobs
{
    public class AzureApplicationJob<T> : IOrchestratorJobExtension
    {
        public string ExtensionName => "AzureApp";
        
        protected IAzureGraphClient Client { get; private set; }
        
        protected void Initialize(CertificateStore details)
        {
            ILogger logger = LogHandler.GetReflectedClassLogger(this);
            logger.LogDebug("Certificate Store Configuration: {Details}", JsonConvert.SerializeObject(details));
            logger.LogDebug("Initializing AzureAppGatewayClient");
            dynamic properties = JsonConvert.DeserializeObject(details.Properties);
            
            AzureSettings azureProperties = new AzureSettings
            {
                TenantId = details.ClientMachine,
                ApplicationId = properties?.ServerUsername,
                ClientSecret = properties?.ServerPassword
            };

            Client = new AzureServicePrincipalClient(azureProperties)
            {
                ApplicationId = details.StorePath
            };
        }
        
        protected void Initialize(DiscoveryJobConfiguration config)
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
            
            Client = new AzureServicePrincipalClient(azureProperties);
        }
    }
}
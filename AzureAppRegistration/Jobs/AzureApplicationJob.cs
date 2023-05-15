using AzureAppRegistration.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AzureAppRegistration.Jobs
{
    public class ServerConfig
    {
        public string ServerUsername { get; set; }
        public string ServerPassword { get; set; }
        public bool ServerUseSsl { get; set; }
        public string Type { get; set; }
    }
    public class AzureApplicationJob<T> : IOrchestratorJobExtension
    {
        public string ExtensionName => "AzAppCert";
        
        protected IAzureGraphClient Client { get; private set; }
        
        protected void Initialize(CertificateStore details)
        {
            ILogger logger = LogHandler.GetReflectedClassLogger(this);
            logger.LogDebug("Certificate Store Configuration: {Details}", JsonConvert.SerializeObject(details));
            logger.LogDebug("Initializing AzureAppGatewayClient");

            ServerConfig config = JsonConvert.DeserializeObject<ServerConfig>(details.Properties);
            
            AzureSettings azureProperties = new AzureSettings
            {
                TenantId = details.ClientMachine,
                ApplicationId = config.ServerUsername,
                ClientSecret = config.ServerPassword
            };

            if (config.Type == "OAuth/SAML (ServicePrincipal)")
            {
                Client = new AzureServicePrincipalClient(azureProperties)
                {
                    ApplicationId = details.StorePath
                };
            } else // Else case is "Authentication (Application)"
            {
                Client = new AzureApplicationClient(azureProperties)
                {
                    ApplicationId = details.StorePath
                };
            }
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
            
            if (config.ServerUsername == "OAuth/SAML (ServicePrincipal)")
            {
                Client = new AzureServicePrincipalClient(azureProperties);
            } else // Else case is "Authentication (Application)"
            {
                Client = new AzureApplicationClient(azureProperties);
            }
        }
    }
}
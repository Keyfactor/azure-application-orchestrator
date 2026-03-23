using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Microsoft.Extensions.Logging;
using System;

namespace AzureEnterpriseApplicationOrchestrator
{
    internal class PAMUtilities
    {
        internal static string ResolvePAMField(ILogger logger, IPAMSecretResolver resolver, string description, string key)
        {
            logger.MethodEntry();
            logger.LogDebug($"Fetching {description} value from PAM");
            var value = resolver.Resolve(key);
            logger.LogDebug($"Successfully fetched {description} value from PAM");
            logger.MethodExit();
            return value;
        }
    }
}

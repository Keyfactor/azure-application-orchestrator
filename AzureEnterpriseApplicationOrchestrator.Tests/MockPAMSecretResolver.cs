using Keyfactor.Orchestrators.Extensions.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureEnterpriseApplicationOrchestrator.Tests
{
    public class MockPAMSecretResolver : IPAMSecretResolver
    {
        private readonly Dictionary<string, string> _secrets;

        public MockPAMSecretResolver()
        {
            _secrets = new Dictionary<string, string>();
        }

        public MockPAMSecretResolver(Dictionary<string, string> predefinedSecrets)
        {
            _secrets = predefinedSecrets ?? new Dictionary<string, string>();
        }

        public string Resolve(string instanceInfo)
        {
            // For testing, if we have a predefined secret, return it
            if (instanceInfo != null && _secrets.ContainsKey(instanceInfo))
            {
                return _secrets[instanceInfo];
            }

            // Otherwise, just return the input (simulating no PAM resolution needed)
            return instanceInfo;
        }

        public void AddSecret(string key, string value)
        {
            _secrets[key] = value;
        }
    }
}

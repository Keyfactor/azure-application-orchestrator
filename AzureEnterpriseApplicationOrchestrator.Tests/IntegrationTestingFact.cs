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

namespace AzureEnterpriseApplicationOrchestrator.Tests;

public sealed class IntegrationTestingFact : FactAttribute
{
    public string TenantId { get; private set; }
    public string ApplicationId { get; private set; }
    public string ClientSecret { get; private set; }

    public string TargetApplicationId { get; private set; }

    public IntegrationTestingFact()
    {
        TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? string.Empty;
        ApplicationId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? string.Empty;
        ClientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? string.Empty;

        TargetApplicationId = Environment.GetEnvironmentVariable("AZURE_TARGET_APPLICATION_ID") ?? string.Empty;

        if (string.IsNullOrEmpty(TenantId) || string.IsNullOrEmpty(ApplicationId) || string.IsNullOrEmpty(ClientSecret) || string.IsNullOrEmpty(TargetApplicationId))
        {
            Skip = "Integration testing environment variables are not set - Skipping test. Please run `make setup` to set the environment variables.";
        }
    }
}

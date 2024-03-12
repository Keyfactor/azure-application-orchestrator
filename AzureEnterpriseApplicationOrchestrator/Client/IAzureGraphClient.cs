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

using System.Collections.Generic;
using Keyfactor.Orchestrators.Extensions;

namespace AzureEnterpriseApplicationOrchestrator.Client;

public interface IAzureGraphClientBuilder
{
    public IAzureGraphClientBuilder WithTenantId(string tenantId);
    public IAzureGraphClientBuilder WithTargetApplicationId(string applicationId);
    public IAzureGraphClientBuilder WithApplicationId(string applicationId);
    public IAzureGraphClientBuilder WithClientSecret(string clientSecret);
    public IAzureGraphClientBuilder WithAzureCloud(string azureCloud);
    public IAzureGraphClient Build();
}

public class OperationResult<T>
{
    public T Result { get; set; }
    public string ErrorSummary { get; set; }
    public List<string> Messages { get; set; } = new List<string>();
    public bool Success => Messages.Count == 0;

    public OperationResult(T result)
    {
        Result = result;
    }

    public void AddRuntimeErrorMessage(string message)
    {
        Messages.Add("  - " + message);
    }

    public string ErrorMessage => $"{ErrorSummary}\n{string.Join("\n", Messages)}";
}

public interface IAzureGraphClient
{
    // Application
    public void AddApplicationCertificate(string certificateName, string certificateData, string certificatePassword);
    public void RemoveApplicationCertificate(string certificateName);
    public OperationResult<IEnumerable<CurrentInventoryItem>> GetApplicationCertificates();
    public bool ApplicationCertificateExists(string certificateName);

    // Service Principal
    public void AddServicePrincipalCertificate(string certificateName, string certificateData, string certificatePassword);
    public void RemoveServicePrincipalCertificate(string certificateName);
    public OperationResult<IEnumerable<CurrentInventoryItem>> GetServicePrincipalCertificates();
    public bool ServicePrincipalCertificateExists(string certificateName);

    // Discovery
    public OperationResult<IEnumerable<string>> DiscoverApplicationIds();
}

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

using System.Security.Cryptography.X509Certificates;
using AzureEnterpriseApplicationOrchestrator.AzureSPJobs;
using AzureEnterpriseApplicationOrchestrator.Client;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace AzureEnterpriseApplicationOrchestrator.Tests;

public class AzureEnterpriseApplicationOrchestrator_AzureSP
{
    ILogger _logger { get; set; }

    public AzureEnterpriseApplicationOrchestrator_AzureSP()
    {
        ConfigureLogging();

        _logger = LogHandler.GetClassLogger<AzureEnterpriseApplicationOrchestrator_AzureSP>();
    }

    [IntegrationTestingFact]
    public void AzureSP_Inventory_IntegrationTest_ReturnSuccess()
    {
        // Arrange
        const string password = "passwordpasswordpassword";
        string certName = "SPTest" + Guid.NewGuid().ToString()[..6];
        X509Certificate2 ssCert = AzureEnterpriseApplicationOrchestrator_Client.GetSelfSignedCert(certName);
        string b64PfxSslCert = Convert.ToBase64String(ssCert.Export(X509ContentType.Pfx, password));

        IntegrationTestingFact env = new();

        IAzureGraphClient client = new GraphClient.Builder()
            .WithTenantId(env.TenantId)
            .WithApplicationId(env.ApplicationId)
            .WithClientSecret(env.ClientSecret)
            .WithTargetObjectId(env.TargetServicePrincipalObjectId)
            .Build();

        // Set up the inventory job configuration
        var config = new InventoryJobConfiguration
        {
            CertificateStoreDetails = new CertificateStore
            {
                ClientMachine = env.TenantId,
                StorePath = env.TargetServicePrincipalObjectId,
                Properties = $"{{\"ServerUsername\":\"{env.ApplicationId}\",\"ServerPassword\":\"{env.ClientSecret}\",\"AzureCloud\":\"\"}}"
            }
        };

        var inventory = new Inventory();

        // Create a certificate in the Application
        client.AddServicePrincipalCertificate(certName, b64PfxSslCert, password);

        // Act
        JobResult result = inventory.ProcessJob(config, (inventoryItems) =>
        {
            // Assert
            Assert.NotNull(inventoryItems);
            Assert.NotEmpty(inventoryItems);

            _logger.LogInformation("AzureSP_Inventory_IntegrationTest_ReturnSuccess - Success");
            return true;
        });

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);


        // Clean up
        client.RemoveServicePrincipalCertificate(certName);
    }

    [Fact]
    public void AzureSP_Inventory_ProcessJob_ValidClient_ReturnSuccess()
    {
        // Arrange
        IAzureGraphClient client = new FakeClient
        {
            CertificatesAvailableOnFakeTarget = new Dictionary<string, string>
            {
                { "test", "test" }
            }
        };

        // Set up the inventory job with the fake client
        var inventory = new Inventory
        {
            Client = client
        };

        // Set up the inventory job configuration
        var config = new InventoryJobConfiguration
        {
            CertificateStoreDetails = new CertificateStore
            {
                ClientMachine = "test",
                StorePath = "test",
                Properties = "{\"ServerUsername\":\"test\",\"ServerPassword\":\"test\",\"AzureCloud\":\"test\"}"
            },
            JobHistoryId = 1
        };

        // Act
        JobResult result = inventory.ProcessJob(config, (inventoryItems) =>
                {
                    // Assert
                    Assert.Equal(1, inventoryItems.Count());
                    Assert.Equal("test", inventoryItems.First().Alias);

                    _logger.LogInformation("AzureSP_Inventory_ProcessJob_ValidClient_ReturnSuccess - Success");
                    return true;
                });

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
    }

    [Fact]
    public void AzureSP_Inventory_ProcessJob_InvalidClient_ReturnFailure()
    {
        // Arrange
        IAzureGraphClient client = new FakeClient();

        // Set up the inventory job with the fake client
        var inventory = new Inventory
        {
            Client = client
        };

        // Set up the inventory job configuration
        var config = new InventoryJobConfiguration
        {
            CertificateStoreDetails = new CertificateStore
            {
                ClientMachine = "test",
                StorePath = "test",
                Properties = "{\"ServerUsername\":\"test\",\"ServerPassword\":\"test\",\"AzureCloud\":\"test\"}"
            },
            JobHistoryId = 1
        };

        bool callbackCalled = false;

        // Act
        JobResult result = inventory.ProcessJob(config, (inventoryItems) =>
        {
            callbackCalled = true;

            // Assert
            Assert.True(false, "Callback should not be called");
            return true;
        });

        // Assert
        Assert.False(callbackCalled);
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);

        _logger.LogInformation("AzureSP_Inventory_ProcessJob_InvalidClient_ReturnFailure - Success");
    }

    [IntegrationTestingFact]
    public void AzureSP_Discovery_IntegrationTest_ReturnSuccess()
    {
        // Arrange
        IntegrationTestingFact env = new();

        // Set up the discovery job configuration
        var config = new DiscoveryJobConfiguration
        {
            ClientMachine = env.TenantId,
            ServerUsername = env.ApplicationId,
            ServerPassword = env.ClientSecret,
            JobProperties = new Dictionary<string, object>
            {
                { "dirs", env.TenantId }
            }
        };

        var discovery = new Discovery();

        // Act
        JobResult result = discovery.ProcessJob(config, (discoveredApplicationIds) =>
        {
            // Assert
            Assert.NotNull(discoveredApplicationIds);
            Assert.NotEmpty(discoveredApplicationIds);

            _logger.LogInformation("AzureSP_Discovery_IntegrationTest_ReturnSuccess - Success");
            return true;
        });

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
    }

    [Fact]
    public void AzureSP_Discovery_ProcessJob_ValidClient_ReturnSuccess()
    {
        // Arrange
        IAzureGraphClient client = new FakeClient
        {
            ObjectIdsAvailableOnFakeTenant = new List<string> { "test" }
        };

        // Set up the discovery job with the fake client
        var discovery = new Discovery
        {
            Client = client
        };

        // Set up the discovery job configuration
        var config = new DiscoveryJobConfiguration
        {
            ClientMachine = "fake-tenant-id",
            ServerUsername = "fake-application-id",
            ServerPassword = "fake-client-secret",
            JobProperties = new Dictionary<string, object>
            {
                { "dirs", "fake-tenant-id" }
            }
        };

        // Act
        JobResult result = discovery.ProcessJob(config, (discoveredApplicationIds) =>
        {
            // Assert
            Assert.Equal(1, discoveredApplicationIds.Count());
            Assert.Equal("test", discoveredApplicationIds.First());

            _logger.LogInformation("Discovery_ProcessJob_ValidClient_ReturnSuccess - Success");
            return true;
        });

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);

        _logger.LogInformation("AzureSP_Discovery_ProcessJob_ValidClient_ReturnSuccess - Success");
    }

    [Fact]
    public void AzureSP_Discovery_ProcessJob_InvalidClient_ReturnFailure()
    {
        // Arrange
        IAzureGraphClient client = new FakeClient();

        // Set up the discovery job with the fake client
        var discovery = new Discovery
        {
            Client = client
        };

        // Set up the discovery job configuration
        var config = new DiscoveryJobConfiguration
        {
            ClientMachine = "fake-tenant-id",
            ServerUsername = "fake-application-id",
            ServerPassword = "fake-client-secret",
            JobProperties = new Dictionary<string, object>
            {
                { "dirs", "fake-tenant-id" }
            }
        };

        bool callbackCalled = false;

        // Act
        JobResult result = discovery.ProcessJob(config, (discoveredApplicationIds) =>
        {
            callbackCalled = true;

            // Assert
            Assert.True(false, "Callback should not be called");
            return true;
        });

        // Assert
        Assert.False(callbackCalled);
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);

        _logger.LogInformation("AzureSP_Discovery_ProcessJob_InvalidClient_ReturnFailure - Success");
    }

    [Fact]
    public void AzureSP_ManagementAdd_ProcessJob_ValidClient_ReturnSuccess()
    {
        // Arrange
        FakeClient client = new FakeClient();

        // Set up the management job with the fake client
        var management = new Management
        {
            Client = client
        };

        // Set up the management job configuration
        var config = new ManagementJobConfiguration
        {
            OperationType = CertStoreOperationType.Add,
            JobCertificate = new ManagementJobCertificate
            {
                Alias = "test",
                Contents = "test-certificate-data",
                PrivateKeyPassword = "test-password"
            },
            JobHistoryId = 1
        };

        // Act
        JobResult result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
        Assert.Equal(1, result.JobHistoryId);
        Assert.NotNull(client.CertificatesAvailableOnFakeTarget);
        if (client.CertificatesAvailableOnFakeTarget != null)
        {
            Assert.True(client.CertificatesAvailableOnFakeTarget.ContainsKey("test"));
        }

        _logger.LogInformation("AzureSP_ManagementAdd_ProcessJob_ValidClient_ReturnSuccess - Success");
    }

    [Theory]
    [InlineData("test", "")]
    [InlineData("", "test-password")]
    [InlineData("", "")]
    public void AzureSP_ManagementAdd_ProcessJob_InvalidJobConfig_ReturnFailure(string alias, string pkPassword)
    {
        // Arrange
        FakeClient client = new FakeClient();

        // Set up the management job with the fake client
        var management = new Management
        {
            Client = client
        };

        // Set up the management job configuration
        var config = new ManagementJobConfiguration
        {
            OperationType = CertStoreOperationType.Add,
            JobCertificate = new ManagementJobCertificate
            {
                Alias = alias,
                Contents = "test-certificate-data",
                PrivateKeyPassword = pkPassword
            },
            JobHistoryId = 1
        };

        // Act
        JobResult result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Failure, result.Result);
        Assert.Equal(1, result.JobHistoryId);

        _logger.LogInformation("AzureSP_ManagementAdd_ProcessJob_InvalidJobConfig_ReturnFailure - Success");
    }

    [Fact]
    public void AzureSP_ManagementRemove_ProcessJob_ValidClient_ReturnSuccess()
    {
        // Arrange
        FakeClient client = new FakeClient
        {
            CertificatesAvailableOnFakeTarget = new Dictionary<string, string>
            {
                { "test", "test" }
            }
        };

        // Set up the management job with the fake client
        var management = new Management
        {
            Client = client
        };

        // Set up the management job configuration
        var config = new ManagementJobConfiguration
        {
            OperationType = CertStoreOperationType.Remove,
            JobCertificate = new ManagementJobCertificate
            {
                Alias = "test",
            },
            JobHistoryId = 1
        };

        // Act
        JobResult result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
        Assert.Equal(1, result.JobHistoryId);
        if (client.CertificatesAvailableOnFakeTarget != null)
        {
            Assert.False(client.CertificatesAvailableOnFakeTarget.ContainsKey("test"));
        }

        _logger.LogInformation("AzureSP_ManagementRemove_ProcessJob_ValidClient_ReturnSuccess - Success");
    }

    [Fact]
    public void AzureSP_ManagementReplace_ProcessJob_ValidClient_ReturnSuccess()
    {
        // Arrange
        FakeClient client = new FakeClient
        {
            CertificatesAvailableOnFakeTarget = new Dictionary<string, string>
            {
                { "test", "original-cert-data" }
            }
        };

        // Set up the management job with the fake client
        var management = new Management
        {
            Client = client
        };

        // Set up the management job configuration
        var config = new ManagementJobConfiguration
        {
            OperationType = CertStoreOperationType.Add,
            Overwrite = true,
            JobCertificate = new ManagementJobCertificate
            {
                Alias = "test",
                Contents = "new-certificate-data",
                PrivateKeyPassword = "test-password"
            },
            JobHistoryId = 1
        };

        // Act
        JobResult result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);
        Assert.Equal(1, result.JobHistoryId);
        if (client.CertificatesAvailableOnFakeTarget != null)
        {
            Assert.True(client.CertificatesAvailableOnFakeTarget.ContainsKey("test"));
            Assert.Equal("new-certificate-data", client.CertificatesAvailableOnFakeTarget["test"]);
        }

        _logger.LogInformation("AzureSP_ManagementReplace_ProcessJob_ValidClient_ReturnSuccess - Success");
    }

    [IntegrationTestingFact]
    public void AzureSP_Management_IntegrationTest_ReturnSuccess()
    {
        // Arrange
        IntegrationTestingFact env = new();

        string testHostname = "azureapplicationUnitTest.com";
        string certName = "AppTest" + Guid.NewGuid().ToString()[..6];
        string password = "password";

        X509Certificate2 ssCert = AzureEnterpriseApplicationOrchestrator_Client.GetSelfSignedCert(testHostname);

        string b64PfxSslCert = Convert.ToBase64String(ssCert.Export(X509ContentType.Pfx, password));

        // Set up the management job configuration
        var config = new ManagementJobConfiguration
        {
            OperationType = CertStoreOperationType.Add,
            CertificateStoreDetails = new CertificateStore
            {
                ClientMachine = env.TenantId,
                StorePath = env.TargetServicePrincipalObjectId,
                Properties = $"{{\"ServerUsername\":\"{env.ApplicationId}\",\"ServerPassword\":\"{env.ClientSecret}\",\"AzureCloud\":\"\"}}"
            },
            JobCertificate = new ManagementJobCertificate
            {
                Alias = certName,
                Contents = b64PfxSslCert,
                PrivateKeyPassword = password
            },
        };

        var management = new Management();

        // Act
        // This will process a Management Add job
        JobResult result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);

        // Arrange

        ssCert = AzureEnterpriseApplicationOrchestrator_Client.GetSelfSignedCert(testHostname);

        b64PfxSslCert = Convert.ToBase64String(ssCert.Export(X509ContentType.Pfx, password));

        config.OperationType = CertStoreOperationType.Add;
        config.Overwrite = true;
        config.JobCertificate = new ManagementJobCertificate
        {
            Alias = certName,
            Contents = b64PfxSslCert,
            PrivateKeyPassword = password
        };

        // Act
        // This will process a Management Replace job
        result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);

        // Arrange
        config.OperationType = CertStoreOperationType.Remove;
        config.JobCertificate = new ManagementJobCertificate
        {
            Alias = certName,
        };

        // Act
        // This will process a Management Remove job
        result = management.ProcessJob(config);

        // Assert
        Assert.Equal(OrchestratorJobStatusJobResult.Success, result.Result);

        _logger.LogInformation("AzureSP_Management_IntegrationTest_ReturnSuccess - Success");
    }

    static void ConfigureLogging()
    {
        var config = new NLog.Config.LoggingConfiguration();

        // Targets where to log to: File and Console
        var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
        logconsole.Layout = @"${date:format=HH\:mm\:ss} ${logger} [${level}] - ${message}";

        // Rules for mapping loggers to targets            
        config.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, logconsole);

        // Apply config           
        NLog.LogManager.Configuration = config;

        LogHandler.Factory = LoggerFactory.Create(builder =>
                {
                    builder.AddNLog();
                });
    }
}

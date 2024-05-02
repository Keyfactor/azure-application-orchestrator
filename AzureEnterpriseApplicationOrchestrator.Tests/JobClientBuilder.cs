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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using AzureEnterpriseApplicationOrchestrator;
using AzureEnterpriseApplicationOrchestrator.Client;
using AzureEnterpriseApplicationOrchestrator.Tests;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

public class AzureEnterpriseApplicationOrchestrator_JobClientBuilder
{
    ILogger _logger { get; set;}

    public AzureEnterpriseApplicationOrchestrator_JobClientBuilder()
    {
        ConfigureLogging();

        _logger = LogHandler.GetClassLogger<AzureEnterpriseApplicationOrchestrator_JobClientBuilder>();
    }

    [Fact]
    public void GraphJobClientBuilder_ValidCertificateStoreConfigWithClientSecret_BuildValidClient()
    {
        // Verify that the GraphJobClientBuilder uses the certificate store configuration
        // provided by Keyfactor Command/the Universal Orchestrator correctly as required
        // by the IAzureGraphClientBuilder interface.

        // Arrange
        GraphJobClientBuilder<FakeClient.FakeBuilder> jobClientBuilderWithFakeBuilder = new();

        // Set up the certificate store with names that correspond to how we expect them to be interpreted by
        // the builder
        CertificateStore fakeCertificateStoreDetails = new()
        {
            ClientMachine = "fake-tenant-id",
            StorePath = "fake-azure-target-application-id",
            Properties = "{\"ServerUsername\":\"fake-azure-application-id\",\"ServerPassword\":\"fake-azure-client-secret\",\"AzureCloud\":\"fake-azure-cloud\"}"
        };

        // Act
        IAzureGraphClient fakeAppGatewayClient = jobClientBuilderWithFakeBuilder
            .WithCertificateStoreDetails(fakeCertificateStoreDetails)
            .Build();

        // Assert

        // IAzureGraphClient doesn't require any of the properties set by the builder to be exposed
        // since the production Build() method creates an Azure Resource Manager client.
        // But, our builder is fake and exposes the properties we need to test (via the FakeBuilder class).
        Assert.Equal("fake-tenant-id", jobClientBuilderWithFakeBuilder._builder._tenantId);
        Assert.Equal("fake-azure-target-application-id", jobClientBuilderWithFakeBuilder._builder._targetApplicationId);
        Assert.Equal("fake-azure-application-id", jobClientBuilderWithFakeBuilder._builder._applicationId);
        Assert.Equal("fake-azure-client-secret", jobClientBuilderWithFakeBuilder._builder._clientSecret);
        Assert.Equal("fake-azure-cloud", jobClientBuilderWithFakeBuilder._builder._azureCloudEndpoint);

        _logger.LogInformation("GraphJobClientBuilder_ValidCertificateStoreConfig_BuildValidClient - Success");
    }

    [IntegrationTestingTheory]
    [InlineData("pkcs12")]
    [InlineData("pem")]
    [InlineData("encryptedPem")]
    public void GraphJobClientBuilder_ValidCertificateStoreConfigWithClientCertificate_BuildValidClient(string certificateFormat)
    {
        // Verify that the GraphJobClientBuilder uses the certificate store configuration
        // provided by Keyfactor Command/the Universal Orchestrator correctly as required
        // by the IAzureGraphClientBuilder interface.

        // Arrange
        GraphJobClientBuilder<FakeClient.FakeBuilder> jobClientBuilderWithFakeBuilder = new();

        string password = "passwordpasswordpassword";
        string certName = "SPTest" + Guid.NewGuid().ToString()[..6];
        X509Certificate2 ssCert = GetSelfSignedCert(certName);

        string b64ClientCertificate;
        if (certificateFormat == "pkcs12")
        {
            b64ClientCertificate = Convert.ToBase64String(ssCert.Export(X509ContentType.Pfx, password));
        }
        else if (certificateFormat == "pem")
        {
            string pemCert = ssCert.ExportCertificatePem();
            string keyPem = ssCert.GetRSAPrivateKey()!.ExportPkcs8PrivateKeyPem();
            b64ClientCertificate = Convert.ToBase64String(Encoding.UTF8.GetBytes(keyPem + '\n' + pemCert));
            password = "";
        }
        else
        {
            PbeParameters pbeParameters = new PbeParameters(
                    PbeEncryptionAlgorithm.Aes256Cbc,
                    HashAlgorithmName.SHA384,
                    300_000);
            string pemCert = ssCert.ExportCertificatePem();
            string keyPem = ssCert.GetRSAPrivateKey()!.ExportEncryptedPkcs8PrivateKeyPem(password.ToCharArray(), pbeParameters);
            b64ClientCertificate = Convert.ToBase64String(Encoding.UTF8.GetBytes(keyPem + '\n' + pemCert));
        }

        // Set up the certificate store with names that correspond to how we expect them to be interpreted by
        // the builder
        CertificateStore fakeCertificateStoreDetails = new()
        {
            ClientMachine = "fake-tenant-id",
            StorePath = "fake-azure-target-application-id",
            Properties = $@"{{""ServerUsername"": ""fake-azure-application-id"",""ServerPassword"": ""{password}"",""ClientCertificate"": ""{b64ClientCertificate}"",""AzureCloud"": ""fake-azure-cloud""}}"
        };

        // Act
        IAzureGraphClient fakeAppGatewayClient = jobClientBuilderWithFakeBuilder
            .WithCertificateStoreDetails(fakeCertificateStoreDetails)
            .Build();

        // Assert

        // IAzureGraphClient doesn't require any of the properties set by the builder to be exposed
        // since the production Build() method creates an Azure Resource Manager client.
        // But, our builder is fake and exposes the properties we need to test (via the FakeBuilder class).
        Assert.Equal("fake-tenant-id", jobClientBuilderWithFakeBuilder._builder._tenantId);
        Assert.Equal("fake-azure-target-application-id", jobClientBuilderWithFakeBuilder._builder._targetApplicationId);
        Assert.Equal("fake-azure-application-id", jobClientBuilderWithFakeBuilder._builder._applicationId);
        Assert.Equal("fake-azure-cloud", jobClientBuilderWithFakeBuilder._builder._azureCloudEndpoint);
        Assert.Equal(ssCert.GetCertHash(), jobClientBuilderWithFakeBuilder._builder._clientCertificate!.GetCertHash());
        Assert.NotNull(jobClientBuilderWithFakeBuilder._builder._clientCertificate!.GetRSAPrivateKey());
        Assert.Equal(jobClientBuilderWithFakeBuilder._builder._clientCertificate!.GetRSAPrivateKey()!.ExportRSAPrivateKeyPem(), ssCert.GetRSAPrivateKey()!.ExportRSAPrivateKeyPem());

        _logger.LogInformation("GraphJobClientBuilder_ValidCertificateStoreConfig_BuildValidClient - Success");
    }

    public static X509Certificate2 GetSelfSignedCert(string hostname)
    {
        RSA rsa = RSA.Create(2048);
        CertificateRequest req = new CertificateRequest($"CN={hostname}", rsa, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

        SubjectAlternativeNameBuilder subjectAlternativeNameBuilder = new SubjectAlternativeNameBuilder();
        subjectAlternativeNameBuilder.AddDnsName(hostname);
        req.CertificateExtensions.Add(subjectAlternativeNameBuilder.Build());
        req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DataEncipherment | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));        
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("2.5.29.32.0"), new Oid("1.3.6.1.5.5.7.3.1") }, false));

        X509Certificate2 selfSignedCert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));
        Console.Write($"Created self-signed certificate for \"{hostname}\" with thumbprint {selfSignedCert.Thumbprint}\n");
        return selfSignedCert;
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

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

using Microsoft.Extensions.Logging;
using Keyfactor.Logging;
using NLog.Extensions.Logging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AzureEnterpriseApplicationOrchestrator.Client;

namespace AzureEnterpriseApplicationOrchestrator.Tests;

public class AzureEnterpriseApplicationOrchestrator_Client
{
    public AzureEnterpriseApplicationOrchestrator_Client()
    {
        ConfigureLogging();
    }

    [IntegrationTestingTheory]
    [InlineData("clientcert")]
    [InlineData("clientsecret")]
    public void GraphClient_Application_AddGetRemove_ReturnSuccess(string testAuthMethod)
    {
        // Arrange
        string certName = "AppTest" + Guid.NewGuid().ToString()[..6];
        X509Certificate2 ssCert = GetSelfSignedCert(certName);
        string b64Cert = Convert.ToBase64String(ssCert.Export(X509ContentType.Cert));

        IntegrationTestingFact env = new();
        IAzureGraphClientBuilder clientBuilder = new GraphClient.Builder()
            .WithTenantId(env.TenantId)
            .WithApplicationId(env.ApplicationId)
            .WithTargetApplicationId(env.TargetApplicationId);

        if (testAuthMethod == "clientcert")
        {
            clientBuilder.WithClientSecret(env.ClientSecret);
        }
        else
        {
            var cert = X509Certificate2.CreateFromPemFile(env.ClientCertificatePath);
            clientBuilder.WithClientCertificate(cert);
        }
        
        IAzureGraphClient client = clientBuilder.Build();

        // Step 1 - Add the certificate to the Application

        client.AddApplicationCertificate(certName, b64Cert);

        // Assert
        // The certificate should be added to the Application.
        // If this is not the case, AddApplicationCertificate will throw an exception.

        // Step 2 - Get the certificate from the Application

        // Act
        OperationResult<IEnumerable<Keyfactor.Orchestrators.Extensions.CurrentInventoryItem>> operationResult = client.GetApplicationCertificates();
        
        // Assert
        Assert.True(operationResult.Success);
        Assert.NotNull(operationResult.Result);
        Assert.True(operationResult.Result.Any(c => c.Alias == certName));
        Assert.True(operationResult.Result.Any(c => c.Alias == certName && c.PrivateKeyEntry == false));
        
        // Step 3 - Determine if the certificate exists in the Application

        // Act
        bool exists = client.ApplicationCertificateExists(certName);

        // Assert
        Assert.True(exists);

        // Step 4 - Remove the certificate from the Application
        client.RemoveApplicationCertificate(certName);


        // Assert
        // The certificate should be removed from the Application.
        // If this is not the case, RemoveApplicationCertificate will throw an exception.

        // Step 5 - Determine if the certificate exists in the Application

        // Act
        exists = client.ApplicationCertificateExists(certName);

        // Assert
        Assert.False(exists);
    }

    [IntegrationTestingTheory]
    [InlineData("clientcert")]
    [InlineData("clientsecret")]
    public void GraphClient_ServicePrincipal_AddGetRemove_ReturnSuccess(string testAuthMethod)
    {
        // Arrange
        const string password = "passwordpasswordpassword";
        string certName = "SPTest" + Guid.NewGuid().ToString()[..6];
        X509Certificate2 ssCert = GetSelfSignedCert(certName);
        string b64PfxSslCert = Convert.ToBase64String(ssCert.Export(X509ContentType.Pfx, password));

        IntegrationTestingFact env = new();
        IAzureGraphClientBuilder clientBuilder = new GraphClient.Builder()
            .WithTenantId(env.TenantId)
            .WithApplicationId(env.ApplicationId)
            .WithTargetApplicationId(env.TargetApplicationId);

        if (testAuthMethod == "clientcert")
        {
            clientBuilder.WithClientSecret(env.ClientSecret);
        }
        else
        {
            var cert = X509Certificate2.CreateFromPemFile(env.ClientCertificatePath);
            clientBuilder.WithClientCertificate(cert);
        }
        
        IAzureGraphClient client = clientBuilder.Build();

        // Step 1 - Add the certificate to the Service Principal (and set it as the preferred SAML signing certificate)

        client.AddServicePrincipalCertificate(certName, b64PfxSslCert, password);

        // Assert
        // The certificate should be added to the Service Principal and set as the preferred SAML signing certificate.
        // If this is not the case, AddServicePrincipalCertificate will throw an exception.

        // Step 2 - Get the certificate from the Service Principal
        OperationResult<IEnumerable<Keyfactor.Orchestrators.Extensions.CurrentInventoryItem>> operationResult = client.GetServicePrincipalCertificates();

        // Assert
        Assert.True(operationResult.Success);
        Assert.NotNull(operationResult.Result);
        Assert.True(operationResult.Result.Any(c => c.Alias == certName));
        Assert.True(operationResult.Result.Any(c => c.Alias == certName && c.PrivateKeyEntry));

        // Step 3 - Determine if the certificate exists in the Service Principal

        // Act
        bool exists = client.ServicePrincipalCertificateExists(certName);

        // Assert
        Assert.True(exists);

        // Step 4 - Remove the certificate from the Service Principal
        client.RemoveServicePrincipalCertificate(certName);


        // Assert
        // The certificate should be removed from the Service Principal.
        // If this is not the case, RemoveServicePrincipalCertificate will throw an exception.

        // Step 5 - Determine if the certificate exists in the Service Principal

        // Wait for changes to replicate
        Thread.Sleep(5000);

        // Act
        exists = client.ServicePrincipalCertificateExists(certName);

        // Assert
        Assert.False(exists);
    }

    [IntegrationTestingTheory]
    [InlineData("clientcert")]
    [InlineData("clientsecret")]
    public void GraphClient_DiscoverApplicationIds_ReturnSuccess(string testAuthMethod)
    {
        // Arrange
        const string password = "passwordpasswordpassword";
        string certName = "SPTest" + Guid.NewGuid().ToString()[..6];
        X509Certificate2 ssCert = GetSelfSignedCert(certName);
        string b64PfxSslCert = Convert.ToBase64String(ssCert.Export(X509ContentType.Pfx, password));

        IntegrationTestingFact env = new();
        IAzureGraphClientBuilder clientBuilder = new GraphClient.Builder()
            .WithTenantId(env.TenantId)
            .WithApplicationId(env.ApplicationId)
            .WithTargetApplicationId(env.TargetApplicationId);

        if (testAuthMethod == "clientcert")
        {
            clientBuilder.WithClientSecret(env.ClientSecret);
        }
        else
        {
            var cert = X509Certificate2.CreateFromPemFile(env.ClientCertificatePath);
            clientBuilder.WithClientCertificate(cert);
        }
        
        IAzureGraphClient client = clientBuilder.Build();

        // Act
        OperationResult<IEnumerable<string>> operationResult = client.DiscoverApplicationIds();

        // Assert
        Assert.True(operationResult.Success);
        Assert.NotNull(operationResult.Result);
        Assert.True(operationResult.Result.Any());
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


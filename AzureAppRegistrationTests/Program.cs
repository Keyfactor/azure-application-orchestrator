using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using AzureAppRegistration.Client;
using Microsoft.Graph;

namespace AzureAppRegistrationTests
{
    public class AzureAppRegistrationTests
    {
        public AzureAppRegistrationTests()
        {
            AzureProperties properties = new AzureProperties
            {
                TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? string.Empty,
                ApplicationId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? string.Empty,
                ClientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? string.Empty
            };
            
            AzureApplicationClient client = new AzureApplicationClient(properties)
            {
                ApplicationId = "a31973fd-b47a-4e7f-91fa-90c67dfab45f"
            };

            client.GetApplicationCertificates();
            
            X509Certificate2 cert = GetSelfSignedCert("test");
            
            Console.WriteLine(Encoding.UTF8.GetString(cert.Export(X509ContentType.Cert, "password")));

            client.AddApplicationCertificate("asdf",
                Encoding.UTF8.GetString(cert.Export(X509ContentType.Cert, "password")), "password");
        }

        public static void Main(string[] args)
        {
            AzureAppRegistrationTests tests = new AzureAppRegistrationTests();
        }
        
        private static X509Certificate2 GetSelfSignedCert(string hostname)
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
    }
}
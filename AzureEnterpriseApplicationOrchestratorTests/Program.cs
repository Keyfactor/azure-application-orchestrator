// Copyright 2023 Keyfactor
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AzureEnterpriseApplicationOrchestrator.Client;
using Keyfactor.Orchestrators.Extensions;

namespace AzureEnterpriseApplicationOrchestratorTests
{
    public class AzureAppRegistrationTests
    {
        public static void Main(string[] args)
        {
            AzureAppRegistrationTests tests = new AzureAppRegistrationTests();
            
            // Create self signed certificate
            const string password = "passwordpasswordpassword";
            string certName = "AppTest" + Guid.NewGuid().ToString()[..6];
            X509Certificate2 ssCert = GetSelfSignedCert(certName);
            string b64PfxSslCert = Convert.ToBase64String(ssCert.Export(X509ContentType.Pfx, password));
            
            //tests.ListAllSpCertificates();

            tests.TestGetCertificates();
            
            tests.TestAddCertificate(certName, b64PfxSslCert, password);
            
            tests.TestGetCertificates();
            
            ssCert = GetSelfSignedCert(certName);
            b64PfxSslCert = Convert.ToBase64String(ssCert.Export(X509ContentType.Pfx, password));
            
            tests.TestReplaceCertificate(certName, b64PfxSslCert, password);
            
            tests.TestGetCertificates();
            
            tests.TestRemoveCertificate(certName);
        }
        
        public AzureAppRegistrationTests()
        {
            AzureSettings properties = new AzureSettings
            {
                TenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? string.Empty,
                ApplicationId = Environment.GetEnvironmentVariable("AZURE_APP_ID") ?? string.Empty,
                ClientSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET") ?? string.Empty
            };

            ApplicationClient = new AzureApplicationClient(properties)
            {
                ApplicationId = "0a6f8aa3-1912-4f95-baf6-507075bb42de"
            };

            ServicePrincipalClient = new AzureServicePrincipalClient(properties)
            {
                ApplicationId = "0a6f8aa3-1912-4f95-baf6-507075bb42de"
            };
        }
        
        private AzureApplicationClient ApplicationClient { get; }
        private AzureServicePrincipalClient ServicePrincipalClient { get; }

        public void TestAddCertificate(string certName, string b64PfxSslCert, string password)
        {
            Console.Write("Adding Application Certificate...\n");
            ApplicationClient.AddCertificate(certName, b64PfxSslCert, password);
            
            Console.Write("Adding Service Principal Certificate...\n");
            ServicePrincipalClient.AddCertificate(certName, b64PfxSslCert, password);
        }

        public void TestGetCertificates()
        {
            Console.WriteLine("Getting Application Certificates...");
            foreach (CurrentInventoryItem certInv in ApplicationClient.GetInventory())
            {
                Console.WriteLine("    Found Certificate called: " + certInv.Alias);
            }
            
            Console.WriteLine("Getting Service Principal Certificates...");
            foreach (CurrentInventoryItem certInv in ServicePrincipalClient.GetInventory())
            {
                Console.WriteLine("    Found Certificate called: " + certInv.Alias);
            }
        }

        public void TestRemoveCertificate(string certName)
        {
            Console.WriteLine($"Removing Application Certificate called {certName}...");
            ApplicationClient.RemoveCertificate(certName);
            
            Console.WriteLine($"Removing Service Principal Certificate called {certName}...");
            ServicePrincipalClient.RemoveCertificate(certName);
        }

        public void TestReplaceCertificate(string certName, string b64PfxSslCert, string password)
        {
            Console.WriteLine("Replacing Application Certificate...");
            ApplicationClient.ReplaceCertificate(certName, b64PfxSslCert, password);
            
            Console.WriteLine("Replacing Service Principal Certificate...");
            ServicePrincipalClient.ReplaceCertificate(certName, b64PfxSslCert, password);
        }

        private void ListAllSpCertificates()
        {
            // Match the service principal to the application registration
            Dictionary<string, string> appRegistrations = ApplicationClient.GetApplications();
            Dictionary<string, string> servicePrincipals = ApplicationClient.GetServicePrincipals();

            // Find the intersection:
            Dictionary<string, string> intersection = appRegistrations
                .Where(kvp => servicePrincipals.ContainsKey(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            foreach (KeyValuePair<string, string> kvp in intersection)
            {
                Console.WriteLine("    App ID {0} is both an Application and Service Principal", kvp.Key);
            }

            // For each Application in the tenant, list all certificates
            foreach (KeyValuePair<string, string> kvp in ApplicationClient.GetApplications())
            {
                // The key contains the application ID
                ApplicationClient.ApplicationId = kvp.Key;
                
                // Get the certificates for the service principal
                foreach (CurrentInventoryItem certInv in ApplicationClient.GetInventory())
                {
                    Console.WriteLine($"    Found Application Certificate called: {certInv.Alias} for App ID {kvp.Key} and Object ID {kvp.Value})");
                }
            }
            
            // For each Service Principal in the tenant, list all certificates
            foreach (KeyValuePair<string, string> kvp in ServicePrincipalClient.GetServicePrincipals())
            {
                // The key contains the application ID
                ServicePrincipalClient.ApplicationId = kvp.Key;
                
                // Get the certificates for the service principal
                foreach (CurrentInventoryItem certInv in ServicePrincipalClient.GetInventory())
                {
                    Console.WriteLine($"    Found Service Principal Certificate called: {certInv.Alias} for App ID {kvp.Key} and Object ID {kvp.Value}");
                }
            }
            
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
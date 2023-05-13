using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;

namespace AzureAppRegistration.Client
{
    public class AzureApplicationClient : AzureGraphClient<Application>, IAzureGraphClient
    {
        public AzureApplicationClient(AzureSettings properties) : base(properties)
        {
            
        }

        public void AddCertificateAndKey(string certificateName, string certificateData, string certificatePassword)
        {
            // certificateData is a base64 encoded PFX certificate
            X509Certificate2 certificate = SerializeCertificate(certificateData, certificatePassword);
            if (certificate.Thumbprint == null)
                throw new Exception("Could not calculate thumbprint for certificate");
            
            // Calculate the SHA256 hash of the certificate's thumbprint
            byte[] customKeyId = Encoding.UTF8.GetBytes(certificate.Thumbprint)[..32];
            
            Log.LogDebug("Adding certificate called \"{CertificateName}\" to application ID \"{ApplicationId}\" (custom key ID {Hash})", certificateName, ApplicationId, Encoding.UTF8.GetString(customKeyId));

            // Create a GUID to represent the key ID and to link the key to the certificate
            Guid privKeyGuid = Guid.NewGuid();

            // Update the application object
            Log.LogDebug("    Updating application object for application ID \"{ApplicationId}\"", ApplicationId);
            try
            {
                GraphClient.Applications[ObjectId].PatchAsync(new Application
                {
                    KeyCredentials = new List<KeyCredential>()
                    {
                        new KeyCredential {
                            DisplayName = certificateName,
                            Type = "AsymmetricX509Cert",
                            Usage = "Verify",
                            CustomKeyIdentifier = customKeyId,
                            StartDateTime = DateTimeOffset.Parse(certificate.GetEffectiveDateString()),
                            EndDateTime = DateTimeOffset.Parse(certificate.GetExpirationDateString()),
                            KeyId = Guid.NewGuid(),
                            Key = certificate.Export(X509ContentType.Cert)
                        },
                        new KeyCredential {
                            DisplayName = certificateName,
                            Type = "X509CertAndPassword",
                            Usage = "Sign",
                            CustomKeyIdentifier = customKeyId,
                            StartDateTime = DateTimeOffset.Parse(certificate.GetEffectiveDateString()),
                            EndDateTime = DateTimeOffset.Parse(certificate.GetExpirationDateString()),
                            KeyId = privKeyGuid,
                            Key = certificate.Export(X509ContentType.Pfx, certificatePassword)
                        }
                    },
                    PasswordCredentials = new List<PasswordCredential>()
                    {
                        new PasswordCredential
                        {
                            CustomKeyIdentifier = customKeyId,
                            KeyId = privKeyGuid,
                            StartDateTime = DateTimeOffset.Parse(certificate.GetEffectiveDateString()),
                            EndDateTime = DateTimeOffset.Parse(certificate.GetExpirationDateString()),
                            SecretText = certificatePassword,
                        }
                    }
                }).Wait();
            }
            catch (AggregateException e)
            {
                Log.LogError("Failed to update application with new certificates: {Message}", e.Message);
                throw;
            }

            Log.LogDebug("    Certificate added successfully");
        }

        public void AddCertificate(string certificateName, string certificateData, string certificatePassword)
        {
            // certificateData is a base64 encoded PFX certificate
            X509Certificate2 certificate = SerializeCertificate(certificateData, certificatePassword);
            if (certificate.Thumbprint == null)
                throw new Exception("Could not calculate thumbprint for certificate");
            
            // Calculate the SHA256 hash of the certificate's thumbprint
            byte[] customKeyId = Encoding.UTF8.GetBytes(certificate.Thumbprint)[..32];
            
            Log.LogDebug("Adding certificate called \"{CertificateName}\" to application ID \"{ApplicationId}\" (custom key ID {Hash})", certificateName, ApplicationId, Encoding.UTF8.GetString(customKeyId));
            
            // Get the application object
            Application application = GetApplication();

            // Update the application object
            Log.LogDebug("    Updating application object for application ID \"{ApplicationId}\"", ApplicationId);
            try
            {
                GraphClient.Applications[ObjectId].PatchAsync(new Application
                {
                    KeyCredentials = new List<KeyCredential>(DeepCopyKeyList(application.KeyCredentials))
                    {
                        new KeyCredential {
                            DisplayName = certificateName,
                            Type = "AsymmetricX509Cert",
                            Usage = "Verify",
                            CustomKeyIdentifier = customKeyId,
                            StartDateTime = DateTimeOffset.Parse(certificate.GetEffectiveDateString()),
                            EndDateTime = DateTimeOffset.Parse(certificate.GetExpirationDateString()),
                            KeyId = Guid.NewGuid(),
                            Key = certificate.Export(X509ContentType.Cert)
                        }
                    }
                }).Wait();
            }
            catch (AggregateException e)
            {
                Log.LogError("Failed to update application with new certificates: {Message}", e.Message);
                throw;
            }

            Log.LogDebug("    Certificate added successfully");
        }

        public void ReplaceCertificate(string certificateName, string certificateData, string certificatePassword)
        {
            X509Certificate2 cert = SerializeCertificate(certificateData, certificatePassword);
            
            Log.LogDebug("Replacing certificate called \"{CertificateName}\" for application ID \"{ApplicationId}\"", certificateName, ApplicationId);

            // Deep copy the key list
            List<KeyCredential> deepKeyList = DeepCopyKeyList(GetApplication().KeyCredentials);
            
            // Update the key credential that matches the new certificate's name with the new certificate
            foreach (KeyCredential keyCredential in deepKeyList.Where(keyCredential => keyCredential.DisplayName == certificateName))
            {
                Log.LogDebug("    Replacing key credential \"{DisplayName}\" ({KeyId})", keyCredential.DisplayName, keyCredential.KeyId);

                // Update the key credential
                keyCredential.Key = cert.Export(X509ContentType.Cert);
                keyCredential.StartDateTime = DateTimeOffset.Parse(cert.GetEffectiveDateString());
                keyCredential.EndDateTime = DateTimeOffset.Parse(cert.GetExpirationDateString());
            }
            
            // Update the application object
            Log.LogDebug("    Updating application object for application ID \"{ApplicationId}\"", ApplicationId);
            try
            {
                GraphClient.Applications[ObjectId].PatchAsync(new Application
                {
                    KeyCredentials = deepKeyList
                }).Wait();
            }
            catch (AggregateException e)
            {
                Log.LogError("Failed to update application with new certificates: {Message}", e.Message);
                throw;
            }
        }

        public void RemoveCertificate(string certificateName)
        {
            // Get the application object
            Application application = GetApplication();
            
            // Don't delete existing certificates/key passwords unless they match the certificateName
            List<KeyCredential> keys = DeepCopyKeyList(application.KeyCredentials);
            
            // Create a new list for keys we want to keep
            List<KeyCredential> keysToKeep = new List<KeyCredential>();

            // Find certificates that match the thumbprint, and store their GUIDs
            foreach (KeyCredential keyCredential in keys)
            {
                if (keyCredential.DisplayName == certificateName)
                {
                    Log.LogDebug("    Removing key credential \"{DisplayName}\" ({KeyId})", keyCredential.DisplayName, keyCredential.KeyId);
                    continue;
                }

                keysToKeep.Add(keyCredential);
            }
            
            // Update the application object
            Log.LogDebug("    Updating application object for application ID \"{ApplicationId}\"", ApplicationId);
            try
            {
                GraphClient.Applications[ObjectId].PatchAsync(new Application
                {
                    KeyCredentials = keysToKeep
                }).Wait();
            }
            catch (AggregateException e)
            {
                Log.LogError("Failed to update application with new certificates: {Message}", e.Message);
                throw;
            }
        }

        public bool CertificateExists(string certificateName)
        {
            Application application = GetApplication();
            
            return application.KeyCredentials != null && application.KeyCredentials.Any(c => c.DisplayName == certificateName);
        }
    }
}
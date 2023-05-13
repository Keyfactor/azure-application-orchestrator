using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.ServicePrincipals.Item.AddPassword;
using Microsoft.Graph.Models;
using Microsoft.Graph.ServicePrincipals.Item.AddKey;
using Microsoft.Graph.ServicePrincipals.Item.RemovePassword;

namespace AzureAppRegistration.Client
{
    public class AzureServicePrincipalClient : AzureGraphClient<ServicePrincipal>, IAzureGraphClient
    {
        public AzureServicePrincipalClient(AzureSettings properties) : base(properties)
        {
            
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
            
            // Create a GUID to represent the key ID and to link the key to the certificate
            Guid privKeyGuid = Guid.NewGuid();
            
            // Update the service principal object
            Log.LogDebug("    Updating service principal object for application ID \"{ApplicationId}\"", ApplicationId);
            try
            {
                GraphClient.ServicePrincipals[ObjectId].PatchAsync(new ServicePrincipal
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
            } catch (AggregateException e)
            {
                Log.LogWarning("    Failed to update service principal object: {Message}", e.Message);
                // TODO remove certificates to avoid leaving the service principal in a bad state
                throw;
            }
            
            // Update the preferred SAML certificate
            try
            {
                GraphClient.ServicePrincipals[ObjectId].PatchAsync(new ServicePrincipal
                {
                    PreferredTokenSigningKeyThumbprint = certificate.Thumbprint
                }).Wait();
            }
            catch (AggregateException e)
            {
                Log.LogWarning("    Failed to set preferred SAML certificate: {Message}", e.Message);
                // TODO remove certificates to avoid leaving the service principal in a bad state
                throw;
            }
        }

        private void AddCertificate(string certificateName, string certificateData, string certificatePassword, bool isSamlPreferred=false)
        {
            // certificateData is a base64 encoded PFX certificate
            X509Certificate2 certificate = SerializeCertificate(certificateData, certificatePassword);
            if (certificate.Thumbprint == null)
                throw new Exception("Could not calculate thumbprint for certificate");
            
            // Calculate the SHA256 hash of the certificate's thumbprint
            byte[] customKeyId = Encoding.UTF8.GetBytes(certificate.Thumbprint)[..32];
            Log.LogDebug("Adding certificate called \"{CertificateName}\" to application ID \"{ApplicationId}\" (custom key ID {Hash})", certificateName, ApplicationId, Encoding.UTF8.GetString(customKeyId));
            
            // Get the service principal object
            ServicePrincipal servicePrincipal = GetServicePrincipal();

            // Create a GUID to represent the key ID and to link the key to the certificate
            Guid privKeyGuid = Guid.NewGuid();
            
            // // ========================================================================================================
            //
            // try
            // {
            //     GraphClient.ServicePrincipals[ObjectId].AddKey.PostAsync(new AddKeyPostRequestBody
            //     {
            //         KeyCredential = new KeyCredential
            //         {
            //             Type = "X509CertAndPassword",
            //             Usage = "Sign",
            //             Key = certificate.Export(X509ContentType.Pfx, certificatePassword)
            //         },
            //         PasswordCredential = new PasswordCredential
            //         {
            //             SecretText = certificatePassword
            //         },
            //         Proof = CalculateJwt(certificate)
            //     }).Wait();
            // } catch (AggregateException e)
            // {
            //     Log.LogWarning("    Failed to add certificate to service principal object: {Message}", e.Message);
            //     throw;
            // }
            //
            // // ========================================================================================================

            // Update the service principal object
            Log.LogDebug("    Updating service principal object for application ID \"{ApplicationId}\"", ApplicationId);
            try
            {
                GraphClient.ServicePrincipals[ObjectId].PatchAsync(new ServicePrincipal
                {
                    KeyCredentials = new List<KeyCredential>(DeepCopyKeyList(servicePrincipal.KeyCredentials))
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
                    PasswordCredentials = new List<PasswordCredential>(DeepCopyPasswordList(servicePrincipal.PasswordCredentials))
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
            } catch (AggregateException e)
            {
                Log.LogWarning("    Failed to update service principal object: {Message}", e.Message);
                // TODO remove certificates to avoid leaving the service principal in a bad state
                throw;
            }

            if (isSamlPreferred)
            {
                SetPreferredSamlSignerThumbprint(certificate.Thumbprint);
            }

            Log.LogDebug("    Certificate added successfully");
        }

        public void RemoveCertificate(string certificateName)
        {
            // Get the service principal object
            ServicePrincipal servicePrincipal = GetServicePrincipal();
            
            List<KeyCredential> keys = DeepCopyKeyList(servicePrincipal.KeyCredentials);
            IEnumerable<PasswordCredential> passwords = DeepCopyPasswordList(servicePrincipal.PasswordCredentials);
            
            // Store GUIDs of keys to delete
            Dictionary<string, bool> guidMap = new Dictionary<string, bool>();
            
            // Create a new list for keys we want to keep
            List<KeyCredential> keysToKeep = new List<KeyCredential>();
            List<PasswordCredential> passwordsToKeep = new List<PasswordCredential>();
            
            // Find certificates that match the thumbprint, and store their GUIDs
            foreach (KeyCredential keyCredential in keys)
            {
                if (keyCredential.DisplayName == certificateName)
                {
                    Log.LogDebug("    Removing key credential \"{DisplayName}\" ({KeyId})", keyCredential.DisplayName, keyCredential.KeyId);
                    
                    // Store the GUID of the key to delete
                    if (keyCredential.Usage == "Sign" && keyCredential.CustomKeyIdentifier != null)
                        guidMap[Encoding.UTF8.GetString(keyCredential.CustomKeyIdentifier)] = true;

                    continue;
                }
                
                keysToKeep.Add(keyCredential);
            }
            
            foreach (PasswordCredential passwordCredential in passwords.ToList())
            {
                if (passwordCredential.CustomKeyIdentifier != null && guidMap.ContainsKey(Encoding.UTF8.GetString(passwordCredential.CustomKeyIdentifier)))
                {
                    Log.LogDebug("    Removing password credential for certificate \"{CertificateName}\" ({Guid})", passwordCredential.CustomKeyIdentifier, passwordCredential.KeyId);
                    continue;
                }
                
                passwordsToKeep.Add(passwordCredential);
            }
            
            // Update the service principal object
            Log.LogDebug("    Updating service principal object for application ID \"{ApplicationId}\"", ApplicationId);
            try
            {
                GraphClient.ServicePrincipals[ObjectId].PatchAsync(new ServicePrincipal
                {
                    KeyCredentials = keysToKeep,
                    PasswordCredentials = passwordsToKeep
                });
            } catch (AggregateException e)
            {
                Log.LogWarning("    Failed to update service principal object with updated certificate list: {Message}", e.Message);
                throw;
            }
        }
        
        private void RemoveCertificate(string thumbprint, string replacementPreferredSamlCertificateThumbprint="")
        {
            // Get the service principal object
            ServicePrincipal servicePrincipal = GetServicePrincipal();
            
            // Don't delete existing certificates/key passwords unless they match the thumbprint
            List<KeyCredential> keys = servicePrincipal.KeyCredentials ?? new List<KeyCredential>();
            List<PasswordCredential> passwords = servicePrincipal.PasswordCredentials ?? new List<PasswordCredential>();

            // If the current preferred SAML certificate is being removed, set the preferred SAML certificate to the replacement
            string currentPreferredSamlCertificateThumbprint = GetPreferredSamlSignerThumbprint();
            if (currentPreferredSamlCertificateThumbprint == thumbprint)
            {
                Log.LogDebug("    Preferred SAML certificate ({Thumbprint}) is being removed", thumbprint);
                if (replacementPreferredSamlCertificateThumbprint == "")
                {
                    if (keys.Count == 0)
                    {
                        Log.LogWarning("    No replacement preferred SAML certificate specified, and no other certificates exist to set as preferred SAML certificate");
                        throw new Exception(
                            "No replacement preferred SAML certificate specified, and no other certificates exist to set as preferred SAML certificate");
                    }
                    // If no replacement is specified, set the preferred SAML certificate to the first certificate in the list
                    X509Certificate2 replacementCert = GetCertificateFromKeyCredential(keys[0]);
                    replacementPreferredSamlCertificateThumbprint = replacementCert.Thumbprint;
                    
                    Log.LogDebug("    No replacement preferred SAML certificate specified, setting preferred SAML certificate to \"{CertificateName}\" ({Thumbprint})", replacementCert.Subject, replacementCert.Thumbprint);
                }
                SetPreferredSamlSignerThumbprint(replacementPreferredSamlCertificateThumbprint);
            }

            // Store GUIDs of keys to delete
            Dictionary<string, bool> guidMap = new Dictionary<string, bool>();
            
            // Find certificates that match the thumbprint, and store their GUIDs
            foreach (KeyCredential keyCredential in keys)
            {
                // Get the certificate from the key credential
                X509Certificate2 certificate = GetCertificateFromKeyCredential(keyCredential);
                if (certificate == null)
                {
                    continue;
                }
                
                // If the thumbprint doesn't match, skip it
                if (certificate.Thumbprint != thumbprint)
                {
                    continue;
                }
                
                Log.LogDebug("    Removing key credential \"{DisplayName}\" ({KeyId})", keyCredential.DisplayName, keyCredential.KeyId);

                keys.Remove(keyCredential);
                
                // Store the GUID of the key to delete
                guidMap[keyCredential.KeyId.ToString()] = true;
            }
            
            // Find password credentials that match the GUIDs of the keys to delete
            foreach (PasswordCredential passwordCredential in passwords.Where(passwordCredential => guidMap.ContainsKey(passwordCredential.KeyId.ToString())))
            {
                Log.LogDebug("    Removing password credential for certificate \"{CertificateName}\" ({Guid})", passwordCredential.CustomKeyIdentifier, passwordCredential.KeyId);
                
                passwords.Remove(passwordCredential);
                
                // Delete the password
                GraphClient.ServicePrincipals[ObjectId].RemovePassword.PostAsync(new RemovePasswordPostRequestBody
                {
                    KeyId = passwordCredential.KeyId
                }).Wait();
            }

            // Update the service principal object
            Log.LogDebug("    Updating service principal object for application ID \"{ApplicationId}\"", ApplicationId);
            GraphClient.ServicePrincipals[ObjectId].PatchAsync(new ServicePrincipal
            {
                KeyCredentials = keys,
            });
        }
        
        private void ReplaceCertificateOld(string certificateName, string certificateData, string certificatePassword)
        {
            // Get the service principal object
            ServicePrincipal servicePrincipal = GetServicePrincipal();

            X509Certificate2 cert = SerializeCertificate(certificateData, certificatePassword);
            
            Log.LogDebug("Replacing certificate called \"{CertificateName}\" for application ID \"{ApplicationId}\"", certificateName, ApplicationId);
            
            // First, determine if the current certificate is the preferred SAML certificate
            string replacementPreferredSamlCertificateThumbprint = "";
            if (servicePrincipal.KeyCredentials != null)
            {
                KeyCredential currentCertificate = servicePrincipal.KeyCredentials.FirstOrDefault(c => c.DisplayName == certificateName);
                // If the current certificate is the preferred SAML certificate, set the replacement certificate as the preferred SAML certificate
                if (GetCertificateFromKeyCredential(currentCertificate).Thumbprint ==
                    GetPreferredSamlSignerThumbprint())
                {
                    X509Certificate2 replacementCertificate = SerializeCertificate(certificateData, certificatePassword);
                    replacementPreferredSamlCertificateThumbprint = replacementCertificate.Thumbprint;
                }
            }

            // Then, add the new certificate
            AddCertificate(certificateName, certificateData, certificatePassword);

            // Finally, remove the old certificate
            RemoveCertificate(cert.Thumbprint, replacementPreferredSamlCertificateThumbprint);
        }

        public void ReplaceCertificate(string certificateName, string certificateData, string certificatePassword)
        {
            Log.LogDebug("Replacing certificate called \"{CertificateName}\" for application ID \"{ApplicationId}\"", certificateName, ApplicationId);
            
            // We only need to add the certificate since the AddCertificate method will remove the old certificate automatically
            AddCertificate(certificateName, certificateData, certificatePassword);
        }
        
        public bool CertificateExists(string certificateName)
        {
            ServicePrincipal servicePrincipal = GetServicePrincipal();
            
            return servicePrincipal.KeyCredentials != null && servicePrincipal.KeyCredentials.Any(c => c.DisplayName == certificateName);
        }
        
        private void SetPreferredSamlSignerThumbprint(string preferredSamlSignerThumbprint)
        {
            // Create a service principal object with the updated key and password credentials
            ServicePrincipal requestBody = new ServicePrincipal
            {
                PreferredTokenSigningKeyThumbprint = preferredSamlSignerThumbprint,
            };
            
            // Update the service principal object
            Log.LogDebug("    Updating service principal object for application ID \"{ApplicationId}\"", ApplicationId);

            try
            {
                GraphClient.ServicePrincipals[ObjectId].PatchAsync(requestBody).Wait();
            } catch (AggregateException e)
            {
                Log.LogWarning("    Failed to update service principal object with updated preferred SAML signer thumbprint: {Message}", e.Message);
                throw;
            }
        }
        
        private string GetPreferredSamlSignerThumbprint()
        {
            ServicePrincipal sp = GetServicePrincipal();

            return sp.PreferredTokenSigningKeyThumbprint;
        }
    }
}
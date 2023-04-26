using System;
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace AzureAppRegistration.Jobs
{
    public class Management : AzureApplicationJob<Management>, IManagementJobExtension
    {
        ILogger _logger = LogHandler.GetClassLogger<Management>();
        
        public JobResult ProcessJob(ManagementJobConfiguration config)
        {
            _logger.LogDebug("Beginning App Gateway Management Job");
            
            Initialize(config.CertificateStoreDetails);
            
            JobResult result = new JobResult
            {
                Result = OrchestratorJobStatusJobResult.Failure,
                JobHistoryId = config.JobHistoryId
            };

            try
            {
                switch (config.OperationType)
                {
                    case CertStoreOperationType.Add:
                        _logger.LogDebug("Adding certificate to App Gateway");
                        
                        PerformAddition(config);
                        
                        _logger.LogDebug("Add operation complete.");
                        
                        result.Result = OrchestratorJobStatusJobResult.Success;
                        break;
                    case CertStoreOperationType.Remove:
                        _logger.LogDebug("Removing certificate from App Gateway");

                        ApplicationClient.RemoveApplicationCertificate(config.JobCertificate.Alias);
                        
                        _logger.LogDebug("Remove operation complete");
                        result.Result = OrchestratorJobStatusJobResult.Success;
                        break;
                    default:
                        _logger.LogDebug("Invalid management operation type: {OT}", config.OperationType);
                        throw new ArgumentOutOfRangeException();
                }
            } catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job:\n {Message}", ex.Message);
                result.FailureMessage = ex.Message;
            }

            return result;
        }
        
        private void PerformAddition(ManagementJobConfiguration config)
        {
            // Ensure that the certificate is in PKCS#12 format.
            if (string.IsNullOrWhiteSpace(config.JobCertificate.PrivateKeyPassword))
            {
                throw new Exception("Certificate must be in PKCS#12 format.");
            }
            // Ensure that an alias is provided.
            if (string.IsNullOrWhiteSpace(config.JobCertificate.Alias))
            {
                throw new Exception("Certificate alias is required.");
            }
            
            if (ApplicationClient.ApplicationCertificateExists(config.JobCertificate.Alias) && !config.Overwrite)
            {
                _logger.LogDebug("Certificate with alias \"{Alias}\" already exists in App Gateway, and job was not configured to overwrite", config.JobCertificate.Alias);
                throw new Exception($"Certificate with alias \"{config.JobCertificate.Alias}\" already exists in App Gateway, and job was not configured to overwrite");
            }
            
            string isPreferredSamlSignerString = config.JobProperties["IsPreferredSamlSigner"]?.ToString();
            bool isPreferredSamlSigner = false;
            if (!string.IsNullOrWhiteSpace(config.JobProperties["IsPreferredSamlSigner"]?.ToString()))
            {
                _logger.LogDebug("Enrollment field 'SetPreferredSamlSigner' is set to \"{SetPreferredSamlSigner}\". Also updating HTTP Listener", config.JobProperties["SetPreferredSamlSigner"].ToString());
            }
            
            if (config.Overwrite)
            {
                _logger.LogDebug("Overwrite is enabled, replacing certificate in gateway called \"{0}\"", config.JobCertificate.Alias);
                ApplicationClient.ReplaceApplicationCertificate(config.JobCertificate.Alias, config.JobCertificate.Contents, config.JobCertificate.PrivateKeyPassword);
            }
            else
            {
                _logger.LogDebug("Adding certificate to App Gateway");
                ApplicationClient.AddApplicationCertificate(config.JobCertificate.Alias, config.JobCertificate.Contents, config.JobCertificate.PrivateKeyPassword, isPreferredSamlSigner);
            }
        }
    }
}
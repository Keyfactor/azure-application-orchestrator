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
            _logger.LogDebug("Beginning {Type} Management Job", Client.TypeString);
            
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
                        _logger.LogDebug("Adding certificate to {Type}", Client.TypeString);
                        
                        PerformAddition(config);
                        
                        _logger.LogDebug("Add operation complete");
                        
                        result.Result = OrchestratorJobStatusJobResult.Success;
                        break;
                    case CertStoreOperationType.Remove:
                        _logger.LogDebug("Removing certificate from {Type}", Client.TypeString);

                        Client.RemoveCertificate(config.JobCertificate.Alias);
                        
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
            // Ensure that the password is provided.
            if (string.IsNullOrWhiteSpace(config.JobCertificate.PrivateKeyPassword))
            {
                throw new Exception("Private key password must be provided.");
            }
            // Ensure that an alias is provided.
            if (string.IsNullOrWhiteSpace(config.JobCertificate.Alias))
            {
                throw new Exception("Certificate alias is required.");
            }
            
            if (Client.CertificateExists(config.JobCertificate.Alias) && !config.Overwrite)
            {
                _logger.LogDebug("Certificate with alias \"{Alias}\" already exists in {Type}, and job was not configured to overwrite", config.JobCertificate.Alias, Client.TypeString);
                throw new Exception($"Certificate with alias \"{config.JobCertificate.Alias}\" already exists in {Client.TypeString}, and job was not configured to overwrite");
            }

            if (config.Overwrite)
            {
                _logger.LogDebug("Overwrite is enabled, replacing certificate in {Type} called \"{Alias}\"", Client.TypeString, config.JobCertificate.Alias);
                Client.ReplaceCertificate(config.JobCertificate.Alias, config.JobCertificate.Contents, config.JobCertificate.PrivateKeyPassword);
            }
            else
            {
                _logger.LogDebug("Adding certificate to {Type}", Client.TypeString);
                Client.AddCertificate(config.JobCertificate.Alias, config.JobCertificate.Contents, config.JobCertificate.PrivateKeyPassword);
            }
        }
    }
}
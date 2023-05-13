using System.Collections.Generic;
using Keyfactor.Orchestrators.Extensions;

namespace AzureAppRegistration.Client
{
    public interface IAzureGraphClient
    {
        public string TypeString { get; }
        public void AddCertificate(string certificateName, string certificateData, string certificatePassword);
        public void ReplaceCertificate(string certificateName, string certificateData, string certificatePassword);
        public void RemoveCertificate(string certificateName);
        public bool CertificateExists(string certificateName);
        public IEnumerable<CurrentInventoryItem> GetInventory();
    }
}
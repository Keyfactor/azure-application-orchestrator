# azure-application-certificate-orchestrator
Keyfactor orchestrator extension to inventory and manage Azure Enterprise Application/Service Principal certificates

## Observations of the Microsoft Graph API
* The Microsoft Graph API does not return the certificate if the usage is "Sign" and the key type is "X509CertAndPassword"
* Using the `PATCH` method with the `/servicePrincipal` and `/application` endpoints to to add new certificates with "Sign" usage and "X509CertAndPassword" key type does not work
* Using the `/servicePrincipal/<id>/addKey` method requires the private key of another certificate already uploaded to Azure
# Overview

The Azure Enterprise Application/Service Principal certificate operations are implemented by the `AzureSP` store type, and supports the management of a single certificate for use in SSO/SAML assertion signing. The Management Add operation is only supported with the certificate replacement option, since adding a new certificate will replace the existing certificate. The Add operation will also set newly added certificates as the active certificate for SSO/SAML usage. The Management Remove operation removes the certificate from the Enterprise Application/Service Principal, which is the same as removing the SSO/SAML signing certificate. The Discovery operation discovers all Enterprise Applications/Service Principals in the tenant.

# Global Store Type Section

> **WARNING** AzureSP "Azure Enterprise Application (Service Principal)" is **Depricated**. Please use **AzureSP2** "Azure Enterprise Application 2 (Service Principal)" instead. 

# Requirements

## Enterprise Application (Service Principal)

### Service Principal Certificates

Service Principal certificates are typically used for SAML Token signing. Service Principals are created from Enterprise Applications, and will mostly be configured with a variation of Microsoft's [SAML-based single sign-on](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/add-application-portal) documentation. For more information on the mechanics of the Service Principal certificate management capabilities of this extension, please see the [mechanics](#extension-mechanics) section.

# Discovery Job Configuration

The Discovery operation discovers all Azure Enterprise Applications that the Service Principal has access to. The discovered Enterprise Applications (specifically, their Application IDs) are reported back to Command and can be easily added as certificate stores from the Locations tab.

The Discovery operation uses the "Directories to search" field, and accepts input in one of the following formats:
- `*` - If the asterisk symbol `*` is used, the extension will search for all Azure Enterprise Applications that the Service Principal has access to, but only in the tenant that the discovery job was configured for as specified by the "Client Machine" field in the certificate store configuration.
- `<tenant-id>,<tenant-id>,...` - If a comma-separated list of tenant IDs is used, the extension will search for all Azure Enterprise Applications available in each tenant specified in the list. The tenant IDs should be the GUIDs associated with each tenant, and it's the user's responsibility to ensure that the service principal has access to the specified tenants.

> The Discovery Job only supports Client Secret authentication.

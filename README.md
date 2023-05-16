# Azure Application and Enterprise Application Orchestrator

The Azure Application and Enterprise Application Orchestrator extension acts as a proxy between Keyfactor and Azure that allows Keyfactor to manage Application (Auth) and Enterprise Application (SSO/SAML) certificates.

#### Integration status: Prototype - Demonstration quality. Not for use in customer environments.

## About the Keyfactor Universal Orchestrator Extension

This repository contains a Universal Orchestrator Extension which is a plugin to the Keyfactor Universal Orchestrator. Within the Keyfactor Platform, Orchestrators are used to manage “certificate stores” &mdash; collections of certificates and roots of trust that are found within and used by various applications.

The Universal Orchestrator is part of the Keyfactor software distribution and is available via the Keyfactor customer portal. For general instructions on installing Extensions, see the “Keyfactor Command Orchestrator Installation and Configuration Guide” section of the Keyfactor documentation. For configuration details of this specific Extension see below in this readme.

The Universal Orchestrator is the successor to the Windows Orchestrator. This Orchestrator Extension plugin only works with the Universal Orchestrator and does not work with the Windows Orchestrator.



## Support for Azure Application and Enterprise Application Orchestrator

Azure Application and Enterprise Application Orchestrator is supported by Keyfactor for Keyfactor customers. If you have a support issue, please open a support ticket with your Keyfactor representative.

###### To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.



---




## Platform Specific Notes

The Keyfactor Universal Orchestrator may be installed on either Windows or Linux based platforms. The certificate operations supported by a capability may vary based what platform the capability is installed on. The table below indicates what capabilities are supported based on which platform the encompassing Universal Orchestrator is running.
| Operation | Win | Linux |
|-----|-----|------|
|Supports Management Add|&check; |&check; |
|Supports Management Remove|&check; |&check; |
|Supports Create Store|  |  |
|Supports Discovery|&check; |&check; |
|Supports Renrollment|  |  |
|Supports Inventory|&check; |&check; |





---


## Overview
The Azure Application and Enterprise Application Orchestrator extension remotely manages both 
Azure App Registration (Application; Auth) certificates and Enterprise Application (Service Principal; SSO/SAML) certificates.
The extension implements the Inventory, Management Add, Management Remove, and Discovery job types.

Certificates used for client authentication by Applications (configured in App Registrations)
are represented by the `AzureApp` store type, and certificates used for SSO/SAML assertion signing are
represented by the `AzureSP` store type. Both store types are managed by the same extension. The extension is 
configured with a single Azure Service Principal that is used to authenticate to the Azure Graph API.

### Azure App Registration Certificate Operations
The Azure App Registration/Application certificate operations are implemented by the `AzureApp` store type.
The Management Add operation can add as many certificates as needed to an App Registration.
These certificates can be used for client authentication, assertion encryption, or other future
implementations. The Management Remove operation can remove certificates from an App Registration.
The Discovery operation discovers all App Registrations in the tenant.

### Azure Enterprise Application Certificate Operations
The Azure Enterprise Application/Service Principal certificate operations are implemented by the `AzureSP` store type,
and supports the management of a single certificate for use in SSO/SAML assertion signing.
The Management Add operation is only supported with the certificate replacement option,
since adding a new certificate will replace the existing certificate. The Add operation will
also set newly added certificates as the active certificate for SSO/SAML usage. The Management
Remove operation removes the certificate from the Enterprise Application/Service Principal,
which is the same as removing the SSO/SAML signing certificate. The Discovery operation
discovers all Enterprise Applications/Service Principals in the tenant.

## Azure Configuration
The Azure Application and Enterprise Application Orchestrator extension requires an Azure Service Principal
to authenticate to the Azure Graph API. The Service Principal must have the **_Application_** `Application.ReadWrite.All` permission
under "API Permissions" (_not_ Delegated), and the permission must have Admin Consent for the tenant. For more 
information on Admin Consent for App-only access (also called "Application Permissions"), see the
[primer on application-only access](https://learn.microsoft.com/en-us/azure/active-directory/develop/app-only-access-primer).

The Azure Application and Enterprise Application Orchestrator extension also requires a Client Secret to authenticate
to the Azure Graph API.

## Keyfactor Configuration
Follow the Keyfactor Orchestrator configuration guide to install the Azure Application Gateway Orchestrator extension.

This guide uses the `kfutil` Keyfactor command line tool that offers convenient and powerful
command line access to the Keyfactor platform. Before proceeding, ensure that `kfutil` is installed and configured
by following the instructions here: [https://github.com/Keyfactor/kfutil](https://github.com/Keyfactor/kfutil)

Configuration is done in two steps:
1. Create a new Keyfactor Certificate Store Type
2. Create a new Keyfactor Certificate Store

### Keyfactor Certificate Store Type Configuration
Keyfactor Certificate Store Types are used to define and configure the platforms that store and use certificates that will be managed
by Keyfactor Orchestrators. First, create the Azure Service Principal (SSO/SAML) store type. Run the following commands with `kfutil`:
```bash
cat <<EOF > ./AzureSP.json
{
    "Name": "Azure Service Principal (SSO/SAML)",
    "ShortName": "AzureSP",
    "Capability": "AzureSP",
    "LocalStore": false,
    "SupportedOperations": {
        "Add": true,
        "Create": false,
        "Discovery": true,
        "Enrollment": false,
        "Remove": true
    },
    "Properties": [
        {
            "StoreTypeId": 280,
            "Name": "ServerUsername",
            "DisplayName": "Server Username",
            "Type": "Secret",
            "DependsOn": null,
            "DefaultValue": null,
            "Required": false
        },
        {
            "StoreTypeId": 280,
            "Name": "ServerPassword",
            "DisplayName": "Server Password",
            "Type": "Secret",
            "DependsOn": null,
            "DefaultValue": null,
            "Required": false
        },
        {
            "StoreTypeId": 280,
            "Name": "ServerUseSsl",
            "DisplayName": "Use SSL",
            "Type": "Bool",
            "DependsOn": null,
            "DefaultValue": "true",
            "Required": true
        }
    ],
    "EntryParameters": [],
    "PasswordOptions": {
        "EntrySupported": false,
        "StoreRequired": false,
        "Style": "Default"
    },
    "PrivateKeyAllowed": "Required",
    "JobProperties": [],
    "ServerRequired": true,
    "PowerShell": false,
    "BlueprintAllowed": false,
    "CustomAliasAllowed": "Required"
}
EOF
 kfutil store-types create --from-file ./AzureSP.json
```

Then, create the Azure App Registration (Application) store type. Run the following commands with `kfutil`:
```bash
cat <<EOF > ./AzureApp.json
{
    "Name": "Azure Application (Auth)",
    "ShortName": "AzureApp",
    "Capability": "AzureApp",
    "LocalStore": false,
    "SupportedOperations": {
        "Add": true,
        "Create": false,
        "Discovery": true,
        "Enrollment": false,
        "Remove": true
    },
    "Properties": [
        {
            "StoreTypeId": 279,
            "Name": "ServerUsername",
            "DisplayName": "Server Username",
            "Type": "Secret",
            "DependsOn": null,
            "DefaultValue": null,
            "Required": false
        },
        {
            "StoreTypeId": 279,
            "Name": "ServerPassword",
            "DisplayName": "Server Password",
            "Type": "Secret",
            "DependsOn": null,
            "DefaultValue": null,
            "Required": false
        },
        {
            "StoreTypeId": 279,
            "Name": "ServerUseSsl",
            "DisplayName": "Use SSL",
            "Type": "Bool",
            "DependsOn": null,
            "DefaultValue": "true",
            "Required": true
        }
    ],
    "EntryParameters": [],
    "PasswordOptions": {
        "EntrySupported": false,
        "StoreRequired": false,
        "Style": "Default"
    },
    "PrivateKeyAllowed": "Forbidden",
    "JobProperties": [],
    "ServerRequired": true,
    "PowerShell": false,
    "BlueprintAllowed": false,
    "CustomAliasAllowed": "Required"
}
EOF
kfutil store-types create --from-file ./AzureApp.json
```

### Keyfactor Store and Discovery Job Configuration
To create a new certificate store in Keyfactor Command, select the _Locations_ drop down, select _Certificate Stores_, and click the _Add_ button.
To schedule a discovery job, select the _Locations_ drop down, select _Certificate Stores_, click on the _Discovery_ button, and click the _Schedule_ button. Since
Azure App Registrations (Applications) and Azure Enterprise Applications (Service Principals) are linked by the same Application ID, the configuration for both `AzureApp` and `AzureSP`
is the same. The following table describes the configuration options for the Azure App Registration (Application) and Azure Enterprise Application (Service Principal) store types:

| Parameter       | Value                                                               | Description                                                                                                   |
|-----------------|---------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------|
| Category        | 'Azure Application (Auth)' or `Azure Service Principal (SSO/SAML)`  | Name of the store type                                                                                        |
| Client Machine  | Azure Tenant ID                                                     | The Azure Tenant ID                                                                                           |
| Store Path      | Application Gateway resource ID                                     | Azure Application ID of the App Registration (Application) or Enterprise Application (Enterprise Application) |
| Server Username | Application ID                                                      | Application ID of the service principal created to authenticate to the Graph API                              |
| Server Password | Client Secret                                                       | Secret value of the service principal created to authenticate to the Graph API                                |

For the discovery job, populate the _Directories to search_ with any value. The extension will discover all Application Gateways accessible by the Azure Service Principal.


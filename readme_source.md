## Overview
The Azure App Registration and Enterprise Application Orchestrator extension remotely manages both Azure [App Registration/Application](https://learn.microsoft.com/en-us/entra/identity-platform/certificate-credentials) certificates and [Enterprise Application/Service Principal](https://docs.microsoft.com/en-us/azure/active-directory/develop/enterprise-apps-certificate-credentials) certificates. Application certificates are typically public key only and used for client certificate authentication, while Service Principal certificates are commonly used for [SAML Token signing](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/tutorial-manage-certificates-for-federated-single-sign-on). The extension implements the Inventory, Management Add, Management Remove, and Discovery job types.

Certificates used for client authentication by Applications (configured in App Registrations) are represented by the [`AzureApp` store type](docs/azureapp.md), and certificates used for SSO/SAML assertion signing are represented by the [`AzureSP` store type](docs/azuresp.md). Both store types are managed by the same extension. The extension is configured with a single Azure Service Principal that is used to authenticate to the Azure Graph API. The Azure App Registration and Enterprise Application Orchestrator extension manages certificates for Azure App Registrations (Applications) and Enterprise Applications (Service Principals) differently. Namely, Application certificates are public key only, and Service Principal certificates are typically used for SAML Token signing.

## Installation
Before installing the Azure App Registration and Enterprise Application Orchestrator extension, it's recommended to install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.

1. Follow the Requirements section to configure a Service Account and grant the necessary API permissions.

    * [Azure App Registration/Application](docs/azureapp.md#requirements)
    * [Azure Enterprise Application/Service Principal](docs/azuresp.md#requirements)

2. Create Certificate Store Types for the Azure App Registration and Enterprise Application Orchestrator extension. 

    * **Using kfutil**:

        ```shell
        kfutil store-types create AzureApp
        kfutil store-types create AzureSP
        ```

    * **Manually**:

        * [Azure App Registration/Application](docs/azureapp.md#certificate-store-type-configuration)
        * [Azure Enterprise Application/Service Principal](docs/azuresp.md#certificate-store-type-configuration)

3. Install the Azure App Registration and Enterprise Application Orchestrator extension.
    
    * **Using kfutil**: On the server that that hosts the Universal Orchestrator, run the following command:

        ```shell
        # Windows Server
        kfutil orchestrator extension -e azure-application-orchestrator@latest --out "C:\Program Files\Keyfactor\Keyfactor Orchestrator\extensions"

        # Linux
        kfutil orchestrator extension -e azure-application-orchestrator@latest --out "/opt/keyfactor/orchestrator/extensions"
        ```

    * **Manually**: Follow the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions) to install the latest [Azure App Registration and Enterprise Application Orchestrator extension](https://github.com/Keyfactor/azure-application-orchestrator/releases/latest).

4. Create new certificate stores in Keyfactor Command for the Azure App Registration and Enterprise Application Orchestrator extension.

    * [Azure App Registration/Application](docs/azureapp.md#certificate-store-configuration)
    * [Azure Enterprise Application/Service Principal](docs/azuresp.md#certificate-store-configuration)


<h1 align="center" style="border-bottom: none">
    Azure App Registration and Enterprise Application Universal Orchestrator Extension
</h1>

<p align="center">
  <!-- Badges -->
<img src="https://img.shields.io/badge/integration_status-production-3D1973?style=flat-square" alt="Integration Status: production" />
<a href="https://github.com/Keyfactor/azure-application-orchestrator/releases"><img src="https://img.shields.io/github/v/release/Keyfactor/azure-application-orchestrator?style=flat-square" alt="Release" /></a>
<img src="https://img.shields.io/github/issues/Keyfactor/azure-application-orchestrator?style=flat-square" alt="Issues" />
<img src="https://img.shields.io/github/downloads/Keyfactor/azure-application-orchestrator/total?style=flat-square&label=downloads&color=28B905" alt="GitHub Downloads (all assets, all releases)" />
</p>

<p align="center">
  <!-- TOC -->
  <a href="#support">
    <b>Support</b>
  </a>
  ·
  <a href="#installation">
    <b>Installation</b>
  </a>
  ·
  <a href="#license">
    <b>License</b>
  </a>
  ·
  <a href="https://github.com/orgs/Keyfactor/repositories?q=orchestrator">
    <b>Related Integrations</b>
  </a>
</p>


## Overview

The Azure App Registration and Enterprise Application Orchestrator extension remotely manages both Azure [App Registration/Application](https://learn.microsoft.com/en-us/entra/identity-platform/certificate-credentials) certificates and [Enterprise Application/Service Principal](https://docs.microsoft.com/en-us/azure/active-directory/develop/enterprise-apps-certificate-credentials) certificates. Application certificates are typically public key only and used for client certificate authentication, while Service Principal certificates are commonly used for [SAML Assertion signing](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/tutorial-manage-certificates-for-federated-single-sign-on). The extension implements the Inventory, Management Add, Management Remove, and Discovery job types.

Certificates used for client authentication by Applications (configured in App Registrations) are represented by the [`AzureApp` store type](docs/azureapp.md), and certificates used for SSO/SAML assertion signing are represented by the [`AzureSP` store type](docs/azuresp.md). Both store types are managed by the same extension. The extension is configured with a single Azure Service Principal that is used to authenticate to the [Microsoft Graph API](https://learn.microsoft.com/en-us/graph/use-the-api). The Azure App Registration and Enterprise Application Orchestrator extension manages certificates for Azure App Registrations (Applications) and Enterprise Applications (Service Principals) differently.

## Compatibility

This integration is compatible with Keyfactor Universal Orchestrator version 10.4 and later.

## Support
The Azure App Registration and Enterprise Application Universal Orchestrator extension is supported by Keyfactor for Keyfactor customers. If you have a support issue, please open a support ticket with your Keyfactor representative. If you have a support issue, please open a support ticket via the Keyfactor Support Portal at https://support.keyfactor.com. 
 
> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

## Installation

Before installing the Azure App Registration and Enterprise Application Universal Orchestrator extension, we recommend that you install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.

1. **Create Certificate Store Types in Keyfactor Command**  
The Azure App Registration and Enterprise Application Universal Orchestrator extension implements 2 Certificate Store Types. Depending on your use case, you may elect to install one, or all of these Certificate Store Types.

    <details><summary>Azure App Registration (Application)</summary>


    > More information on the Azure App Registration (Application) Certificate Store Type can be found [here](docs/azureapp.md).

    * **Create AzureApp using kfutil**:

        ```shell
        # Azure App Registration (Application)
        kfutil store-types create AzureApp
        ```

    * **Create AzureApp manually in the Command UI**:
        
        Refer to the [Azure App Registration (Application)](docs/azureapp.md#certificate-store-type-configuration) creation docs.
    </details>

    <details><summary>Azure Enterprise Application (Service Principal)</summary>


    > More information on the Azure Enterprise Application (Service Principal) Certificate Store Type can be found [here](docs/azuresp.md).

    * **Create AzureSP using kfutil**:

        ```shell
        # Azure Enterprise Application (Service Principal)
        kfutil store-types create AzureSP
        ```

    * **Create AzureSP manually in the Command UI**:
        
        Refer to the [Azure Enterprise Application (Service Principal)](docs/azuresp.md#certificate-store-type-configuration) creation docs.
    </details>

2. **Download the latest Azure App Registration and Enterprise Application Universal Orchestrator extension from GitHub.** 

    On the [Azure App Registration and Enterprise Application Universal Orchestrator extension GitHub version page](https://github.com/Keyfactor/azure-application-orchestrator/releases/latest), click the `azure-application-orchestrator` asset to download the zip archive. Unzip the archive containing extension assemblies to a known location.

3. **Locate the Universal Orchestrator extensions directory.**

    * **Default on Windows** - `C:\Program Files\Keyfactor\Keyfactor Orchestrator\extensions`
    * **Default on Linux** - `/opt/keyfactor/orchestrator/extensions`
    
4. **Create a new directory for the Azure App Registration and Enterprise Application Universal Orchestrator extension inside the extensions directory.**
        
    Create a new directory called `azure-application-orchestrator`.
    > The directory name does not need to match any names used elsewhere; it just has to be unique within the extensions directory.

5. **Copy the contents of the downloaded and unzipped assemblies from __step 2__ to the `azure-application-orchestrator` directory.**

6. **Restart the Universal Orchestrator service.**

    Refer to [Starting/Restarting the Universal Orchestrator service](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/StarttheService.htm).



> The above installation steps can be supplimented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions).

## Configuration and Usage

The Azure App Registration and Enterprise Application Universal Orchestrator extension implements 2 Certificate Store Types, each of which implements different functionality. Refer to the individual instructions below for each Certificate Store Type that you deemed necessary for your use case from the installation section.

<details><summary>Azure App Registration (Application)</summary>

1. Refer to the [requirements section](docs/azureapp.md#requirements) to ensure all prerequisites are met before using the Azure App Registration (Application) Certificate Store Type.
2. Create new [Azure App Registration (Application)](docs/azureapp.md#certificate-store-configuration) Certificate Stores in Keyfactor Command.
</details>

<details><summary>Azure Enterprise Application (Service Principal)</summary>

1. Refer to the [requirements section](docs/azuresp.md#requirements) to ensure all prerequisites are met before using the Azure Enterprise Application (Service Principal) Certificate Store Type.
2. Create new [Azure Enterprise Application (Service Principal)](docs/azuresp.md#certificate-store-configuration) Certificate Stores in Keyfactor Command.
</details>


## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Universal Orchestrator extensions](https://github.com/orgs/Keyfactor/repositories?q=orchestrator).
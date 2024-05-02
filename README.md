
# Azure App Registration and Enterprise Application Orchestrator

The Azure App Registration and Enterprise Application Orchestrator extension remotely manages both Azure App Registration/Application certificates and Enterprise Application/Service Principal certificates.

#### Integration status: Production - Ready for use in production environments.

## About the Keyfactor Universal Orchestrator Extension

This repository contains a Universal Orchestrator Extension which is a plugin to the Keyfactor Universal Orchestrator. Within the Keyfactor Platform, Orchestrators are used to manage “certificate stores” &mdash; collections of certificates and roots of trust that are found within and used by various applications.

The Universal Orchestrator is part of the Keyfactor software distribution and is available via the Keyfactor customer portal. For general instructions on installing Extensions, see the “Keyfactor Command Orchestrator Installation and Configuration Guide” section of the Keyfactor documentation. For configuration details of this specific Extension see below in this readme.

The Universal Orchestrator is the successor to the Windows Orchestrator. This Orchestrator Extension plugin only works with the Universal Orchestrator and does not work with the Windows Orchestrator.

## Support for Azure App Registration and Enterprise Application Orchestrator

Azure App Registration and Enterprise Application Orchestrator is supported by Keyfactor for Keyfactor customers. If you have a support issue, please open a support ticket via the Keyfactor Support Portal at https://support.keyfactor.com

###### To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

---


---



## Keyfactor Version Supported

The minimum version of the Keyfactor Universal Orchestrator Framework needed to run this version of the extension is 10.4
## Platform Specific Notes

The Keyfactor Universal Orchestrator may be installed on either Windows or Linux based platforms. The certificate operations supported by a capability may vary based what platform the capability is installed on. The table below indicates what capabilities are supported based on which platform the encompassing Universal Orchestrator is running.
| Operation | Win | Linux |
|-----|-----|------|
|Supports Management Add|&check; |&check; |
|Supports Management Remove|&check; |&check; |
|Supports Create Store|  |  |
|Supports Discovery|&check; |&check; |
|Supports Reenrollment|  |  |
|Supports Inventory|&check; |&check; |





---


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

## Installation
Before installing the Azure App Registration and Enterprise Application Universal Orchestrator extension, it's recommended to install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.

The Azure App Registration and Enterprise Application Universal Orchestrator extension implements 2 Certificate Store Types. Depending on your use case, you may elect to install one, or all of these Certificate Store Types. An overview for each type is linked below:
* [Azure App Registration (Application)](docs/azureapp.md)
* [Azure Enterprise Application (Service Principal)](docs/azuresp.md)

<details><summary>Azure App Registration (Application)</summary>


1. Follow the [requirements section](docs/azureapp.md#requirements) to configure a Service Account and grant necessary API permissions.

    <details><summary>Requirements</summary>

    ### Azure Service Principal (Graph API Authentication)

    The Azure App Registration and Enterprise Application Orchestrator extension uses an [Azure Service Principal](https://learn.microsoft.com/en-us/entra/identity-platform/app-objects-and-service-principals?tabs=browser) for authentication. Follow [Microsoft's documentation](https://learn.microsoft.com/en-us/entra/identity-platform/howto-create-service-principal-portal) to create a service principal. Currently, Client Secret authentication is supported. The Service Principal must have the following API Permission:
    - **_Microsoft Graph Application Permissions_**:
      - `Application.ReadWrite.All` (_not_ Delegated; Admin Consent) - Allows the app to create, read, update and delete applications and service principals without a signed-in user.

    > For more information on Admin Consent for App-only access (also called "Application Permissions"), see the [primer on application-only access](https://learn.microsoft.com/en-us/azure/active-directory/develop/app-only-access-primer).

    Alternatively, the Service Principal can be granted the `Application.ReadWrite.OwnedBy` permission if the Service Principal is only intended to manage its own App Registration/Application.

    #### Client Certificate or Client Secret

    Beginning in version 3.0.0, the Azure App Registration and Enterprise Application Orchestrator extension supports both [client certificate authentication](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-1-add-a-certificate) and [client secret](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-2-add-a-client-secret) authentication.

    * **Client Secret** - Follow [Microsoft's documentation](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-2-add-a-client-secret) to create a Client Secret. This secret will be used as the **Server Password** field in the [Certificate Store Configuration](#certificate-store-configuration) section.
    * **Client Certificate** - Create a client certificate key pair with the Client Authentication extended key usage. The client certificate will be used in the ClientCertificate field in the [Certificate Store Configuration](#certificate-store-configuration) section. If you have access to Keyfactor Command, the instructions in this section walk you through enrolling a certificate and ensuring that it's in the correct format. Once enrolled, follow [Microsoft's documentation](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-1-add-a-certificate) to add the _public key_ certificate (no private key) to the service principal used for authentication.

        The certificate can be in either of the following formats:
        * Base64-encoded PKCS#12 (PFX) with a matching private key.
        * Base64-encoded PEM-encoded certificate _and_ PEM-encoded PKCS8 private key. Make sure that the certificate and private key are separated with a newline. The order doesn't matter - the extension will determine which is which.

        If the private key is encrypted, the encryption password will replace the **Server Password** field in the [Certificate Store Configuration](#certificate-store-configuration) section.

    > **Creating and Formatting a Client Certificate using Keyfactor Command**
    >
    > To get started quickly, you can follow the instructions below to create and properly format a client certificate to authenticate to the Microsoft Graph API.
    >
    > 1. In Keyfactor Command, hover over **Enrollment** and select **PFX Enrollment**.
    > 2. Select a **Template** that supports Client Authentication as an extended key usage.
    > 3. Populate the certificate subject as appropriate for the Template. It may be sufficient to only populate the Common Name, but consult your IT policy to ensure that this certificate is compliant.
    > 4. At the bottom of the page, uncheck the box for **Include Chain**, and select either **PFX** or **PEM** as the certificate Format.
    > 5. Make a note of the password on the next page - it won't be shown again.
    > 6. Prepare the certificate and private key for Azure and the Orchestrator extension:
    >     * If you downloaded the certificate in PEM format, use the commands below:
    >
    >        ```shell
    >        # Verify that the certificate downloaded from Command contains the certificate and private key. They should be in the same file
    >        cat <your_certificate.pem>
    >
    >        # Separate the certificate from the private key
    >        openssl x509 -in <your_certificate.pem> -out pubkeycert.pem
    >
    >        # Base64 encode the certificate and private key
    >        cat <your_certificate.pem> | base64 > clientcertkeypair.pem.base64
    >        ```
    >
    >    * If you downloaded the certificate in PFX format, use the commands below:
    >
    >        ```shell
    >        # Export the certificate from the PFX file
    >        openssl pkcs12 -in <your_certificate.pfx> -clcerts -nokeys -out pubkeycert.pem
    >
    >        # Base64 encode the PFX file
    >        cat <your_certificate.pfx> | base64 > clientcert.pfx.base64
    >        ```
    > 7. Follow [Microsoft's documentation](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-1-add-a-certificate) to add the public key certificate to the service principal used for authentication.
    >
    > You will use `clientcert.[pem|pfx].base64` as the **ClientCertificate** field in the [Certificate Store Configuration](#certificate-store-configuration) section.

    ### Azure App Registration (Application)

    #### Application Certificates

    Application certificates are used for client authentication and are typically public key only. No additional configuration in Azure is necessary to manage Application certificates since all App Registrations can contain any number of [Certificates and Secrets](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app#add-credentials). Unless the Discovery job is used, you should collect the Application IDs for each App Registration that contains certificates to be managed.



    </details>

2. Create Certificate Store Types for the Azure App Registration and Enterprise Application Orchestrator extension. 

    * **Using kfutil**:

        ```shell
        # Azure App Registration (Application)
        kfutil store-types create AzureApp
        ```

    * **Manually**:
        * [Azure App Registration (Application)](docs/azureapp.md#certificate-store-type-configuration)

3. Install the Azure App Registration and Enterprise Application Universal Orchestrator extension.
    
    * **Using kfutil**: On the server that that hosts the Universal Orchestrator, run the following command:

        ```shell
        # Windows Server
        kfutil orchestrator extension -e azure-application-orchestrator@latest --out "C:\Program Files\Keyfactor\Keyfactor Orchestrator\extensions"

        # Linux
        kfutil orchestrator extension -e azure-application-orchestrator@latest --out "/opt/keyfactor/orchestrator/extensions"
        ```

    * **Manually**: Follow the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions) to install the latest [Azure App Registration and Enterprise Application Universal Orchestrator extension](https://github.com/Keyfactor/azure-application-orchestrator/releases/latest).

4. Create new certificate stores in Keyfactor Command for the Sample Universal Orchestrator extension.
    * [Azure App Registration (Application)](docs/azureapp.md#certificate-store-configuration)
</details>

<details><summary>Azure Enterprise Application (Service Principal)</summary>


1. Follow the [requirements section](docs/azuresp.md#requirements) to configure a Service Account and grant necessary API permissions.

    <details><summary>Requirements</summary>

    ### Azure Service Principal (Graph API Authentication)

    The Azure App Registration and Enterprise Application Orchestrator extension uses an [Azure Service Principal](https://learn.microsoft.com/en-us/entra/identity-platform/app-objects-and-service-principals?tabs=browser) for authentication. Follow [Microsoft's documentation](https://learn.microsoft.com/en-us/entra/identity-platform/howto-create-service-principal-portal) to create a service principal. Currently, Client Secret authentication is supported. The Service Principal must have the following API Permission:
    - **_Microsoft Graph Application Permissions_**:
      - `Application.ReadWrite.All` (_not_ Delegated; Admin Consent) - Allows the app to create, read, update and delete applications and service principals without a signed-in user.

    > For more information on Admin Consent for App-only access (also called "Application Permissions"), see the [primer on application-only access](https://learn.microsoft.com/en-us/azure/active-directory/develop/app-only-access-primer).

    Alternatively, the Service Principal can be granted the `Application.ReadWrite.OwnedBy` permission if the Service Principal is only intended to manage its own App Registration/Application.

    #### Client Certificate or Client Secret

    Beginning in version 3.0.0, the Azure App Registration and Enterprise Application Orchestrator extension supports both [client certificate authentication](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-1-add-a-certificate) and [client secret](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-2-add-a-client-secret) authentication.

    * **Client Secret** - Follow [Microsoft's documentation](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-2-add-a-client-secret) to create a Client Secret. This secret will be used as the **Server Password** field in the [Certificate Store Configuration](#certificate-store-configuration) section.
    * **Client Certificate** - Create a client certificate key pair with the Client Authentication extended key usage. The client certificate will be used in the ClientCertificate field in the [Certificate Store Configuration](#certificate-store-configuration) section. If you have access to Keyfactor Command, the instructions in this section walk you through enrolling a certificate and ensuring that it's in the correct format. Once enrolled, follow [Microsoft's documentation](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-1-add-a-certificate) to add the _public key_ certificate (no private key) to the service principal used for authentication.

        The certificate can be in either of the following formats:
        * Base64-encoded PKCS#12 (PFX) with a matching private key.
        * Base64-encoded PEM-encoded certificate _and_ PEM-encoded PKCS8 private key. Make sure that the certificate and private key are separated with a newline. The order doesn't matter - the extension will determine which is which.

        If the private key is encrypted, the encryption password will replace the **Server Password** field in the [Certificate Store Configuration](#certificate-store-configuration) section.

    > **Creating and Formatting a Client Certificate using Keyfactor Command**
    >
    > To get started quickly, you can follow the instructions below to create and properly format a client certificate to authenticate to the Microsoft Graph API.
    >
    > 1. In Keyfactor Command, hover over **Enrollment** and select **PFX Enrollment**.
    > 2. Select a **Template** that supports Client Authentication as an extended key usage.
    > 3. Populate the certificate subject as appropriate for the Template. It may be sufficient to only populate the Common Name, but consult your IT policy to ensure that this certificate is compliant.
    > 4. At the bottom of the page, uncheck the box for **Include Chain**, and select either **PFX** or **PEM** as the certificate Format.
    > 5. Make a note of the password on the next page - it won't be shown again.
    > 6. Prepare the certificate and private key for Azure and the Orchestrator extension:
    >     * If you downloaded the certificate in PEM format, use the commands below:
    >
    >        ```shell
    >        # Verify that the certificate downloaded from Command contains the certificate and private key. They should be in the same file
    >        cat <your_certificate.pem>
    >
    >        # Separate the certificate from the private key
    >        openssl x509 -in <your_certificate.pem> -out pubkeycert.pem
    >
    >        # Base64 encode the certificate and private key
    >        cat <your_certificate.pem> | base64 > clientcertkeypair.pem.base64
    >        ```
    >
    >    * If you downloaded the certificate in PFX format, use the commands below:
    >
    >        ```shell
    >        # Export the certificate from the PFX file
    >        openssl pkcs12 -in <your_certificate.pfx> -clcerts -nokeys -out pubkeycert.pem
    >
    >        # Base64 encode the PFX file
    >        cat <your_certificate.pfx> | base64 > clientcert.pfx.base64
    >        ```
    > 7. Follow [Microsoft's documentation](https://learn.microsoft.com/en-us/graph/auth-register-app-v2#option-1-add-a-certificate) to add the public key certificate to the service principal used for authentication.
    >
    > You will use `clientcert.[pem|pfx].base64` as the **ClientCertificate** field in the [Certificate Store Configuration](#certificate-store-configuration) section.

    ### Enterprise Application (Service Principal)

    #### Service Principal Certificates

    Service Principal certificates are typically used for SAML Token signing. Service Principals are created from Enterprise Applications, and will mostly be configured with a variation of Microsoft's [SAML-based single sign-on](https://learn.microsoft.com/en-us/entra/identity/enterprise-apps/add-application-portal) documentation. For more information on the mechanics of the Service Principal certificate management capabilities of this extension, please see the [mechanics](#extension-mechanics) section.



    </details>

2. Create Certificate Store Types for the Azure App Registration and Enterprise Application Orchestrator extension. 

    * **Using kfutil**:

        ```shell
        # Azure Enterprise Application (Service Principal)
        kfutil store-types create AzureSP
        ```

    * **Manually**:
        * [Azure Enterprise Application (Service Principal)](docs/azuresp.md#certificate-store-type-configuration)

3. Install the Azure App Registration and Enterprise Application Universal Orchestrator extension.
    
    * **Using kfutil**: On the server that that hosts the Universal Orchestrator, run the following command:

        ```shell
        # Windows Server
        kfutil orchestrator extension -e azure-application-orchestrator@latest --out "C:\Program Files\Keyfactor\Keyfactor Orchestrator\extensions"

        # Linux
        kfutil orchestrator extension -e azure-application-orchestrator@latest --out "/opt/keyfactor/orchestrator/extensions"
        ```

    * **Manually**: Follow the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions) to install the latest [Azure App Registration and Enterprise Application Universal Orchestrator extension](https://github.com/Keyfactor/azure-application-orchestrator/releases/latest).

4. Create new certificate stores in Keyfactor Command for the Sample Universal Orchestrator extension.
    * [Azure Enterprise Application (Service Principal)](docs/azuresp.md#certificate-store-configuration)
</details>


## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Universal Orchestrator extensions](https://github.com/orgs/Keyfactor/repositories?q=orchestrator).

When creating cert store type manually, that store property names and entry parameter names are case sensitive


When creating cert store type manually, that store property names and entry parameter names are case sensitive



## Azure App Registration/Application

Azure [App Registration/Application certificates](https://learn.microsoft.com/en-us/entra/identity-platform/certificate-credentials) are typically used for client authentication by applications and are typically public key only in Azure. The general model by which these credentials are consumed is that the certificate and private key are accessible by the Application using the App Registration, and are passed to the service that is authenticating the Application. The Azure App Registration and Enterprise Application Orchestrator extension implements the Inventory, Management Add, Management Remove, and Discovery job types for managing these certificates.

## Requirements

### Azure Service Principal (Graph API Authentication)

The Azure App Registration and Enterprise Application Orchestrator extension uses an [Azure Service Principal](https://learn.microsoft.com/en-us/entra/identity-platform/app-objects-and-service-principals?tabs=browser) for authentication. Follow [Microsoft's documentation](https://learn.microsoft.com/en-us/entra/identity-platform/howto-create-service-principal-portal) to create a service principal. Currently, Client Secret authentication is supported. The Service Principal must have the following API Permission:
- **_Microsoft Graph Application Permissions_**:
  - `Application.ReadWrite.All` (_not_ Delegated; Admin Consent) - Allows the app to create, read, update and delete applications and service principals without a signed-in user.

> For more information on Admin Consent for App-only access (also called "Application Permissions"), see the [primer on application-only access](https://learn.microsoft.com/en-us/azure/active-directory/develop/app-only-access-primer).

Alternatively, the Service Principal can be granted the `Application.ReadWrite.OwnedBy` permission if the Service Principal is only intended to manage its own App Registration/Application.

### Azure App Registration (Application)

#### Application Certificates

Application certificates are used for client authentication and are typically public key only. No additional configuration in Azure is necessary to manage Application certificates since all App Registrations can contain any number of [Certificates and Secrets](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app#add-credentials). Unless the Discovery job is used, you should collect the Application IDs for each App Registration that contains certificates to be managed.

## Extension Mechanics

The Azure App Registration and Enterprise Application Orchestrator extension uses the [Microsoft Dotnet Graph SDK](https://learn.microsoft.com/en-us/graph/sdks/sdks-overview) to interact with the Microsoft Graph API. The extension uses the following Graph API endpoints to manage Application certificates:

* [Get Application](https://learn.microsoft.com/en-us/graph/api/application-get?view=graph-rest-1.0&tabs=http) - Used to obtain the Object ID of the App Registration, and to download the certificates owned by the App Registration.
* [Update Application](https://learn.microsoft.com/en-us/graph/api/application-update?view=graph-rest-1.0&tabs=http) - Used to modify the App Registration to add or remove certificates.
    * Specifically, the extension manipulates the [`keyCredentials` resource](https://learn.microsoft.com/en-us/graph/api/resources/keycredential?view=graph-rest-1.0) of the Application object.

### Discovery Job

The Discovery operation discovers all Azure App Registrations that the Service Principal has access to. The discovered App Registrations (specifically, their Application IDs) are reported back to Command and can be easily added as certificate stores from the Locations tab.

The Discovery operation uses the "Directories to search" field, and accepts input in one of the following formats:
- `*` - If the asterisk symbol `*` is used, the extension will search for all Azure App Registrations that the Service Principal has access to, but only in the tenant that the discovery job was configured for as specified by the "Client Machine" field in the certificate store configuration.
- `<tenant-id>,<tenant-id>,...` - If a comma-separated list of tenant IDs is used, the extension will search for all Azure App Registrations available in each tenant specified in the list. The tenant IDs should be the GUIDs associated with each tenant, and it's the user's responsibility to ensure that the service principal has access to the specified tenants.

## Certificate Store Type Configuration

The recommended method for creating the `AzureApp` Certificate Store Type is to use [kfutil](https://github.com/Keyfactor/kfutil). After installing, use the following command to create the `AzureApp` Certificate Store Type:

```shell
kfutil store-types create AzureApp
```

Alternatively, you can create the `AzureApp` Certificate Store Type manually by following the following steps:

<details><summary>AzureApp</summary>

Create a store type called `AzureApp` with the attributes in the tables below:

### Basic Tab
| Attribute | Value | Description |
| --------- | ----- | ----- |
| Name | Azure Application (Auth) | Display name for the store type (may be customized) |
| Short Name | AzureApp | Short display name for the store type |
| Capability | AzureApp | Store type name orchestrator will register with. Check the box to allow entry of value |
| Supported Job Types (check the box for each) | Add, Discovery, Remove | Job types the extension supports |
| Needs Server | &check; | Determines if a target server name is required when creating store |
| Blueprint Allowed |  | Determines if store type may be included in an Orchestrator blueprint |
| Uses PowerShell |  | Determines if underlying implementation is PowerShell |
| Requires Store Password |  | Determines if a store password is required when configuring an individual store. |
| Supports Entry Password |  | Determines if an individual entry within a store can have a password. |


The Basic tab should look like this:

![AzureApp Basic Tab](../.github/images/AzureApp-basic-store-type-dialog.png)

### Advanced Tab
| Attribute | Value | Description |
| --------- | ----- | ----- |
| Supports Custom Alias | Required | Determines if an individual entry within a store can have a custom Alias. |
| Private Key Handling | Forbidden | This determines if Keyfactor can send the private key associated with a certificate to the store. Required because IIS certificates without private keys would be invalid. |
| PFX Password Style | Default | 'Default' - PFX password is randomly generated, 'Custom' - PFX password may be specified when the enrollment job is created (Requires the Allow Custom Password application setting to be enabled.) |


The Advanced tab should look like this:

![AzureApp Advanced Tab](../.github/images/AzureApp-advanced-store-type-dialog.png)

### Custom Fields Tab
Custom fields operate at the certificate store level and are used to control how the orchestrator connects to the remote target server containing the certificate store to be managed. The following custom fields should be added to the store type:

| Name | Display Name | Type | Default Value/Options | Required | Description |
| ---- | ------------ | ---- | --------------------- | -------- | ----------- |
| ServerUsername | Server Username | Secret |  |  | The Application ID of the Service Principal used to authenticate with Microsoft Graph for managing Application/Service Principal certificates. |
| ServerPassword | Server Password | Secret |  |  | A Client Secret that the extension will use to authenticate with Microsoft Graph for managing Application/Service Principal certificates. |
| ServerUseSsl | Use SSL | Bool | true | &check; | Specifies whether SSL should be used for communication with the server. Set to 'true' to enable SSL, and 'false' to disable it. |


The Custom Fields tab should look like this:

![AzureApp Custom Fields Tab](../.github/images/AzureApp-custom-fields-store-type-dialog.png)

</details>

## Certificate Store Configuration

After creating the `AzureApp` Certificate Store Type and installing the Azure App Registration and Enterprise Application Orchestrator extension, you can create new [Certificate Stores](https://software.keyfactor.com/Core-OnPrem/Current/Content/ReferenceGuide/Certificate%20Stores.htm?Highlight=certificate%20store) to manage certificates in the remote platform.

The following table describes the required and optional fields for the `AzureApp` certificate store type.

| Attribute | Description |
| --------- | ----------- |
| Category | Select Azure Application (Auth)  or the customized certificate store name from the previous step. |
| Container | Optional container to associate certificate store with. |
| Client Machine | The Azure Tenant (directory) ID that owns the Service Principal. |
| Store Path | The Application ID of the target Application/Service Principal that will be managed by the Azure App Registration and Enterprise Application Orchestrator extension. |
| Orchestrator | Select an approved orchestrator capable of managing AzureApp certificates. Specifically, one with the AzureApp capability. |
| Server Username | The Application ID of the Service Principal used to authenticate with Microsoft Graph for managing Application/Service Principal certificates. |
| Server Password | A Client Secret that the extension will use to authenticate with Microsoft Graph for managing Application/Service Principal certificates. |
| Use SSL | Specifies whether SSL should be used for communication with the server. Set to 'true' to enable SSL, and 'false' to disable it. |

* **Using kfutil**

    ```shell
    # Generate a CSV template for the AzureApp certificate store
    kfutil stores import generate-template --store-type-name AzureApp --outpath AzureApp.csv

    # Open the CSV file and fill in the required fields for each certificate store.

    # Import the CSV file to create the certificate stores
    kfutil stores import csv --store-type-name AzureApp --file AzureApp.csv
    ```

* **Manually with the Command UI**: In Keyfactor Command, navigate to Certificate Stores from the Locations Menu. Click the Add button to create a new Certificate Store using the attributes in the table above.


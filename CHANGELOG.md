- 1.0.1
    - First production release

- 2.0.0
    - Properly report Private Key existence in Inventory job for both AzureSP and AzureApp.
    - Remove Private Key handling from AzureApp Certificate Store Type

- 3.0.0
    - Implement client certificate authentication as a secondary authentication method to Microsoft Graph API

- 3.1.0
    - chore(deps): Upgrade .NET packages to their latest versions

- 3.1.1
  - fix(deps): Revert main Azure App Registration and Enterprise Application Orchestrator extension .NET project to .NET 6 from .NET 8.

- 3.2.0
  - chore(docs): Upgrade GitHub Actions to use Bootstrap Workflow v3 to support Doctool

- 4.0.0
  - Depricate AzureApp and AzureSP in favor of AzureApp2 and AzureSP2 that interpret the Store Path field as the Object ID instead of App ID.
  - Discovery job modified to return available Certificate Stores with Store Path in the format `<ID GUID> (<Friendly Name>)`.
  - Before other jobs operate on Certificate Stores, the contents after the ID GUID will be truncated, maintaining backward compatibility.


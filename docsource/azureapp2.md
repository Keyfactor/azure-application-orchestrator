## Overview

Azure [App Registration/Application certificates](https://learn.microsoft.com/en-us/entra/identity-platform/certificate-credentials)
are typically used for client authentication by applications and are typically public key only in Azure. The general
model by which these credentials are consumed is that the certificate and private key are accessible by the Application
using the App Registration, and are passed to the service authenticating the Application. The Azure App
Registration and Enterprise Application Orchestrator extension implements the Inventory, Management Add, Management
Remove, and Discovery job types for managing these certificates.

## Requirements

Application certificates are used for client authentication and are typically public key only. No additional
configuration in Azure is necessary to manage Application certificates since all App Registrations can contain any
number
of [Certificates and Secrets](https://learn.microsoft.com/en-us/entra/identity-platform/quickstart-register-app#add-credentials).
Unless the Discovery job is used, you should collect the Application IDs for each App Registration that contains
certificates to be managed.
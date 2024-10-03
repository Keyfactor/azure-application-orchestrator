<#
.NOTES
    Created on:       03/10/2024
    Created by:       Hayden Roszell
    Filename:         DefineDiscoveredStores.ps1
    Tested on Keyfactor Command v24.4
#>

# Parameters
param(
    [Parameter(Mandatory = $true)]
    [string]$BearerTokenUrl,

    [Parameter(Mandatory = $true)]
    [string]$ClientID,

    [Parameter(Mandatory = $true)]
    [string]$ClientSecret,

    [Parameter(Mandatory = $false)]
    [string]$Scope,

    [Parameter(Mandatory = $false)]
    [string]$Audience,

    [Parameter(Mandatory = $true)]
    [string]$CommandApiUrl,

    [Parameter(Mandatory = $true)]
    [string]$CertificateStoreType,  # Short name of the certificate store type

    [Parameter(Mandatory = $true)]
    [string]$ServerUsername,

    [Parameter(Mandatory = $true)]
    [string]$ServerPassword
)

# Validate parameters
$errorsPresent = $false

if ([string]::IsNullOrEmpty($BearerTokenUrl))
{
    Write-Error "BearerTokenUrl is required"
    $errorsPresent = $true
}

if ([string]::IsNullOrEmpty($ClientID))
{
    Write-Error "ClientID is required"
    $errorsPresent = $true
}

if ([string]::IsNullOrEmpty($ClientSecret))
{
    Write-Error "ClientSecret is required"
    $errorsPresent = $true
}

if ([string]::IsNullOrEmpty($CommandApiUrl))
{
    Write-Error "CommandApiUrl is required"
    $errorsPresent = $true
}

if ([string]::IsNullOrEmpty($CertificateStoreType))
{
    Write-Error "CertificateStoreType is required"
    $errorsPresent = $true
}

if ([string]::IsNullOrEmpty($ServerUsername))
{
    Write-Error "ServerUsername is required"
    $errorsPresent = $true
}

if ([string]::IsNullOrEmpty($ServerPassword))
{
    Write-Error "ServerPassword is required"
    $errorsPresent = $true
}

if ($errorsPresent)
{
    exit 1
}


function Submit-RESTRequest
{
    param(

        [Parameter(Mandatory,HelpMessage='The request path')]
        [string]$Path,
        [Parameter(HelpMessage='Body of request')]
        [string]$Body,
        [Parameter(Mandatory,HelpMessage='Method of API call')]
        [ValidateSet("GET","POST","PUT","DELETE")]
        [string]$Method
    )

    # Fetch Bearer Token
    try
    {
        # Build the token request body
        $token_body = @{
            grant_type    = 'client_credentials'
            client_id     = $script:ClientID
            client_secret = $script:ClientSecret
        }

        # Include Scope if provided
        if (-not [string]::IsNullOrEmpty($script:Scope))
        {
            $token_body['scope'] = $script:Scope
        }

        # Include Audience if provided
        if (-not [string]::IsNullOrEmpty($script:Audience))
        {
            $token_body['audience'] = $script:Audience
        }

        # Request the token
        Write-Host "Fetching token from $script:BearerTokenUrl"
        $tokenResponse = Invoke-RestMethod -Method Post -Uri $script:BearerTokenUrl -Body $token_body -ContentType 'application/x-www-form-urlencoded'

        $accessToken = $tokenResponse.access_token

        if (-not $accessToken)
        {
            Write-Error "Failed to retrieve access token."
            exit 1
        }
    } catch
    {
        Write-Error "Error fetching access token: $_"
        exit 1
    }

    # Use the token to call the Keyfactor Command API
    $headers = @{
        'Authorization' = "Bearer $accessToken"
        'Content-Type'  = 'application/json'
        'x-keyfactor-api-version' = '1.0'
        'x-keyfactor-requested-with' = 'APIClient'
    }

    Write-Host "Submitting $Method request to $Path"

    try
    {
        if ($Body)
        {
            $apiResponse = Invoke-RestMethod -Method $Method -Uri "$CommandApiUrl/$Path" -Headers $headers -Body $Body
        } else
        {
            $apiResponse = Invoke-RestMethod -Method $Method -Uri "$CommandApiUrl/$Path" -Headers $headers
        }

    } catch
    {
        Write-Error "Error calling the Keyfactor Command API: $_"
        exit 1
    }

    return $apiResponse
}

# Step 1: Get the available Certificate Store Types
$certificateStoreTypes = Submit-RESTRequest -Method GET -Path "CertificateStoreTypes"

$desiredStoreType = $certificateStoreTypes | Where-Object { $_.ShortName -eq $CertificateStoreType }

if (-not $desiredStoreType)
{
    Write-Error "Certificate Store Type with ShortName '$CertificateStoreType' not found."
    exit 1
}

$certStoreTypeId = $desiredStoreType.StoreType
Write-Host "$CertificateStoreType has Type ID $certStoreTypeId"

# Step 3: Fetch the Certificate Stores
$certificateStores = Submit-RESTRequest -Method GET -Path "CertificateStores"

# Step 4: Process the Certificate Stores
$storesToProcess = $certificateStores | Where-Object { $_.Approved -eq $false -and $_.CertStoreType -eq $certStoreTypeId }
$storesToProcessLength = $storesToProcess.Length

Write-Host "Found $storesToProcessLength Discovered Certificate Stores of type $CertificateStoreType"

foreach ($store in $storesToProcess)
{
    # Add/update the properties
    $properties = @{
        ServerUsername = @{
            value = @{
                SecretValue = $ServerUsername
            }
        }
        ServerPassword = @{
            value = @{
                SecretValue = $ServerPassword
            }
        }
        ClientCertificate = @{
            value = @{
                SecretValue = ""
            }
        }
        ClientCertificatePassword = @{
            value = @{
                SecretValue = ""
            }
        }
        ServerUseSsl = @{
            value = "true"
        }
    }

    # Convert back to JSON string
    $propertiesJson = $properties | ConvertTo-Json -Compress

    # Build the request body
    $body = @{
        Id                = $store.Id
        ContainerId       = $store.ContainerId
        CreateIfMissing   = $false
        Properties        = $propertiesJson
        InventorySchedule = $store.InventorySchedule
        Password          = $null
    }

    # Convert body to JSON
    $bodyJson = $body | ConvertTo-Json -Depth 10

    # Submit POST request
    $response = Submit-RESTRequest -Method PUT -Path "CertificateStores" -Body $bodyJson

    Write-Host "Updated Certificate Store with Id $($response.Id)"
}

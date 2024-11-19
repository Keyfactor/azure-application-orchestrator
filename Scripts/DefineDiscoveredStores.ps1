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
    [string]$ServicePrincipalClientID,

    [Parameter(Mandatory = $true)]
    [string]$ServicePrincipalClientSecret,

    [Parameter(Mandatory = $true)]
    [string]$WhitelistCsvPath  # Path to the whitelist CSV file
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

if ([string]::IsNullOrEmpty($ServicePrincipalClientID))
{
    Write-Error "ServicePrincipalClientID is required"
    $errorsPresent = $true
}

if ([string]::IsNullOrEmpty($ServicePrincipalClientSecret))
{
    Write-Error "ServicePrincipalClientSecret is required"
    $errorsPresent = $true
}

if (-not (Test-Path $WhitelistCsvPath))
{
    Write-Error "Whitelist CSV file '$WhitelistCsvPath' does not exist."
    $errorsPresent = $true
}

if ($errorsPresent)
{
    exit 1
}

# Read the whitelist CSV file
try
{
    $whitelistData = Import-Csv -Path $WhitelistCsvPath
    $whitelistGuids = $whitelistData | Select-Object -ExpandProperty id
} catch
{
    Write-Error "Error reading or processing the whitelist CSV file: $_"
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

Write-Host "============================================================================================"
Write-Host "Step 1: Get the Store Type ID that corresponds to $CertificateStoreType"
Write-Host "============================================================================================"
$certificateStoreTypes = Submit-RESTRequest -Method GET -Path "CertificateStoreTypes"

$desiredStoreType = $certificateStoreTypes | Where-Object { $_.ShortName -eq $CertificateStoreType }

if (-not $desiredStoreType)
{
    Write-Error "Certificate Store Type with ShortName '$CertificateStoreType' not found."
    exit 1
}

$certStoreTypeId = $desiredStoreType.StoreType
Write-Host "$CertificateStoreType has Type ID $certStoreTypeId"

Write-Host "============================================================================================"
Write-Host "Step 2: Download all Certificate Stores from Command with pagination"
Write-Host "============================================================================================"
$pageSize = 100
$currentPage = 1
$allPagesDownloaded = $false

$totalDownloadedCertStores
$allCertificateStores = @()
do
{
    Write-Host "Downloading Certificate Stores page $currentPage (page size $pageSize)"
    try
    {
        $certificateStores = Submit-RESTRequest -Method GET -Path "CertificateStores?ReturnLimit=$pageSize&PageReturned=$currentPage"

        # Check if any certificate stores are returned
        if ($certificateStores.Count -eq 0)
        {
            $allPagesDownloaded = $true
        } else
        {
            Write-Host "Fetched $($certificateStores.Count) certificate stores."
            $totalDownloadedCertStores += $certificateStores.Count 

            $allCertificateStores += $certificateStores
        }

        # Move to the next page
        $currentPage++

    } catch
    {
        Write-Error "Failed to fetch certificate stores: $_"
        $allPagesDownloaded = $true # Exit the loop on error
    }

} while (!$allPagesDownloaded)

Write-Host "Finished downloading $totalDownloadedCertStores total certificate stores in $($currentPage - 2) pages"

Write-Host "============================================================================================"
Write-Host "Step 3: Filter the downloaded Certificate Stores for ones that came back in Discovery"
Write-Host "============================================================================================"
$storesToProcess = $allCertificateStores | Where-Object { $_.Approved -eq $false -and $_.CertStoreType -eq $certStoreTypeId }

Write-Host "$($storesToProcess.Length)/$totalDownloadedCertStores downloaded Certificate Stores are Discovered Certificate Stores of type $CertificateStoreType ($certStoreTypeId) (only exist on the Discovery page; haven't been defined in Command)"

Write-Host "============================================================================================"
Write-Host "Step 4: Update (define) Certificate Stores that are on the Whitelist"
Write-Host "============================================================================================"
foreach ($store in $storesToProcess)
{
    # Truncate Storepath to extract GUID
    $storePathParts = $store.Storepath.Split(" ")
    $storePathGuid = $storePathParts[0]

    if (-not ($whitelistGuids -contains $storePathGuid))
    {
        Write-Host "Skipping store with Path '$($store.Storepath)' as its Storepath GUID '$storePathGuid' is not in the whitelist."
        continue
    }

    Write-Host "Certificate Store with Path '$($store.Storepath)' was found in the whitelist - adding"

    # Add/update the properties
    $properties = @{
        ServerUsername = @{
            value = @{
                SecretValue = $ServicePrincipalClientID
            }
        }
        ServerPassword = @{
            value = @{
                SecretValue = $ServicePrincipalClientSecret
            }
        }
        # ClientCertificate = @{
        #     value = @{
        #         SecretValue = ""
        #     }
        # }
        # ClientCertificatePassword = @{
        #     value = @{
        #         SecretValue = ""
        #     }
        # }
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

    # Submit PUT request
    $response = Submit-RESTRequest -Method PUT -Path "CertificateStores" -Body $bodyJson

    Write-Host "Updated Certificate Store with Id $($response.Id)"
}

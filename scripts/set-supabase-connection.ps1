param(
    [string]$SupabaseConnectionString,
    [switch]$SkipUserEnvironment,
    [switch]$SkipLocalFiles
)

$ErrorActionPreference = "Stop"

function ConvertTo-PlainText {
    param(
        [Parameter(Mandatory = $true)]
        [Security.SecureString]$SecureValue
    )

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        if ($bstr -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
    }
}

function Assert-ValidConnectionString {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "SUPABASE_CONNECTION_STRING is empty."
    }

    if ($Value -match "YOUR_|YOUR-|PROJECT_REF|YOUR_NEW_PASSWORD") {
        throw "SUPABASE_CONNECTION_STRING still looks like a placeholder."
    }

    foreach ($requiredPart in @("Host=", "Database=", "Username=", "Password=")) {
        if ($Value.IndexOf($requiredPart, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
            throw "SUPABASE_CONNECTION_STRING is missing required part: $requiredPart"
        }
    }
}

function Write-LocalSettingsFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $settings = [ordered]@{
        ConnectionStrings = [ordered]@{
            DefaultConnection = $Value
        }
    }

    $json = $settings | ConvertTo-Json -Depth 4
    Set-Content -LiteralPath $Path -Value $json -Encoding UTF8
}

if ([string]::IsNullOrWhiteSpace($SupabaseConnectionString)) {
    Write-Host "Paste the full Supabase connection string. It will not be shown on screen."
    $secureValue = Read-Host "SUPABASE_CONNECTION_STRING" -AsSecureString
    $SupabaseConnectionString = ConvertTo-PlainText -SecureValue $secureValue
}

Assert-ValidConnectionString -Value $SupabaseConnectionString

$repoRoot = Split-Path -Parent $PSScriptRoot
$cmsLocalSettings = Join-Path $repoRoot "VinhKhanhTour.CMS\appsettings.Development.Local.json"
$apiLocalSettings = Join-Path $repoRoot "VinhKhanhTour.API\appsettings.Development.Local.json"

if (-not $SkipUserEnvironment) {
    [Environment]::SetEnvironmentVariable("SUPABASE_CONNECTION_STRING", $SupabaseConnectionString, "User")
    $env:SUPABASE_CONNECTION_STRING = $SupabaseConnectionString
    Write-Host "Saved SUPABASE_CONNECTION_STRING to the current User environment."
}

if (-not $SkipLocalFiles) {
    Write-LocalSettingsFile -Path $cmsLocalSettings -Value $SupabaseConnectionString
    Write-LocalSettingsFile -Path $apiLocalSettings -Value $SupabaseConnectionString
    Write-Host "Wrote ignored local settings files for CMS and API."
}

Write-Host "Supabase connection setup is ready. Restart Visual Studio if it was already open."

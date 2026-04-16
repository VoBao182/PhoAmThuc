param(
    [Parameter(Mandatory = $true)]
    [string]$HostedApiBaseUrl,

    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "VinhKhanhTourDemo\VinhKhanhTourDemo.csproj"

Write-Host "Publishing Android APK with HostedApiBaseUrl=$HostedApiBaseUrl"

dotnet publish $projectPath `
    -f net10.0-android `
    -c $Configuration `
    /p:HostedApiBaseUrl=$HostedApiBaseUrl `
    /p:AndroidPackageFormat=apk

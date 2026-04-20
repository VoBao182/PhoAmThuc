param(
    [string]$HostedApiBaseUrl = "https://phoamthuc.onrender.com",

    [string]$Configuration = "Release",

    [string]$RuntimeIdentifier = "android-arm64",

    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "VinhKhanhTourDemo\VinhKhanhTourDemo.csproj"
$publishStartedAtUtc = [DateTime]::UtcNow
$resolvedOutputDir = if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    Join-Path $repoRoot "artifacts\android\$Configuration"
} else {
    $OutputDir
}
$dotnetCliHome = Join-Path $repoRoot ".codex-temp\dotnet-home"

New-Item -ItemType Directory -Path $dotnetCliHome -Force | Out-Null
$env:DOTNET_CLI_HOME = $dotnetCliHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

Write-Host "Publishing Android APK..."
if ([string]::IsNullOrWhiteSpace($HostedApiBaseUrl)) {
    Write-Host "HostedApiBaseUrl: not set. APK will allow manual API URL configuration in-app."
} else {
    Write-Host "HostedApiBaseUrl: $HostedApiBaseUrl"
}
Write-Host "RuntimeIdentifier: $RuntimeIdentifier"

$publishArgs = @(
    "publish"
    $projectPath
    "-f"
    "net10.0-android"
    "-c"
    $Configuration
    "-r"
    $RuntimeIdentifier
    "/p:AndroidPackageFormat=apk"
)

if (-not [string]::IsNullOrWhiteSpace($HostedApiBaseUrl)) {
    $publishArgs += "/p:HostedApiBaseUrl=$HostedApiBaseUrl"
}

& dotnet @publishArgs

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$apkSearchRoot = Join-Path $repoRoot "VinhKhanhTourDemo\bin\$Configuration\net10.0-android"
$apkFiles = Get-ChildItem -Path $apkSearchRoot -Recurse -Filter *.apk |
    Where-Object { $_.LastWriteTimeUtc -ge $publishStartedAtUtc.AddSeconds(-5) } |
    Sort-Object LastWriteTimeUtc -Descending

if (-not $apkFiles) {
    throw "Build completed but no APK file was found under $apkSearchRoot"
}

New-Item -ItemType Directory -Path $resolvedOutputDir -Force | Out-Null

$latestApk = $apkFiles[0]
$targetApkPath = Join-Path $resolvedOutputDir $latestApk.Name
Copy-Item -LiteralPath $latestApk.FullName -Destination $targetApkPath -Force

Write-Host "APK ready:"
Write-Host $targetApkPath

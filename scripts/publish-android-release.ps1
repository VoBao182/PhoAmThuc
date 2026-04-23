param(
    [string]$HostedApiBaseUrl = "https://phoamthuc.onrender.com",

    [string]$Configuration = "Release",

    [string]$RuntimeIdentifier = "",

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
if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    Write-Host "RuntimeIdentifier(s): project defaults"
} else {
    Write-Host "RuntimeIdentifier: $RuntimeIdentifier"
}

$commonAndroidArgs = @(
    $projectPath
    "-f"
    "net10.0-android"
    "-c"
    $Configuration
)

if (-not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    $commonAndroidArgs += @(
        "-r"
        $RuntimeIdentifier
    )
}

$restoreArgs = @(
    "restore"
    $projectPath
    "/p:TargetFramework=net10.0-android"
    "/p:Configuration=$Configuration"
    "/p:RestoreIgnoreFailedSources=true"
)

if (-not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    $restoreArgs += "/p:RuntimeIdentifier=$RuntimeIdentifier"
}

& dotnet @restoreArgs

if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE"
}

$cleanArgs = @(
    "clean"
) + $commonAndroidArgs

& dotnet @cleanArgs

if ($LASTEXITCODE -ne 0) {
    throw "dotnet clean failed with exit code $LASTEXITCODE"
}

$publishArgs = @(
    "publish"
    "--no-restore"
    "/p:AndroidPackageFormat=apk"
) + $commonAndroidArgs

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

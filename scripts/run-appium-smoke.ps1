param(
    [string]$AdbPath = "",
    [string]$AppiumServerUrl = "http://127.0.0.1:4723",
    [string]$Configuration = "Debug",
    [switch]$SkipBuild,
    [switch]$NoReset
)

$ErrorActionPreference = "Stop"

function Resolve-CommandPath([string]$name) {
    $command = Get-Command $name -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    return $null
}

function Resolve-AdbPath {
    param([string]$ConfiguredPath)

    if ($ConfiguredPath -and (Test-Path $ConfiguredPath)) {
        return $ConfiguredPath
    }

    $envAdb = Resolve-CommandPath "adb"
    if ($envAdb) {
        return $envAdb
    }

    $defaultPath = "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe"
    if (Test-Path $defaultPath) {
        return $defaultPath
    }

    throw "Khong tim thay adb. Cai Android SDK platform-tools hoac truyen -AdbPath."
}

function Wait-AppiumReady {
    param([string]$Url)

    $statusUrl = "$($Url.TrimEnd('/'))/status"
    $deadline = (Get-Date).AddSeconds(45)

    while ((Get-Date) -lt $deadline) {
        try {
            $status = Invoke-RestMethod -Uri $statusUrl -TimeoutSec 3
            if ($status.value.ready -eq $true) {
                return
            }
        }
        catch {
            Start-Sleep -Seconds 1
        }
    }

    throw "Appium chua san sang tai $statusUrl."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$adb = Resolve-AdbPath $AdbPath

Write-Host "1. Kiem tra thiet bi Android..."
$devices = & $adb devices
$onlineDevice = $devices | Select-String "`tdevice$" | Select-Object -First 1
if (-not $onlineDevice) {
    throw "Khong co emulator/USB device o trang thai 'device'. Mo emulator hoac bat USB debugging roi chay lai."
}
Write-Host "OK: $onlineDevice"

if (-not $SkipBuild) {
    Write-Host "2. Build APK Android..."
    dotnet publish "$repoRoot\VinhKhanhTourDemo\VinhKhanhTourDemo.csproj" -f net10.0-android -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE"
    }
}
else {
    Write-Host "2. Bo qua build APK (-SkipBuild)."
}

$apk = Get-ChildItem "$env:LOCALAPPDATA\VinhKhanhTourDemo\bin\VinhKhanhTourDemo\$Configuration\net10.0-android" -Recurse -Filter "*Signed.apk" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $apk) {
    throw "Khong tim thay APK da build trong LOCALAPPDATA. Hay bo -SkipBuild hoac set APPIUM_APP_PATH thu cong."
}
Write-Host "APK: $($apk.FullName)"

Write-Host "3. Kiem tra Appium..."
$appium = Resolve-CommandPath "appium"
if (-not $appium) {
    throw "Khong tim thay appium. Chay: npm install -g appium; appium driver install uiautomator2"
}

$serverReady = $false
try {
    Wait-AppiumReady $AppiumServerUrl
    $serverReady = $true
    Write-Host "Appium dang chay: $AppiumServerUrl"
}
catch {
    Write-Host "Dang start Appium server..."
    $appiumProcess = Start-Process -FilePath $appium -ArgumentList "--base-path","/" -WindowStyle Hidden -PassThru
    Wait-AppiumReady $AppiumServerUrl
    $serverReady = $true
}

if (-not $serverReady) {
    throw "Khong the start Appium."
}

Write-Host "4. Chay Appium smoke tests..."
$env:RUN_APPIUM_TESTS = "1"
$env:APPIUM_SERVER_URL = $AppiumServerUrl
$env:APPIUM_APP_PATH = $apk.FullName
$env:APPIUM_DEVICE_NAME = "Android"
if ($NoReset) {
    $env:APPIUM_NO_RESET = "1"
}
else {
    Remove-Item Env:\APPIUM_NO_RESET -ErrorAction SilentlyContinue
}

dotnet test "$repoRoot\tests\VinhKhanhTour.MAUI.AppiumTests\VinhKhanhTour.MAUI.AppiumTests.csproj" --logger "trx;LogFileName=maui-appium-tests.trx" --results-directory "$repoRoot\TestResults"
if ($LASTEXITCODE -ne 0) {
    throw "Appium smoke tests failed with exit code $LASTEXITCODE. Xem artifacts trong TestResults\artifacts\appium."
}

Write-Host "Xong. Appium smoke tests da pass."

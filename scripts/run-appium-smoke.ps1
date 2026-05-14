param(
    [string]$AdbPath = "",
    [string]$AppiumServerUrl = "http://127.0.0.1:4723",
    [string]$Configuration = "Debug",
    [switch]$SkipBuild,
    [switch]$NoReset,
    [switch]$RestartAppium
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

function Resolve-AndroidSdkRootFromAdb {
    param([string]$ResolvedAdbPath)

    $platformTools = Split-Path -Parent $ResolvedAdbPath
    if ((Split-Path -Leaf $platformTools) -eq "platform-tools") {
        return Split-Path -Parent $platformTools
    }

    return $null
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

function Stop-AppiumProcesses {
    $currentProcessId = $PID
    $processes = Get-CimInstance Win32_Process |
        Where-Object {
            $_.ProcessId -ne $currentProcessId
                -and $_.Name -in @("node.exe", "powershell.exe", "cmd.exe")
                -and $_.CommandLine -match "appium"
        }

    foreach ($process in $processes) {
        Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
    }
}

function Stop-DotnetBuildProcesses {
    Write-Host "Don build server/MSBuild node cu de tranh lock Java.Interop.dll..."
    & dotnet build-server shutdown | Out-Host

    $currentProcessId = $PID
    $processes = Get-CimInstance Win32_Process |
        Where-Object {
            $isBuildHelper = $_.Name -in @("MSBuild.exe", "VBCSCompiler.exe")
            $isDotnetMsBuildNode = $_.Name -eq "dotnet.exe" -and $_.CommandLine -match "MSBuild\.dll"
            $_.ProcessId -ne $currentProcessId -and ($isBuildHelper -or $isDotnetMsBuildNode)
        }

    foreach ($process in $processes) {
        Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
    }

    Start-Sleep -Seconds 2
}

function Invoke-AndroidPublish {
    param(
        [string]$ProjectPath,
        [string]$BuildConfiguration
    )

    dotnet publish $ProjectPath `
        -f net10.0-android `
        -c $BuildConfiguration `
        /nr:false `
        /p:UseSharedCompilation=false |
        Out-Host

    return [int]$LASTEXITCODE
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$adb = Resolve-AdbPath $AdbPath
$androidSdkRoot = Resolve-AndroidSdkRootFromAdb $adb
if ($androidSdkRoot -and (Test-Path $androidSdkRoot)) {
    $env:ANDROID_HOME = $androidSdkRoot
    $env:ANDROID_SDK_ROOT = $androidSdkRoot
}

Write-Host "1. Kiem tra thiet bi Android..."
$devices = & $adb devices
$onlineDevice = $devices | Select-String "`tdevice$" | Select-Object -First 1
if (-not $onlineDevice) {
    throw "Khong co emulator/USB device o trang thai 'device'. Mo emulator hoac bat USB debugging roi chay lai."
}
Write-Host "OK: $onlineDevice"

if (-not $SkipBuild) {
    Write-Host "2. Build APK Android..."
    Stop-DotnetBuildProcesses
    $publishExitCode = Invoke-AndroidPublish "$repoRoot\VinhKhanhTourDemo\VinhKhanhTourDemo.csproj" $Configuration
    if ($publishExitCode -ne 0) {
        Write-Host "Publish lan 1 loi, don lock build va thu lai..."
        Stop-DotnetBuildProcesses
        $publishExitCode = Invoke-AndroidPublish "$repoRoot\VinhKhanhTourDemo\VinhKhanhTourDemo.csproj" $Configuration
    }

    if ($publishExitCode -ne 0) {
        throw "dotnet publish failed with exit code $publishExitCode"
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

if ($RestartAppium) {
    Write-Host "Restart Appium server de nhan ANDROID_HOME/ANDROID_SDK_ROOT..."
    Stop-AppiumProcesses
    Start-Sleep -Seconds 2
}

$serverReady = $false
try {
    Wait-AppiumReady $AppiumServerUrl
    $serverReady = $true
    Write-Host "Appium dang chay: $AppiumServerUrl"
}
catch {
    Write-Host "Dang start Appium server..."
    if ([System.IO.Path]::GetExtension($appium) -ieq ".ps1") {
        $appiumProcess = Start-Process -FilePath "powershell" -ArgumentList "-ExecutionPolicy","Bypass","-File",$appium,"--base-path","/" -WindowStyle Hidden -PassThru
    }
    elseif ([System.IO.Path]::GetExtension($appium) -ieq ".cmd" -or [System.IO.Path]::GetExtension($appium) -ieq ".bat") {
        $appiumProcess = Start-Process -FilePath "cmd.exe" -ArgumentList "/c",$appium,"--base-path","/" -WindowStyle Hidden -PassThru
    }
    else {
        $appiumProcess = Start-Process -FilePath $appium -ArgumentList "--base-path","/" -WindowStyle Hidden -PassThru
    }
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

param(
    [string]$AdbPath = "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe",
    [int]$ApiPort = 5118
)

$ErrorActionPreference = "Stop"

if (!(Test-Path $AdbPath)) {
    throw "Khong tim thay adb tai: $AdbPath"
}

Write-Host "1. Kiem tra thiet bi USB..."
$devices = & $AdbPath devices
$onlineDevice = $devices | Select-String "`tdevice$"
if (-not $onlineDevice) {
    throw "Khong co thiet bi Android nao o trang thai 'device'."
}

Write-Host "2. Bat adb reverse tcp:$ApiPort -> tcp:$ApiPort ..."
& $AdbPath reverse "tcp:$ApiPort" "tcp:$ApiPort" | Out-Host

Write-Host "3. Kiem tra API local..."
$healthUrl = "http://127.0.0.1:$ApiPort/health"
$poiUrl = "http://127.0.0.1:$ApiPort/api/poi"

try {
    $health = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 5
    Write-Host "Health OK: $($health.StatusCode) $healthUrl"
}
catch {
    Write-Warning "Health endpoint loi, thu /api/poi ..."
    $poi = Invoke-WebRequest -Uri $poiUrl -UseBasicParsing -TimeoutSec 8
    Write-Host "POI OK: $($poi.StatusCode) $poiUrl"
}

Write-Host "4. Danh sach reverse hien tai:"
& $AdbPath reverse --list | Out-Host

Write-Host ""
Write-Host "San sang test USB. Hay dong app tren dien thoai, mo lai, roi thu lai luong thanh toan."

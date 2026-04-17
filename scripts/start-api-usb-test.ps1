param(
    [string]$AdbPath = "C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe",
    [int]$ApiPort = 5118
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProjectDir = Join-Path $repoRoot "VinhKhanhTour.API"
$apiDllPath = Join-Path $repoRoot ".codex-temp\api-build\VinhKhanhTour.API.dll"
$stdoutPath = Join-Path $repoRoot ".codex-temp\api-stdout.log"
$stderrPath = Join-Path $repoRoot ".codex-temp\api-stderr.log"

if (!(Test-Path $AdbPath)) {
    throw "Khong tim thay adb tai: $AdbPath"
}

Write-Host "1. Build API..."
dotnet build (Join-Path $apiProjectDir "VinhKhanhTour.API.csproj") | Out-Host

Write-Host "2. Giai phong port $ApiPort neu dang co tien trinh treo..."
$listeners = netstat -ano | Select-String ":$ApiPort"
foreach ($line in $listeners) {
    if ($line -match "LISTENING\s+(\d+)$") {
        $pid = [int]$matches[1]
        try {
            Stop-Process -Id $pid -Force -ErrorAction Stop
            Write-Host "Da dung PID $pid"
        }
        catch {
            Write-Warning "Khong dung duoc PID ${pid}: $($_.Exception.Message)"
        }
    }
}

if (Test-Path $stdoutPath) { Remove-Item -LiteralPath $stdoutPath -Force }
if (Test-Path $stderrPath) { Remove-Item -LiteralPath $stderrPath -Force }

Write-Host "3. Khoi dong API local..."
$apiProcess = Start-Process `
    -FilePath "dotnet" `
    -ArgumentList $apiDllPath, "--urls", "http://127.0.0.1:$ApiPort" `
    -WorkingDirectory $apiProjectDir `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath `
    -PassThru

Write-Host "API PID: $($apiProcess.Id)"

Write-Host "4. Cho API san sang..."
$ready = $false
for ($i = 0; $i -lt 12; $i++) {
    Start-Sleep -Seconds 1
    try {
        $response = Invoke-WebRequest -Uri "http://127.0.0.1:$ApiPort/health" -UseBasicParsing -TimeoutSec 3
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
            $ready = $true
            break
        }
    }
    catch {
    }

    if ($apiProcess.HasExited) {
        break
    }
}

if (-not $ready) {
    Write-Host "STDOUT:"
    if (Test-Path $stdoutPath) { Get-Content $stdoutPath | Out-Host }
    Write-Host "STDERR:"
    if (Test-Path $stderrPath) { Get-Content $stderrPath | Out-Host }
    throw "API local khong khoi dong thanh cong tren port $ApiPort."
}

Write-Host "5. Bat adb reverse..."
$devices = & $AdbPath devices
$onlineDevice = $devices | Select-String "`tdevice$"
if (-not $onlineDevice) {
    throw "Khong co thiet bi Android nao o trang thai 'device'."
}

& $AdbPath reverse "tcp:$ApiPort" "tcp:$ApiPort" | Out-Host
& $AdbPath reverse --list | Out-Host

Write-Host ""
Write-Host "San sang test USB."
Write-Host "Hay mo app tren dien thoai va thu lai luong gia han/thanh toan."

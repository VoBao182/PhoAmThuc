param(
    [switch]$WithAppium,
    [string]$CmsUrl = "http://127.0.0.1:5199",
    [string]$LogPath,
    [switch]$NoRestartCms,
    [int]$CmsPort = 5213
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$script:cmsProcess = $null
$script:transcriptStarted = $false

function Invoke-Step {
    param(
        [string]$Name,
        [scriptblock]$Command
    )

    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan
    $global:LASTEXITCODE = 0
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

function Invoke-External {
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$Executable,

        [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Executable $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Stop-RepoDotnetAppProcesses {
    if (-not ($IsWindows -or $env:OS -eq "Windows_NT")) {
        return
    }

    $normalizedRepoRoot = $repoRoot.TrimEnd('\')
    $projectMarkers = @(
        "VinhKhanhTourDemo\api-usb-build\VinhKhanhTour.API.dll",
        "VinhKhanhTourDemo\cms-local-build\VinhKhanhTour.CMS.dll",
        "VinhKhanhTour.API\bin\Debug\net10.0\VinhKhanhTour.API.dll",
        "VinhKhanhTour.CMS\bin\Debug\net10.0\VinhKhanhTour.CMS.dll",
        "VinhKhanhTour.API\VinhKhanhTour.API.csproj",
        "VinhKhanhTour.CMS\VinhKhanhTour.CMS.csproj"
    )

    $processes = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $commandLine = $_.CommandLine
            if ([string]::IsNullOrWhiteSpace($commandLine)) {
                return $false
            }

            if ($commandLine -notlike "*$normalizedRepoRoot*") {
                return $false
            }

            foreach ($marker in $projectMarkers) {
                if ($commandLine -like "*$marker*") {
                    return $true
                }
            }

            return $false
        }

    foreach ($process in $processes) {
        try {
            Write-Host "Stopping stale local app process PID $($process.ProcessId) before building..."
            Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
        }
        catch {
            Write-Warning "Could not stop PID $($process.ProcessId): $($_.Exception.Message)"
        }
    }
}

function Stop-DotnetBuildServers {
    dotnet build-server shutdown | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "dotnet build-server shutdown returned exit code $LASTEXITCODE."
        $global:LASTEXITCODE = 0
    }
}

function Start-CmsLocalForDev {
    if ($NoRestartCms) {
        return
    }

    if (-not ($IsWindows -or $env:OS -eq "Windows_NT")) {
        return
    }

    $scriptPath = Join-Path $repoRoot "scripts\start-cms-local.ps1"
    if (-not (Test-Path -LiteralPath $scriptPath)) {
        Write-Warning "Could not restart CMS local because start-cms-local.ps1 was not found."
        return
    }

    $testResultsDir = Join-Path $repoRoot "TestResults"
    New-Item -ItemType Directory -Force -Path $testResultsDir | Out-Null
    $stdoutPath = Join-Path $testResultsDir "cms-local-after-tests.out.log"
    $stderrPath = Join-Path $testResultsDir "cms-local-after-tests.err.log"
    Remove-Item -LiteralPath $stdoutPath, $stderrPath -Force -ErrorAction SilentlyContinue

    $process = Start-Process `
        -FilePath "powershell" `
        -ArgumentList @("-ExecutionPolicy", "Bypass", "-File", $scriptPath, "-CmsPort", "$CmsPort") `
        -WorkingDirectory $repoRoot `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -WindowStyle Hidden `
        -PassThru

    Write-Host "Restarting CMS local after tests on http://localhost:$CmsPort (launcher PID $($process.Id))."
}

function Unblock-BuildOutputs {
    if (-not $IsWindows -and $env:OS -ne "Windows_NT") {
        return
    }

    Get-ChildItem -Path $repoRoot -Recurse -File -Include *.dll,*.exe |
        Where-Object {
            $_.FullName -like "*\bin\*" -or
            $_.FullName -like "*\obj\*"
        } |
        ForEach-Object {
            Unblock-File -LiteralPath $_.FullName -ErrorAction SilentlyContinue
        }
}

try {
    Push-Location $repoRoot
    $testResultsDir = Join-Path $repoRoot "TestResults"
    $artifactDir = Join-Path $testResultsDir "artifacts"
    New-Item -ItemType Directory -Force -Path $testResultsDir, $artifactDir | Out-Null
    $env:TEST_ARTIFACT_DIR = $artifactDir
    $env:TEST_REPO_ROOT = $repoRoot

    if ([string]::IsNullOrWhiteSpace($LogPath)) {
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $LogPath = Join-Path $testResultsDir "run-all-tests-$timestamp.log"
    }

    $resolvedLogPath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($LogPath)
    $resolvedLogDir = Split-Path -Parent $resolvedLogPath
    New-Item -ItemType Directory -Force -Path $resolvedLogDir | Out-Null
    Start-Transcript -Path $resolvedLogPath -Force | Out-Null
    $script:transcriptStarted = $true

    Write-Host "Automation test log: $resolvedLogPath" -ForegroundColor Green

    Invoke-Step "Clean stale local app processes" {
        Stop-RepoDotnetAppProcesses
        Stop-DotnetBuildServers
    }

    Invoke-Step "Restore packages" {
        Invoke-External dotnet restore .\VinhKhanhTourDemo.slnx
    }

    Invoke-Step "Build projects used by automated tests" {
        Invoke-External dotnet build .\VinhKhanhTour.API\VinhKhanhTour.API.csproj --no-restore
        Invoke-External dotnet build .\VinhKhanhTour.CMS\VinhKhanhTour.CMS.csproj --no-restore
        Invoke-External dotnet build .\tests\VinhKhanhTour.API.Tests\VinhKhanhTour.API.Tests.csproj --no-restore
        Invoke-External dotnet build .\tests\VinhKhanhTour.CMS.E2ETests\VinhKhanhTour.CMS.E2ETests.csproj --no-restore
        Invoke-External dotnet build .\tests\VinhKhanhTour.MAUI.AppiumTests\VinhKhanhTour.MAUI.AppiumTests.csproj --no-restore
        Unblock-BuildOutputs
    }

    Invoke-Step "Install Playwright Chromium" {
        Invoke-External dotnet build .\tests\VinhKhanhTour.CMS.E2ETests\VinhKhanhTour.CMS.E2ETests.csproj --no-restore
        $playwrightSearchRoots = @(
            (Join-Path $repoRoot "tests\VinhKhanhTour.CMS.E2ETests"),
            (Join-Path $env:LOCALAPPDATA "VinhKhanhTourDemo\bin\VinhKhanhTour.CMS.E2ETests")
        )
        $playwrightScript = Get-ChildItem -Path $playwrightSearchRoots -Recurse -Filter "playwright.ps1" -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -like "*VinhKhanhTour.CMS.E2ETests*" } |
            Select-Object -First 1 -ExpandProperty FullName

        if ([string]::IsNullOrWhiteSpace($playwrightScript)) {
            throw "Could not find Playwright install script for CMS E2E tests."
        }

        Invoke-External powershell -ExecutionPolicy Bypass -File $playwrightScript install chromium
    }

    Invoke-Step "API tests" {
        Invoke-External dotnet test .\tests\VinhKhanhTour.API.Tests\VinhKhanhTour.API.Tests.csproj --no-build --logger "trx;LogFileName=api-tests.trx" --results-directory $testResultsDir
    }

    Invoke-Step "CMS Playwright tests" {
        Remove-Item Env:\CMS_BASE_URL -ErrorAction SilentlyContinue
        Invoke-External dotnet test .\tests\VinhKhanhTour.CMS.E2ETests\VinhKhanhTour.CMS.E2ETests.csproj --no-build --logger "trx;LogFileName=cms-e2e-tests.trx" --results-directory $testResultsDir
    }

    Invoke-Step "MAUI Appium tests" {
        if ($WithAppium) {
            $env:RUN_APPIUM_TESTS = "1"
        }
        else {
            Remove-Item Env:\RUN_APPIUM_TESTS -ErrorAction SilentlyContinue
        }

        Invoke-External dotnet test .\tests\VinhKhanhTour.MAUI.AppiumTests\VinhKhanhTour.MAUI.AppiumTests.csproj --no-build --logger "trx;LogFileName=maui-appium-tests.trx" --results-directory $testResultsDir
    }
}
finally {
    if ($script:cmsProcess -and -not $script:cmsProcess.HasExited) {
        Stop-Process -Id $script:cmsProcess.Id -Force
    }
    Start-CmsLocalForDev
    if ($script:transcriptStarted) {
        Stop-Transcript | Out-Null
    }
    Pop-Location
}

param(
    [switch]$WithAppium,
    [string]$CmsUrl = "http://127.0.0.1:5199",
    [string]$LogPath
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
    if ($script:transcriptStarted) {
        Stop-Transcript | Out-Null
    }
    Pop-Location
}

param(
    [switch]$WithAppium,
    [string]$CmsUrl = "http://127.0.0.1:5199"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$script:cmsProcess = $null

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

try {
    Push-Location $repoRoot
    $testResultsDir = Join-Path $repoRoot "TestResults"
    $artifactDir = Join-Path $testResultsDir "artifacts"
    New-Item -ItemType Directory -Force -Path $testResultsDir, $artifactDir | Out-Null
    $env:TEST_ARTIFACT_DIR = $artifactDir

    Invoke-Step "Restore packages" {
        Invoke-External dotnet restore .\VinhKhanhTourDemo.slnx
    }

    Invoke-Step "Build projects used by automated tests" {
        Invoke-External dotnet build .\VinhKhanhTour.API\VinhKhanhTour.API.csproj --no-restore
        Invoke-External dotnet build .\VinhKhanhTour.CMS\VinhKhanhTour.CMS.csproj --no-restore
        Invoke-External dotnet build .\tests\VinhKhanhTour.API.Tests\VinhKhanhTour.API.Tests.csproj --no-restore
        Invoke-External dotnet build .\tests\VinhKhanhTour.CMS.E2ETests\VinhKhanhTour.CMS.E2ETests.csproj --no-restore
        Invoke-External dotnet build .\tests\VinhKhanhTour.MAUI.AppiumTests\VinhKhanhTour.MAUI.AppiumTests.csproj --no-restore
    }

    Invoke-Step "Install Playwright Chromium" {
        Invoke-External dotnet build .\tests\VinhKhanhTour.CMS.E2ETests\VinhKhanhTour.CMS.E2ETests.csproj --no-restore
        $playwrightScript = Join-Path $repoRoot "tests\VinhKhanhTour.CMS.E2ETests\bin\Debug\net10.0\playwright.ps1"
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
    Pop-Location
}

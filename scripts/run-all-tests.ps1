param(
    [switch]$WithAppium,
    [string]$CmsUrl = "http://127.0.0.1:5199"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$connectionString = "Host=localhost;Port=5432;Database=vinhkhanhtour_test;Username=postgres;Password=postgres;Timeout=1;Command Timeout=1;Pooling=false"
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
        Invoke-External dotnet test .\tests\VinhKhanhTour.API.Tests\VinhKhanhTour.API.Tests.csproj --no-build
    }

    Invoke-Step "Start CMS for Playwright" {
        try {
            Invoke-WebRequest -Uri "$($CmsUrl.TrimEnd('/'))/health" -UseBasicParsing -TimeoutSec 2 | Out-Null
            Write-Host "CMS is already running at $CmsUrl; reusing it for Playwright."
            return
        }
        catch {
            Write-Host "Starting CMS at $CmsUrl."
        }

        $envVars = @{
            "ASPNETCORE_URLS" = $CmsUrl
            "ASPNETCORE_ENVIRONMENT" = "Testing"
            "SUPABASE_CONNECTION_STRING" = $connectionString
            "Logging__LogLevel__Default" = "Warning"
            "Logging__LogLevel__Microsoft.AspNetCore" = "Warning"
            "Logging__LogLevel__Microsoft.Hosting.Lifetime" = "Warning"
        }

        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = "dotnet"
        $cmsProject = Join-Path $repoRoot "VinhKhanhTour.CMS\VinhKhanhTour.CMS.csproj"
        $startInfo.Arguments = "run --no-launch-profile --project `"$cmsProject`" --no-build"
        $startInfo.WorkingDirectory = $repoRoot
        $startInfo.UseShellExecute = $false
        foreach ($key in $envVars.Keys) {
            $startInfo.Environment[$key] = $envVars[$key]
        }

        $script:cmsProcess = [System.Diagnostics.Process]::Start($startInfo)
        $deadline = (Get-Date).AddSeconds(45)
        do {
            Start-Sleep -Milliseconds 700
            try {
                Invoke-WebRequest -Uri "$($CmsUrl.TrimEnd('/'))/health" -UseBasicParsing -TimeoutSec 3 | Out-Null
                return
            }
            catch {
                if ($script:cmsProcess.HasExited) {
                    throw "CMS process exited before it became ready."
                }
            }
        } while ((Get-Date) -lt $deadline)

        throw "CMS did not become ready at $CmsUrl."
    }

    Invoke-Step "CMS Playwright tests" {
        $env:CMS_BASE_URL = $CmsUrl
        Invoke-External dotnet test .\tests\VinhKhanhTour.CMS.E2ETests\VinhKhanhTour.CMS.E2ETests.csproj --no-build
    }

    Invoke-Step "MAUI Appium tests" {
        if ($WithAppium) {
            $env:RUN_APPIUM_TESTS = "1"
        }
        else {
            Remove-Item Env:\RUN_APPIUM_TESTS -ErrorAction SilentlyContinue
        }

        Invoke-External dotnet test .\tests\VinhKhanhTour.MAUI.AppiumTests\VinhKhanhTour.MAUI.AppiumTests.csproj --no-build
    }
}
finally {
    if ($script:cmsProcess -and -not $script:cmsProcess.HasExited) {
        Stop-Process -Id $script:cmsProcess.Id -Force
    }
    Pop-Location
}

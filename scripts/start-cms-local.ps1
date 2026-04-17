param(
    [string]$SupabaseConnectionString,
    [switch]$ForgetSavedConnectionString
)

$ErrorActionPreference = "Stop"

function ConvertTo-PlainText {
    param(
        [Parameter(Mandatory = $true)]
        [Security.SecureString]$SecureValue
    )

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        if ($bstr -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
        }
    }
}

function Get-StoredConnectionString {
    $processValue = [Environment]::GetEnvironmentVariable("SUPABASE_CONNECTION_STRING", "Process")
    if (-not [string]::IsNullOrWhiteSpace($processValue)) {
        return $processValue
    }

    $userValue = [Environment]::GetEnvironmentVariable("SUPABASE_CONNECTION_STRING", "User")
    if (-not [string]::IsNullOrWhiteSpace($userValue)) {
        return $userValue
    }

    $machineValue = [Environment]::GetEnvironmentVariable("SUPABASE_CONNECTION_STRING", "Machine")
    if (-not [string]::IsNullOrWhiteSpace($machineValue)) {
        return $machineValue
    }

    return $null
}

if ($ForgetSavedConnectionString) {
    [Environment]::SetEnvironmentVariable("SUPABASE_CONNECTION_STRING", $null, "User")
    Write-Host "Da xoa SUPABASE_CONNECTION_STRING luu o muc User."
    return
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$cmsProjectPath = Join-Path $repoRoot "VinhKhanhTour.CMS\VinhKhanhTour.CMS.csproj"

if (-not (Test-Path -LiteralPath $cmsProjectPath)) {
    throw "Khong tim thay project CMS tai: $cmsProjectPath"
}

if ([string]::IsNullOrWhiteSpace($SupabaseConnectionString)) {
    $SupabaseConnectionString = Get-StoredConnectionString
}

if ([string]::IsNullOrWhiteSpace($SupabaseConnectionString)) {
    Write-Host "Chua tim thay SUPABASE_CONNECTION_STRING tren may nay."
    Write-Host "Hay dan full connection string Supabase. Noi dung se khong hien tren man hinh."
    $secureValue = Read-Host "SUPABASE_CONNECTION_STRING" -AsSecureString
    $SupabaseConnectionString = ConvertTo-PlainText -SecureValue $secureValue

    if ([string]::IsNullOrWhiteSpace($SupabaseConnectionString)) {
        throw "Ban chua nhap SUPABASE_CONNECTION_STRING."
    }

    $rememberAnswer = (Read-Host "Luu vao User env de lan sau chi can chay .\\scripts\\start-cms-local.ps1 ? (y/N)").Trim().ToLowerInvariant()
    if ($rememberAnswer -eq "y" -or $rememberAnswer -eq "yes") {
        [Environment]::SetEnvironmentVariable("SUPABASE_CONNECTION_STRING", $SupabaseConnectionString, "User")
        Write-Host "Da luu SUPABASE_CONNECTION_STRING vao User env."
    }
}

$env:SUPABASE_CONNECTION_STRING = $SupabaseConnectionString
$env:ASPNETCORE_ENVIRONMENT = "Development"

Write-Host "Dang chay CMS local..."
Write-Host "API hien dang tro toi https://phoamthuc.onrender.com"
Write-Host "Dung Ctrl+C de dung CMS."

dotnet run --project $cmsProjectPath

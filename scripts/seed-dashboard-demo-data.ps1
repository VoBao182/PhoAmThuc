param(
    [string]$ConnectionString,
    [string]$SqlPath
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($SqlPath)) {
    $SqlPath = Join-Path $PSScriptRoot "seed-dashboard-demo-data.sql"
}

$SqlPath = (Resolve-Path -LiteralPath $SqlPath).Path

function Get-LocalConnectionString {
    $candidateFiles = @(
        (Join-Path $repoRoot "VinhKhanhTour.API\appsettings.Development.Local.json"),
        (Join-Path $repoRoot "VinhKhanhTour.CMS\appsettings.Development.Local.json"),
        (Join-Path $repoRoot "VinhKhanhTour.API\appsettings.Local.json"),
        (Join-Path $repoRoot "VinhKhanhTour.CMS\appsettings.Local.json")
    )

    foreach ($file in $candidateFiles) {
        if (-not (Test-Path -LiteralPath $file)) {
            continue
        }

        $json = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
        $value = $json.ConnectionStrings.DefaultConnection
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }
    }

    return $null
}

function Assert-ConnectionString {
    param([Parameter(Mandatory = $true)][string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        throw "Missing database connection string. Pass -ConnectionString, set SUPABASE_CONNECTION_STRING, or configure appsettings.Development.Local.json."
    }

    if ($Value -match "YOUR_|YOUR-|PROJECT_REF|YOUR_NEW_PASSWORD") {
        throw "The database connection string still looks like a placeholder."
    }
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = $env:SUPABASE_CONNECTION_STRING
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = Get-LocalConnectionString
}

Assert-ConnectionString -Value $ConnectionString

$runnerDir = Join-Path $repoRoot ".codex-temp\SeedDashboardData"
New-Item -ItemType Directory -Force -Path $runnerDir | Out-Null

$csprojPath = Join-Path $runnerDir "SeedDashboardData.csproj"
$programPath = Join-Path $runnerDir "Program.cs"
$localOutputRoot = Join-Path $env:LOCALAPPDATA "VinhKhanhTourDemo\SeedDashboardData"
$escapedOutputRoot = $localOutputRoot.Replace('\', '\\')
$dotnetHome = Join-Path $repoRoot ".codex-temp\dotnet-home"
New-Item -ItemType Directory -Force -Path $dotnetHome | Out-Null

$npgsqlPackage = Get-ChildItem -LiteralPath (Join-Path $dotnetHome ".nuget\packages\npgsql") -Directory -ErrorAction SilentlyContinue |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "lib\net10.0\Npgsql.dll") } |
    Sort-Object Name -Descending |
    Select-Object -First 1

if ($null -eq $npgsqlPackage) {
    throw "Could not find a local Npgsql package. Build the API project once, then rerun this script."
}

$npgsqlVersion = $npgsqlPackage.Name

@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseAppHost>false</UseAppHost>
    <OutputPath>$escapedOutputRoot\\bin\\</OutputPath>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Npgsql" Version="$npgsqlVersion" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $csprojPath -Encoding UTF8

@'
using Npgsql;

var connectionString = Environment.GetEnvironmentVariable("VKT_SEED_CONNECTION_STRING");
var sqlPath = Environment.GetEnvironmentVariable("VKT_SEED_SQL_PATH");

if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Missing VKT_SEED_CONNECTION_STRING.");

if (string.IsNullOrWhiteSpace(sqlPath) || !File.Exists(sqlPath))
    throw new FileNotFoundException("SQL seed file was not found.", sqlPath);

var builder = new NpgsqlConnectionStringBuilder(connectionString);

if (builder.MaxPoolSize == 0 || builder.MaxPoolSize > 2)
    builder.MaxPoolSize = 2;

if (builder.MinPoolSize > 0)
    builder.MinPoolSize = 0;

if (builder.Timeout == 0 || builder.Timeout > 15)
    builder.Timeout = 15;

if (builder.CommandTimeout == 0 || builder.CommandTimeout > 120)
    builder.CommandTimeout = 120;

var sql = await File.ReadAllTextAsync(sqlPath);

await using var connection = new NpgsqlConnection(builder.ConnectionString);
await connection.OpenAsync();

await using var command = new NpgsqlCommand(sql, connection)
{
    CommandTimeout = 120
};

await using var reader = await command.ExecuteReaderAsync();
var resultSet = 0;

do
{
    if (reader.FieldCount == 0)
        continue;

    resultSet++;
    Console.WriteLine();
    Console.WriteLine($"Result set {resultSet}");

    var headers = Enumerable.Range(0, reader.FieldCount)
        .Select(reader.GetName)
        .ToArray();
    Console.WriteLine(string.Join(" | ", headers));

    while (await reader.ReadAsync())
    {
        var values = new object[reader.FieldCount];
        reader.GetValues(values);
        Console.WriteLine(string.Join(" | ", values.Select(value => value switch
        {
            null => "",
            DBNull => "",
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
            decimal d => d.ToString("0"),
            _ => value.ToString()
        })));
    }
}
while (await reader.NextResultAsync());

Console.WriteLine();
Console.WriteLine("Dashboard demo seed completed.");
'@ | Set-Content -LiteralPath $programPath -Encoding UTF8

$previousDotnetHome = $env:DOTNET_CLI_HOME
$previousSkipFirstTime = $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE
$previousTelemetry = $env:DOTNET_CLI_TELEMETRY_OPTOUT
$previousSeedConnection = $env:VKT_SEED_CONNECTION_STRING
$previousSeedSql = $env:VKT_SEED_SQL_PATH

try {
    $env:DOTNET_CLI_HOME = $dotnetHome
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
    $env:VKT_SEED_CONNECTION_STRING = $ConnectionString
    $env:VKT_SEED_SQL_PATH = $SqlPath

    dotnet restore $csprojPath --ignore-failed-sources
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE."
    }

    dotnet run --project $csprojPath --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet seed runner failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:DOTNET_CLI_HOME = $previousDotnetHome
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = $previousSkipFirstTime
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = $previousTelemetry
    $env:VKT_SEED_CONNECTION_STRING = $previousSeedConnection
    $env:VKT_SEED_SQL_PATH = $previousSeedSql
}

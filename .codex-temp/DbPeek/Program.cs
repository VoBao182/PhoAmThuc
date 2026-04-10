using Npgsql;
using System.Security.Cryptography.X509Certificates;

var password = "Nikonchipbao182.";
var candidates = new[]
{
    ("direct", "Host=db.gdehuskbrhfswvrkfzkl.supabase.co;Database=postgres;Username=postgres;Password=" + password + ";SSL Mode=Require;Trust Server Certificate=true")
};

foreach (var (name, cs) in candidates)
{
    try
    {
        await using var conn = new NpgsqlConnection(cs);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand("select count(*)::text from public.poi;", conn);
        var count = (string?)await cmd.ExecuteScalarAsync();
        Console.WriteLine($"{name}: OK poi_count={count}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{name}: FAIL {ex}");
    }
}

var regions = new[]
{
    "ap-southeast-1",
    "ap-southeast-2",
    "ap-east-1",
    "ap-south-1",
    "ap-northeast-1",
    "ap-northeast-2",
    "ca-central-1",
    "eu-central-1",
    "eu-north-1",
    "eu-west-1",
    "eu-west-2",
    "eu-west-3",
    "me-central-1",
    "us-east-1",
    "us-east-2",
    "us-west-1",
    "us-west-2",
    "sa-east-1"
};

foreach (var region in regions)
{
    var builderCs = $"Host=aws-0-{region}.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.gdehuskbrhfswvrkfzkl;Password={password};SSL Mode=Require;Gss Encryption Mode=Disable";

    try
    {
        var builder = new NpgsqlDataSourceBuilder(builderCs);
        builder.UseSslClientAuthenticationOptionsCallback(options =>
        {
            options.ClientCertificates = [];
            options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
            options.CertificateRevocationCheckMode = X509RevocationMode.NoCheck;
        });

        await using var dataSource = builder.Build();
        await using var conn = await dataSource.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("select count(*)::text from public.poi;", conn);
        var count = (string?)await cmd.ExecuteScalarAsync();
        Console.WriteLine($"region:{region}: OK poi_count={count}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"region:{region}: FAIL {ex.GetBaseException().Message}");
    }
}

using Microsoft.Extensions.Configuration;
using Npgsql;

var config = new ConfigurationBuilder()
    .SetBasePath(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "VinhKhanhTour.API")))
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddJsonFile("appsettings.Development.Local.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
    ?? config.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Missing connection string.");

await using var conn = new NpgsqlConnection(connectionString);
await conn.OpenAsync();

await PrintQueryAsync(
    conn,
    "Active POI visible to /api/poi filter",
    """
    select id::text,
           tenpoi,
           diachi,
           vido,
           kinhdo,
           trangthai,
           ngayhethanduytri,
           count(*) over (partition by round(vido::numeric, 6), round(kinhdo::numeric, 6)) as same_coord_count
    from poi
    where trangthai = true
      and (ngayhethanduytri is null or ngayhethanduytri > now())
    order by mucuutien, tenpoi;
    """);

await PrintQueryAsync(
    conn,
    "All active POI regardless maintenance expiry",
    """
    select id::text,
           tenpoi,
           diachi,
           vido,
           kinhdo,
           trangthai,
           ngayhethanduytri,
           case
             when ngayhethanduytri is null then 'visible_null_expiry'
             when ngayhethanduytri > now() then 'visible_paid'
             else 'hidden_expired'
           end as api_visibility
    from poi
    where trangthai = true
    order by api_visibility, mucuutien, tenpoi;
    """);

await PrintQueryAsync(
    conn,
    "Device 46391152 matching IDs",
    """
    select source, mathietbi, count(*) as rows
    from (
        select 'lichsuphat' as source, mathietbi from lichsuphat where mathietbi ilike '46391152%'
        union all
        select 'vitrikhach' as source, mathietbi from vitrikhach where mathietbi ilike '46391152%'
        union all
        select 'dangkyapp' as source, mathietbi from dangkyapp where mathietbi ilike '46391152%'
    ) x
    group by source, mathietbi
    order by source, mathietbi;
    """);

await PrintQueryAsync(
    conn,
    "Device 46391152 POI activity",
    """
    select coalesce(l.nguon, '') as nguon,
           l.poiid::text,
           coalesce(p.tenpoi, '(missing poi)') as tenpoi,
           count(*) as rows,
           min(l.thoigian) as first_seen,
           max(l.thoigian) as last_seen
    from lichsuphat l
    left join poi p on p.id = l.poiid
    where l.mathietbi ilike '46391152%'
      and l.poiid is not null
    group by coalesce(l.nguon, ''), l.poiid, coalesce(p.tenpoi, '(missing poi)')
    order by tenpoi, nguon;
    """);

static async Task PrintQueryAsync(NpgsqlConnection conn, string title, string sql)
{
    Console.WriteLine();
    Console.WriteLine($"## {title}");

    await using var cmd = new NpgsqlCommand(sql, conn)
    {
        CommandTimeout = 60
    };

    await using var reader = await cmd.ExecuteReaderAsync();
    var names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
    Console.WriteLine(string.Join(" | ", names));

    while (await reader.ReadAsync())
    {
        var values = new object?[reader.FieldCount];
        reader.GetValues(values);
        Console.WriteLine(string.Join(" | ", values.Select(v => v switch
        {
            null => "",
            DBNull => "",
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
            _ => v.ToString()
        })));
    }
}

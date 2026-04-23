using Npgsql;

var connStr = "Host=aws-1-ap-south-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.gdehuskbrhfswvrkfzkl;Password=VinhKhanhTour.;SSL Mode=Require;Trust Server Certificate=true";

var poiIds = new[]
{
    Guid.Parse("6d55af23-0791-4381-af61-50242a14a6e5"), // Bảo ký
    Guid.Parse("526c4561-adda-4883-8ec3-44c72ff6085a"), // Bảo ký
    Guid.Parse("1c780dee-90ba-42fe-9242-491e9c2a5975"), // Kỳ ký
};

await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();
Console.WriteLine("=== DRY-RUN: Khao sat cac hang bi anh huong (KHONG XOA) ===\n");

await Count(conn, "poi",            "SELECT COUNT(*) FROM poi WHERE id = ANY(@ids)",                                                         poiIds);
await Count(conn, "monan",          "SELECT COUNT(*) FROM monan WHERE poiid = ANY(@ids)",                                                   poiIds);
await Count(conn, "thuyetminh",     "SELECT COUNT(*) FROM thuyetminh WHERE poiid = ANY(@ids)",                                              poiIds);
await Count(conn, "bandich (con)",  "SELECT COUNT(*) FROM bandich WHERE thuyetminhid IN (SELECT id FROM thuyetminh WHERE poiid = ANY(@ids))", poiIds);
await Count(conn, "hoadon",         "SELECT COUNT(*) FROM hoadon WHERE poiid = ANY(@ids)",                                                  poiIds);
await Count(conn, "dangkydichvu",   "SELECT COUNT(*) FROM dangkydichvu WHERE poiid = ANY(@ids)",                                            poiIds);
await Count(conn, "lichsuphat (poiid)",       "SELECT COUNT(*) FROM lichsuphat WHERE poiid = ANY(@ids)",                                    poiIds);
await Count(conn, "lichsuphat (thuyetminhid)","SELECT COUNT(*) FROM lichsuphat WHERE thuyetminhid IN (SELECT id FROM thuyetminh WHERE poiid = ANY(@ids))", poiIds);
await Count(conn, "vitrikhach",     "SELECT COUNT(*) FROM vitrikhach WHERE poiid_hientai = ANY(@ids)",                                      poiIds);
await Count(conn, "taikhoan",       "SELECT COUNT(*) FROM taikhoan WHERE poiid = ANY(@ids)",                                                poiIds);

Console.WriteLine("\nCac FK tim thay trong pg_constraint (de kiem tra cascade behavior):");
var fkSql = @"
SELECT conrelid::regclass AS tbl, conname, pg_get_constraintdef(oid) AS def
FROM pg_constraint
WHERE contype = 'f'
  AND (confrelid = 'poi'::regclass OR confrelid = 'thuyetminh'::regclass)
ORDER BY conrelid::regclass::text;";

await using (var cmd = new NpgsqlCommand(fkSql, conn))
await using (var rdr = await cmd.ExecuteReaderAsync())
{
    while (await rdr.ReadAsync())
        Console.WriteLine($"  [{rdr.GetValue(0)}] {rdr.GetString(1)}: {rdr.GetString(2)}");
}

static async Task Count(NpgsqlConnection conn, string label, string sql, Guid[] ids)
{
    await using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("ids", ids);
    var n = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
    Console.WriteLine($"  {label,-28}: {n} hang");
}

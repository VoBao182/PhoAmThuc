using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.Local.json",
        optional: true,
        reloadOnChange: true);

var connectionString = GetConnectionString(builder.Configuration);

builder.Services.AddRazorPages();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        npgsqlOptions => npgsqlOptions.ExecutionStrategy(deps => new ResilientExecutionStrategy(deps))));

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/db", async (AppDbContext db) =>
{
    try
    {
        await db.Database.OpenConnectionAsync();
        return Results.Ok(new { status = "ok", database = "connected" });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            ex.GetBaseException().Message,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Database connection failed");
    }
    finally
    {
        await db.Database.CloseConnectionAsync();
    }
});
app.MapRazorPages();
app.Run();

static string GetConnectionString(ConfigurationManager configuration)
{
    var connectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
        ?? configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connectionString) || LooksLikePlaceholder(connectionString))
    {
        throw new InvalidOperationException(
            "Missing Supabase database connection string. In Visual Studio, set SUPABASE_CONNECTION_STRING or create appsettings.Development.Local.json with ConnectionStrings:DefaultConnection from Supabase.");
    }

    return connectionString;
}

static bool LooksLikePlaceholder(string connectionString)
{
    return connectionString.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("YOUR-", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("PROJECT_REF", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("YOUR_NEW_PASSWORD", StringComparison.OrdinalIgnoreCase);
}

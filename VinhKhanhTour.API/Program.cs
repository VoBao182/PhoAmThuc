using Microsoft.EntityFrameworkCore;
using Npgsql;
using VinhKhanhTour.API.Data;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Critical);
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName}.Local.json",
        optional: true,
        reloadOnChange: true);

var connectionString = GetConnectionString(builder.Configuration, builder.Environment);
var port = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Kết nối Supabase
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        npgsqlOptions => npgsqlOptions.ExecutionStrategy(deps => new ResilientExecutionStrategy(deps))));

// Cho phép MAUI app gọi API
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Phục vụ ảnh tải lên từ wwwroot/uploads
var uploadsPath = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles();

app.UseCors("AllowAll");
app.UseAuthorization();
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
app.MapControllers();
app.Run();

static string GetConnectionString(ConfigurationManager configuration, IHostEnvironment environment)
{
    var connectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
        ?? configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connectionString) || LooksLikePlaceholder(connectionString))
    {
        throw new InvalidOperationException(
            "Missing Supabase database connection string. Set SUPABASE_CONNECTION_STRING or create appsettings.Development.Local.json with ConnectionStrings:DefaultConnection from Supabase.");
    }

    return ConfigureConnectionString(connectionString, configuration, environment);
}

static string ConfigureConnectionString(
    string connectionString,
    ConfigurationManager configuration,
    IHostEnvironment environment)
{
    var builder = new NpgsqlConnectionStringBuilder(connectionString);

    if (environment.IsDevelopment() &&
        configuration.GetValue<bool>("Database:DisableSslForLocalDev"))
    {
        builder.SslMode = SslMode.Disable;
    }

    // Stay below Supabase pooler's session-mode client cap. Free tier allocates a small
    // number of slots per pooler role, shared between API and CMS. Without an explicit cap,
    // Npgsql may hold 100 idle slots and trigger "MaxClientsInSessionMode" under load.
    if (builder.MaxPoolSize > 6 || builder.MaxPoolSize == 0)
        builder.MaxPoolSize = 6;

    if (builder.MinPoolSize > 0)
        builder.MinPoolSize = 0;

    if (builder.ConnectionIdleLifetime == 0 || builder.ConnectionIdleLifetime > 30)
        builder.ConnectionIdleLifetime = 30;

    if (builder.ConnectionPruningInterval == 0 || builder.ConnectionPruningInterval > 10)
        builder.ConnectionPruningInterval = 10;

    return builder.ConnectionString;
}

static bool LooksLikePlaceholder(string connectionString)
{
    return connectionString.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("YOUR-", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("PROJECT_REF", StringComparison.OrdinalIgnoreCase)
        || connectionString.Contains("YOUR_NEW_PASSWORD", StringComparison.OrdinalIgnoreCase);
}

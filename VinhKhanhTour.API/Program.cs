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
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()));

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

static string GetConnectionString(ConfigurationManager configuration)
{
    var connectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
        ?? configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrWhiteSpace(connectionString) || LooksLikePlaceholder(connectionString))
    {
        throw new InvalidOperationException(
            "Missing Supabase database connection string. Set SUPABASE_CONNECTION_STRING or create appsettings.Development.Local.json with ConnectionStrings:DefaultConnection from Supabase.");
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

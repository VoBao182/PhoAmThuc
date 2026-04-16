using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;

var builder = WebApplication.CreateBuilder(args);
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
app.MapControllers();
app.Run();

static string GetConnectionString(ConfigurationManager configuration)
{
    return Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING")
        ?? configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Missing database connection string. Set SUPABASE_CONNECTION_STRING or ConnectionStrings:DefaultConnection.");
}

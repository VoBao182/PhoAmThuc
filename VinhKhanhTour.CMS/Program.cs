using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()));

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.Run();

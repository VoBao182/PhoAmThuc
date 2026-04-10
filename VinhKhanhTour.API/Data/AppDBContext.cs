using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<POI>       POIs        { get; set; }
    public DbSet<ThuyetMinh> ThuyetMinhs { get; set; }
    public DbSet<BanDich>   BanDichs    { get; set; }
    public DbSet<MonAn>     MonAns      { get; set; }
    public DbSet<LichSuPhat> LichSuPhats { get; set; }
    public DbSet<TaiKhoan>  TaiKhoans   { get; set; }  // ← thêm mới

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<POI>().ToTable("poi");
        modelBuilder.Entity<ThuyetMinh>().ToTable("thuyetminh");
        modelBuilder.Entity<BanDich>().ToTable("bandich");
        modelBuilder.Entity<MonAn>().ToTable("monan");
        modelBuilder.Entity<LichSuPhat>().ToTable("lichsuphat");
        modelBuilder.Entity<TaiKhoan>().ToTable("taikhoan");  // ← thêm mới
    }
}

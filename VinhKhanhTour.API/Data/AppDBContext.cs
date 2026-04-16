using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<POI>           POIs           { get; set; }
    public DbSet<ThuyetMinh>   ThuyetMinhs    { get; set; }
    public DbSet<BanDich>      BanDichs       { get; set; }
    public DbSet<MonAn>        MonAns         { get; set; }
    public DbSet<LichSuPhat>   LichSuPhats    { get; set; }
    public DbSet<TaiKhoan>     TaiKhoans      { get; set; }
    public DbSet<DangKyDichVu> DangKyDichVus  { get; set; }
    public DbSet<HoaDon>       HoaDons        { get; set; }
    public DbSet<DangKyApp>       DangKyApps       { get; set; }
    public DbSet<YeuCauThanhToan> YeuCauThanhToans { get; set; }
    public DbSet<VitriKhach>      VitriKhachs      { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<POI>().ToTable("poi");
        modelBuilder.Entity<ThuyetMinh>().ToTable("thuyetminh");
        modelBuilder.Entity<BanDich>().ToTable("bandich");
        modelBuilder.Entity<MonAn>().ToTable("monan");
        modelBuilder.Entity<LichSuPhat>().ToTable("lichsuphat");
        modelBuilder.Entity<TaiKhoan>().ToTable("taikhoan");
        modelBuilder.Entity<DangKyDichVu>().ToTable("dangkydichvu");
        modelBuilder.Entity<HoaDon>().ToTable("hoadon");
        modelBuilder.Entity<DangKyApp>().ToTable("dangkyapp");
        modelBuilder.Entity<YeuCauThanhToan>().ToTable("yeucauthanhtoan");
        modelBuilder.Entity<VitriKhach>().ToTable("vitrikhach");

        modelBuilder.Entity<DangKyDichVu>()
            .HasIndex(d => d.POIId);
        modelBuilder.Entity<DangKyApp>()
            .HasIndex(d => d.MaThietBi);
        modelBuilder.Entity<YeuCauThanhToan>()
            .HasIndex(y => y.MaThietBi);
        modelBuilder.Entity<VitriKhach>()
            .HasIndex(v => v.MaThietBi)
            .IsUnique();
    }
}

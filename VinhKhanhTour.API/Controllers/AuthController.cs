using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VinhKhanhTour.API.Data;
using VinhKhanhTour.API.Models;

namespace VinhKhanhTour.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    public AuthController(AppDbContext db) => _db = db;

    // POST /api/auth/login
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.TenDangNhap) ||
            string.IsNullOrWhiteSpace(req.MatKhau))
            return BadRequest(new { message = "Vui lòng nhập đầy đủ thông tin." });

        var tk = await _db.TaiKhoans.FirstOrDefaultAsync(t =>
            t.TenDangNhap == req.TenDangNhap && t.TrangThai);

        if (tk == null)
            return Unauthorized(new { message = "Tên đăng nhập không tồn tại." });

        // So sánh plain text (PoC demo — production nên dùng BCrypt)
        if (tk.MatKhau != req.MatKhau)
            return Unauthorized(new { message = "Mật khẩu không đúng." });

        return Ok(new UserResponse
        {
            Id          = tk.Id,
            TenDangNhap = tk.TenDangNhap,
            TenTaiKhoan = tk.TenTaiKhoan ?? tk.TenDangNhap,
            Email       = tk.Email,
            SoDienThoai = tk.SoDienThoai,
            VaiTro      = tk.VaiTro
        });
    }

    // POST /api/auth/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.TenDangNhap) ||
            string.IsNullOrWhiteSpace(req.MatKhau))
            return BadRequest(new { message = "Tên đăng nhập và mật khẩu không được trống." });

        if (req.MatKhau.Length < 6)
            return BadRequest(new { message = "Mật khẩu phải có ít nhất 6 ký tự." });

        bool exists = await _db.TaiKhoans.AnyAsync(t => t.TenDangNhap == req.TenDangNhap);
        if (exists)
            return Conflict(new { message = "Tên đăng nhập đã tồn tại." });

        var tk = new TaiKhoan
        {
            Id          = Guid.NewGuid(),
            TenDangNhap = req.TenDangNhap,
            MatKhau     = req.MatKhau,   // plain text cho demo
            TenTaiKhoan = req.TenTaiKhoan ?? req.TenDangNhap,
            Email       = req.Email,
            SoDienThoai = req.SoDienThoai,
            VaiTro      = "khach",
            TrangThai   = true,
            NgayTao     = DateTime.UtcNow
        };

        _db.TaiKhoans.Add(tk);
        await _db.SaveChangesAsync();

        return Ok(new UserResponse
        {
            Id          = tk.Id,
            TenDangNhap = tk.TenDangNhap,
            TenTaiKhoan = tk.TenTaiKhoan,
            Email       = tk.Email,
            SoDienThoai = tk.SoDienThoai,
            VaiTro      = tk.VaiTro
        });
    }
}

public class LoginRequest
{
    public string TenDangNhap { get; set; } = "";
    public string MatKhau     { get; set; } = "";
}

public class RegisterRequest
{
    public string  TenDangNhap { get; set; } = "";
    public string  MatKhau     { get; set; } = "";
    public string? TenTaiKhoan { get; set; }
    public string? Email       { get; set; }
    public string? SoDienThoai { get; set; }
}

public class UserResponse
{
    public Guid    Id          { get; set; }
    public string  TenDangNhap { get; set; } = "";
    public string? TenTaiKhoan { get; set; }
    public string? Email       { get; set; }
    public string? SoDienThoai { get; set; }
    public string  VaiTro      { get; set; } = "khach";
}

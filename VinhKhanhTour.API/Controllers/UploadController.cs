using Microsoft.AspNetCore.Mvc;

namespace VinhKhanhTour.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    // POST /api/upload
    // Nhận file ảnh, lưu vào wwwroot/uploads, trả về đường dẫn tương đối.
    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Chưa chọn file." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = "File quá lớn. Tối đa 5 MB." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { error = $"Định dạng không hỗ trợ. Dùng: {string.Join(", ", AllowedExtensions)}" });

        var uploadsDir = Path.Combine(
            AppContext.BaseDirectory, "wwwroot", "uploads");

        // Fallback nếu wwwroot không tồn tại trong output dir
        if (!Directory.Exists(uploadsDir))
        {
            uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            Directory.CreateDirectory(uploadsDir);
        }

        var fileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(uploadsDir, fileName);

        await using (var stream = System.IO.File.Create(fullPath))
            await file.CopyToAsync(stream);

        // Trả về đường dẫn tương đối — app sẽ ghép với ApiBaseUrl
        return Ok(new { url = $"/uploads/{fileName}" });
    }
}

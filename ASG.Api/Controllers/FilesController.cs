using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public FilesController(IWebHostEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// 上传图片并返回可访问的URL（默认存储到 /wwwroot/uploads/markdown）。
        /// 仅允许图片文件：png/jpeg/jpg/webp/gif。
        /// </summary>
        [HttpPost("upload-image")]
        [Authorize]
        public async Task<IActionResult> UploadImage([FromForm] IFormFile image, [FromQuery] string? scope = "markdown")
        {
            if (image == null || image.Length == 0)
            {
                return BadRequest(new { message = "请选择要上传的图片" });
            }

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "image/png", "image/jpeg", "image/jpg", "image/webp", "image/gif"
            };
            if (!allowed.Contains(image.ContentType))
            {
                return BadRequest(new { message = $"不支持的图片类型：{image.ContentType}" });
            }

            var ext = Path.GetExtension(image.FileName);
            if (string.IsNullOrWhiteSpace(ext))
            {
                // 尝试根据ContentType指定扩展名
                ext = image.ContentType switch
                {
                    "image/png" => ".png",
                    "image/jpeg" => ".jpg",
                    "image/jpg" => ".jpg",
                    "image/webp" => ".webp",
                    "image/gif" => ".gif",
                    _ => ".img"
                };
            }
            ext = ext.ToLowerInvariant();

            var root = _env.WebRootPath;
            if (string.IsNullOrEmpty(root))
            {
                root = Path.Combine(_env.ContentRootPath, "wwwroot");
            }
            var scopeDir = string.IsNullOrWhiteSpace(scope) ? "markdown" : scope.Trim();
            var dir = Path.Combine(root, "uploads", scopeDir);
            Directory.CreateDirectory(dir);

            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(dir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            var relativePath = $"/uploads/{scopeDir}/{fileName}";
            var scheme = Request.Scheme;
            var host = Request.Host.HasValue ? Request.Host.Value : string.Empty;
            var url = !string.IsNullOrEmpty(host) ? $"{scheme}://{host}{relativePath}" : relativePath;

            return Ok(new { url });
        }
    }
}
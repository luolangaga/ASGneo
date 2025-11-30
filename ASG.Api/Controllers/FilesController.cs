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

        /// <summary>
        /// 上传3D模型与关联资源（多文件），返回主模型URL与文件列表。
        /// 存储路径：/wwwroot/uploads/models/{bundle}/。
        /// 支持扩展：.glb .gltf .obj .fbx .stl .ply .bin .mtl .png .jpg .jpeg .webp
        /// </summary>
        [HttpPost("upload-model")]
        [Authorize]
        public async Task<IActionResult> UploadModelBundle([FromForm] List<IFormFile> files, [FromForm] string? main, [FromQuery] string? bundle = null)
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { message = "请至少选择一个文件" });
            }

            var allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { 
                ".glb", ".gltf", ".obj", ".fbx", ".stl", ".ply", ".bin", ".mtl", ".png", ".jpg", ".jpeg", ".webp", ".tga",".pmx", ".pmd"
            };

            const long MaxSizePerFile = 50L * 1024 * 1024; // 50MB/文件

            // 生成或使用 bundle 目录
            var root = _env.WebRootPath;
            if (string.IsNullOrEmpty(root))
            {
                root = Path.Combine(_env.ContentRootPath, "wwwroot");
            }
            var id = string.IsNullOrWhiteSpace(bundle) ? Guid.NewGuid().ToString("N") : new string(bundle.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
            if (string.IsNullOrWhiteSpace(id)) id = Guid.NewGuid().ToString("N");
            var dir = Path.Combine(root, "uploads", "models", id);
            Directory.CreateDirectory(dir);

            var saved = new List<(string FileName, string Url, string Ext)>();

            foreach (var f in files)
            {
                if (f == null || f.Length == 0) continue;
                var ext = Path.GetExtension(f.FileName)?.ToLowerInvariant() ?? "";
                if (!allowedExts.Contains(ext))
                {
                    return BadRequest(new { message = $"不支持的文件类型：{f.FileName}" });
                }
                if (f.Length > MaxSizePerFile)
                {
                    return BadRequest(new { message = $"文件过大（>{MaxSizePerFile / (1024 * 1024)}MB）：{f.FileName}" });
                }

                var safeName = Path.GetFileName(f.FileName);
                var filePath = Path.Combine(dir, safeName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await f.CopyToAsync(stream);
                }
                var relative = $"/uploads/models/{id}/{safeName}";
                var scheme = Request.Scheme;
                var host = Request.Host.HasValue ? Request.Host.Value : string.Empty;
                var url = !string.IsNullOrEmpty(host) ? $"{scheme}://{host}{relative}" : relative;
                saved.Add((safeName, url, ext));
            }

            if (saved.Count == 0)
            {
                return BadRequest(new { message = "未保存任何文件" });
            }

            // 主模型文件：优先 main 指定；否则选择首个模型扩展
            var modelExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".glb", ".gltf", ".obj", ".fbx", ".stl", ".ply" };
            string? mainName = null;
            if (!string.IsNullOrWhiteSpace(main))
            {
                var m = saved.FirstOrDefault(x => string.Equals(x.FileName, main, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(m.FileName)) mainName = m.FileName;
            }
            if (string.IsNullOrEmpty(mainName))
            {
                var m = saved.FirstOrDefault(x => modelExts.Contains(x.Ext));
                mainName = m.FileName;
            }
            if (string.IsNullOrEmpty(mainName))
            {
                return BadRequest(new { message = "未找到主模型文件（需 .glb/.gltf/.obj/.fbx/.stl/.ply）" });
            }

            var mainUrl = saved.First(x => x.FileName == mainName).Url;
            var filesOut = saved.Select(x => new { name = x.FileName, url = x.Url }).ToList();

            return Ok(new { bundle = id, mainUrl, files = filesOut });
        }
    }
}

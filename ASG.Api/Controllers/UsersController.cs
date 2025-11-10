using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ASG.Api.DTOs;
using ASG.Api.Repositories;
using ASG.Api.Authorization;
using System.Security.Claims;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<UsersController> _logger;
        private readonly IWebHostEnvironment _env;

        public UsersController(IUserRepository userRepository, ILogger<UsersController> logger, IWebHostEnvironment env)
        {
            _userRepository = userRepository;
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// 获取自己的信息
        /// </summary>
        /// <returns>Current user information</returns>
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            var userResponse = new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Role = user.Role,
                RoleDisplayName = user.RoleDisplayName,
                RoleName = user.RoleName,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                IsActive = user.IsActive,
                AvatarUrl = GetAvatarUrl(user.Id),
                TeamId = user.TeamId
            };

            return Ok(userResponse);
        }

        /// <summary>
        /// Get all users (Admin only - for demonstration)
        /// </summary>
        /// <returns>List of all users</returns>
        [HttpGet]
        [Authorize(Policy = AuthorizationPolicies.CanManageUsers)]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userRepository.GetAllAsync();
            var userResponses = users.Select(user => new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Role = user.Role,
                RoleDisplayName = user.RoleDisplayName,
                RoleName = user.RoleName,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                IsActive = user.IsActive
            });

            return Ok(userResponses);
        }

        /// <summary>
        /// Get user by ID
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>User information</returns>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            var userResponse = new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Role = user.Role,
                RoleDisplayName = user.RoleDisplayName,
                RoleName = user.RoleName,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                IsActive = user.IsActive,
                AvatarUrl = GetAvatarUrl(user.Id),
                TeamId = user.TeamId
            };

            return Ok(userResponse);
        }

        /// <summary>
        /// 更新个人资料（仅更新 FirstName / LastName）
        /// </summary>
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            await _userRepository.UpdateAsync(user);

            var userResponse = new UserResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Role = user.Role,
                RoleDisplayName = user.RoleDisplayName,
                RoleName = user.RoleName,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                IsActive = user.IsActive,
                AvatarUrl = GetAvatarUrl(user.Id),
                TeamId = user.TeamId
            };

            return Ok(userResponse);
        }

        /// <summary>
        /// 上传头像（multipart/form-data，字段名：avatar）
        /// </summary>
        [HttpPost("avatar")]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile? avatar)
        {
            if (avatar == null || avatar.Length == 0)
            {
                return BadRequest(new { message = "请选择要上传的头像文件" });
            }

            var allowedExts = new[] { ".png", ".jpg", ".jpeg", ".webp" };
            var ext = Path.GetExtension(avatar.FileName).ToLowerInvariant();
            if (!allowedExts.Contains(ext))
            {
                return BadRequest(new { message = "仅支持 png/jpg/jpeg/webp 格式" });
            }

            // 限制大小：5MB
            const long maxSize = 5 * 1024 * 1024;
            if (avatar.Length > maxSize)
            {
                return BadRequest(new { message = "文件大小不能超过 5MB" });
            }

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var root = _env.WebRootPath;
            if (string.IsNullOrEmpty(root))
            {
                root = Path.Combine(_env.ContentRootPath, "wwwroot");
            }

            var userDir = Path.Combine(root, "avatars", userId);
            Directory.CreateDirectory(userDir);

            // 统一文件名为 avatar.ext，若存在旧文件则删除
            foreach (var file in Directory.GetFiles(userDir, "avatar.*"))
            {
                try { System.IO.File.Delete(file); } catch { /* ignore */ }
            }

            var filePath = Path.Combine(userDir, $"avatar{ext}");
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await avatar.CopyToAsync(stream);
            }

            var url = GetAvatarUrl(userId);
            return Ok(new { avatarUrl = url });
        }

        private string? GetAvatarUrl(string userId)
        {
            var root = _env.WebRootPath;
            if (string.IsNullOrEmpty(root))
            {
                root = Path.Combine(_env.ContentRootPath, "wwwroot");
            }
            var userDir = Path.Combine(root, "avatars", userId);
            if (!Directory.Exists(userDir)) return null;
            var files = Directory.GetFiles(userDir, "avatar.*");
            if (files.Length == 0) return null;
            var fileName = Path.GetFileName(files[0]);
            var relativePath = $"/avatars/{userId}/{fileName}";
            var scheme = Request.Scheme;
            var host = Request.Host.HasValue ? Request.Host.Value : string.Empty;
            if (!string.IsNullOrEmpty(host))
            {
                return $"{scheme}://{host}{relativePath}";
            }
            return relativePath;
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="id">User ID</param>
        /// <returns>Success message</returns>
        [HttpDelete("{id}")]
        [Authorize(Policy = AuthorizationPolicies.CanDeleteUsers)]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var exists = await _userRepository.ExistsAsync(id);
            if (!exists)
            {
                return NotFound(new { message = "User not found." });
            }

            await _userRepository.DeleteAsync(id);
            _logger.LogInformation("User deleted: {UserId}", id);

            return Ok(new { message = "User deleted successfully." });
        }
    }
}
using Microsoft.AspNetCore.Mvc;
using ASG.Api.DTOs;
using ASG.Api.Services;
using Microsoft.AspNetCore.Identity;
using ASG.Api.Models;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;
        private readonly IConfiguration _config;

        public AuthController(IAuthService authService, ILogger<AuthController> logger, IConfiguration config)
        {
            _authService = authService;
            _logger = logger;
            _config = config;
        }

        /// <summary>
        /// 注册用户
        /// </summary>
        /// <param name="registrationDto">User registration data</param>
        /// <returns>Authentication response with JWT token</returns>
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegistrationDto registrationDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var result = await _authService.RegisterAsync(registrationDto);
                _logger.LogInformation("User registered successfully: {Email}", registrationDto.Email);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "User registration failed: {Email}", registrationDto.Email);
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 登录用户
        /// </summary>
        /// <param name="loginDto">User login credentials</param>
        /// <returns>Authentication response with JWT token</returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto loginDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _authService.LoginAsync(loginDto);
            if (result == null)
            {
                return Unauthorized(new { message = "邮箱或密码错误" });
            }

            _logger.LogInformation("User logged in successfully: {Email}", loginDto.Email);
            return Ok(result);
        }

        /// <summary>
        /// 退出登录(client-side token removal)
        /// </summary>
        /// <returns>Success message</returns>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
           
            await Task.CompletedTask;
            return Ok(new { message = "这个Api暂无实质性作用，请客户端清除Token以注销" });
        }

        [HttpPost("password-reset/request")]
        public async Task<IActionResult> RequestPasswordReset([FromBody] PasswordResetRequestDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _authService.RequestPasswordResetAsync(dto.Email);
            _logger.LogInformation("Password reset requested: {Email}", dto.Email);
            return Ok(new { message = "如果该邮箱存在，将发送重置邮件" });
        }

        [HttpPost("password-reset/confirm")]
        public async Task<IActionResult> ConfirmPasswordReset([FromBody] PasswordResetConfirmDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ok = await _authService.ResetPasswordAsync(dto.Email, dto.Token, dto.NewPassword);
            if (!ok)
            {
                return BadRequest(new { message = "密码重置失败" });
            }
            _logger.LogInformation("Password reset succeeded: {Email}", dto.Email);
            return Ok(new { message = "密码已更新" });
        }

        // 第三方登录已移除
    }
}
using Microsoft.AspNetCore.Mvc;
using ASG.Api.DTOs;
using ASG.Api.Services;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IAuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        /// <summary>
        /// Register a new user
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

            var result = await _authService.RegisterAsync(registrationDto);
            if (result == null)
            {
                return BadRequest(new { message = "User registration failed. Email may already be in use." });
            }

            _logger.LogInformation("User registered successfully: {Email}", registrationDto.Email);
            return Ok(result);
        }

        /// <summary>
        /// Login user
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
                return Unauthorized(new { message = "Invalid email or password." });
            }

            _logger.LogInformation("User logged in successfully: {Email}", loginDto.Email);
            return Ok(result);
        }

        /// <summary>
        /// Logout user (client-side token removal)
        /// </summary>
        /// <returns>Success message</returns>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            // In JWT implementation, logout is typically handled client-side
            await Task.CompletedTask;
            return Ok(new { message = "Logged out successfully. Please remove the token from client storage." });
        }
    }
}
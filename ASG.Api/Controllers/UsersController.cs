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

        public UsersController(IUserRepository userRepository, ILogger<UsersController> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
        }

        /// <summary>
        /// Get current user profile
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
                IsActive = user.IsActive
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
                IsActive = user.IsActive
            };

            return Ok(userResponse);
        }

        /// <summary>
        /// Delete user (soft delete)
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
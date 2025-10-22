using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ASG.Api.Models;

namespace ASG.Api.Authorization
{
    public class RoleAuthorizationHandler : AuthorizationHandler<RoleRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            RoleRequirement requirement)
        {
            var userRoleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;
            
            if (string.IsNullOrEmpty(userRoleClaim))
            {
                context.Fail();
                return Task.CompletedTask;
            }

            if (!Enum.TryParse<UserRole>(userRoleClaim, out var userRole))
            {
                context.Fail();
                return Task.CompletedTask;
            }

            // 检查用户角色是否满足要求
            if (HasRequiredRole(userRole, requirement.RequiredRole, requirement.AllowHigherRoles))
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }

            return Task.CompletedTask;
        }

        private static bool HasRequiredRole(UserRole userRole, UserRole requiredRole, bool allowHigherRoles)
        {
            // 如果用户角色完全匹配
            if (userRole == requiredRole)
                return true;

            // 如果不允许更高角色，直接返回false
            if (!allowHigherRoles)
                return false;

            // 角色层级检查：SuperAdmin > Admin > User
            return requiredRole switch
            {
                UserRole.User => userRole == UserRole.Admin || userRole == UserRole.SuperAdmin,
                UserRole.Admin => userRole == UserRole.SuperAdmin,
                UserRole.SuperAdmin => false, // 只有SuperAdmin才能访问SuperAdmin级别的资源
                _ => false
            };
        }
    }
}
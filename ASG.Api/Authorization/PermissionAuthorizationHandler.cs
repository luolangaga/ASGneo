using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ASG.Api.Models;

namespace ASG.Api.Authorization
{
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
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

            // 检查用户角色是否有指定权限
            if (userRole.HasPermission(requirement.Permission))
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }

            return Task.CompletedTask;
        }
    }
}
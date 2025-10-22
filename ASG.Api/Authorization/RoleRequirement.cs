using Microsoft.AspNetCore.Authorization;
using ASG.Api.Models;

namespace ASG.Api.Authorization
{
    public class RoleRequirement : IAuthorizationRequirement
    {
        public UserRole RequiredRole { get; }
        public bool AllowHigherRoles { get; }

        public RoleRequirement(UserRole requiredRole, bool allowHigherRoles = true)
        {
            RequiredRole = requiredRole;
            AllowHigherRoles = allowHigherRoles;
        }
    }
}
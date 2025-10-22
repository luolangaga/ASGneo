using ASG.Api.Models;

namespace ASG.Api.Authorization
{
    public static class AuthorizationPolicies
    {
        // 角色策略
        public const string RequireUserRole = "RequireUserRole";
        public const string RequireAdminRole = "RequireAdminRole";
        public const string RequireSuperAdminRole = "RequireSuperAdminRole";

        // 权限策略
        public const string CanManageUsers = "CanManageUsers";
        public const string CanViewReports = "CanViewReports";
        public const string CanManageSystem = "CanManageSystem";
        public const string CanDeleteUsers = "CanDeleteUsers";
        public const string CanAssignRoles = "CanAssignRoles";

        public static void ConfigurePolicies(IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                // 角色策略配置
                options.AddPolicy(RequireUserRole, policy =>
                    policy.Requirements.Add(new RoleRequirement(UserRole.User)));

                options.AddPolicy(RequireAdminRole, policy =>
                    policy.Requirements.Add(new RoleRequirement(UserRole.Admin)));

                options.AddPolicy(RequireSuperAdminRole, policy =>
                    policy.Requirements.Add(new RoleRequirement(UserRole.SuperAdmin, false)));

                // 权限策略配置
                options.AddPolicy(CanManageUsers, policy =>
                    policy.Requirements.Add(new PermissionRequirement("manage_users")));

                options.AddPolicy(CanViewReports, policy =>
                    policy.Requirements.Add(new PermissionRequirement("view_reports")));

                options.AddPolicy(CanManageSystem, policy =>
                    policy.Requirements.Add(new PermissionRequirement("manage_system")));

                options.AddPolicy(CanDeleteUsers, policy =>
                    policy.Requirements.Add(new PermissionRequirement("delete_users")));

                options.AddPolicy(CanAssignRoles, policy =>
                    policy.Requirements.Add(new PermissionRequirement("assign_roles")));
            });
        }
    }
}
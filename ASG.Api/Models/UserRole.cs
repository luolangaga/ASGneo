namespace ASG.Api.Models
{
    public enum UserRole
    {
        None = 0,
        User = 1,
        Admin = 2,
        SuperAdmin = 3
    }

    public static class UserRoleExtensions
    {
        public static string GetDisplayName(this UserRole role)
        {
            return role switch
            {
                UserRole.User => "用户",
                UserRole.Admin => "管理员",
                // 统一移除 SuperAdmin 的显示，映射到管理员
                UserRole.SuperAdmin => "管理员",
                _ => "未知角色"
            };
        }

        public static string GetRoleName(this UserRole role)
        {
            return role switch
            {
                UserRole.User => "User",
                UserRole.Admin => "Admin",
                // 统一移除 SuperAdmin 的名称，映射到 Admin
                UserRole.SuperAdmin => "Admin",
                _ => "Unknown"
            };
        }

        public static bool HasPermission(this UserRole role, UserRole requiredRole)
        {
            return (int)role >= (int)requiredRole;
        }

        public static bool HasPermission(this UserRole role, string permission)
        {
            return permission switch
            {
                // 取消 SuperAdmin 特权，统一由 Admin 管理；为了兼容历史账号，SuperAdmin 也视为 Admin
                "manage_users" => role == UserRole.Admin || role == UserRole.SuperAdmin,
                "view_reports" => role == UserRole.Admin || role == UserRole.SuperAdmin,
                "manage_system" => role == UserRole.Admin || role == UserRole.SuperAdmin,
                "delete_users" => role == UserRole.Admin || role == UserRole.SuperAdmin,
                "assign_roles" => role == UserRole.Admin || role == UserRole.SuperAdmin,
                _ => false
            };
        }
    }
}
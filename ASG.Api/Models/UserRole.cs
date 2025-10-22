namespace ASG.Api.Models
{
    public enum UserRole
    {
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
                UserRole.SuperAdmin => "超级管理员",
                _ => "未知角色"
            };
        }

        public static string GetRoleName(this UserRole role)
        {
            return role switch
            {
                UserRole.User => "User",
                UserRole.Admin => "Admin",
                UserRole.SuperAdmin => "SuperAdmin",
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
                "manage_users" => role == UserRole.Admin || role == UserRole.SuperAdmin,
                "view_reports" => role == UserRole.Admin || role == UserRole.SuperAdmin,
                "manage_system" => role == UserRole.SuperAdmin,
                "delete_users" => role == UserRole.SuperAdmin,
                "assign_roles" => role == UserRole.SuperAdmin,
                _ => false
            };
        }
    }
}
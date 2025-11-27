import { get, put } from './api'

// 对齐后端 RoleController 路由
export const getAllRoles = () => get('/Role/roles')
export const getUsersByRole = (roleName, params) => get(`/Role/users-by-role/${encodeURIComponent(roleName)}`, { params })
export const getPagedUsers = (params) => get('/Role/users', { params })
export const getRoleStatistics = () => get('/Role/statistics')
export const hasPermission = (permissionName) => get(`/Role/check-permission/${encodeURIComponent(permissionName)}`)
export const canAssignRoles = () => hasPermission('assign_roles')
// updateUserRole 需传枚举数字：User=1, Admin=2, SuperAdmin=3
export const updateUserRole = (userId, role) => put('/Role/update-role', { userId, role })
export const getMyRole = () => get('/Role/my-role')
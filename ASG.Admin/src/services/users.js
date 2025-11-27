import { get, post, put, del } from './api'

export const getUser = (id) => get(`/Users/${id}`)
export const getUsers = (params) => get('/Users', { params })
// 分页用户列表由 RoleController 提供
export const getUsersPaged = (params) => get('/Role/users', { params })
// 管理员更新用户邮件积分
export const updateEmailCredits = (id, credits) => put(`/Users/${id}/email-credits`, { credits })
export const createUser = (data) => post('/Users', data)
export const deleteUser = (id) => del(`/Users/${id}`)
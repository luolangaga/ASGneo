import { apiFetch } from './api'

// 支持分页获取战队列表
export async function getTeams(page = 1, pageSize = 10) {
  return apiFetch('/Teams', { params: { page, pageSize } })
}
export async function getTeam(id) { return apiFetch(`/Teams/${id}`) }
export async function getTeamHonors(id) { return apiFetch(`/Teams/${id}/honors`) }
export async function searchTeams(name, page = 1, pageSize = 10) {
  const params = new URLSearchParams({ name: name || '', page, pageSize })
  return apiFetch(`/Teams/search?${params.toString()}`)
}
export async function createTeam(dto) { return apiFetch('/Teams/register', { method: 'POST', body: dto }) }
export async function updateTeam(id, dto) { return apiFetch(`/Teams/${id}`, { method: 'PUT', body: dto }) }
export async function deleteTeam(id) { return apiFetch(`/Teams/${id}`, { method: 'DELETE' }) }
export async function changeTeamPassword(id, currentPassword, newPassword) { return apiFetch(`/Teams/${id}/change-password`, { method: 'POST', body: { CurrentPassword: currentPassword, NewPassword: newPassword } }) }
export async function likeTeam(id) { return apiFetch(`/Teams/${id}/like`, { method: 'POST' }) }
export async function uploadTeamLogo(id, file) { const f = new FormData(); f.append('logo', file); return apiFetch(`/Teams/${id}/logo`, { method: 'POST', body: f }) }
export async function verifyOwnership(id) { return apiFetch(`/Teams/${id}/verify-ownership`) }
export async function generateInvite(id, validDays = 7) { return apiFetch(`/Teams/${id}/invite`, { method: 'POST', params: { validDays } }) }
export async function getInvite(token) { return apiFetch(`/Teams/invites/${token}`) }

export default { getTeams, getTeam, getTeamHonors, searchTeams, createTeam, updateTeam, deleteTeam, changeTeamPassword, likeTeam, uploadTeamLogo, verifyOwnership, generateInvite, getInvite }

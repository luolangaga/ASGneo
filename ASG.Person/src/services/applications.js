import { apiFetch } from './api'

export async function applyRecruitment(id, dto) { return apiFetch(`/Recruitments/${id}/apply`, { method: 'POST', body: JSON.stringify(dto) }) }
export async function getApplicationsByRecruitment(id) { return apiFetch(`/Recruitments/${id}/applications`) }
export async function approveApplication(id, dto) { return apiFetch(`/Applications/${id}/approve`, { method: 'POST', body: JSON.stringify(dto) }) }
export async function rejectApplication(id, dto) { return apiFetch(`/Applications/${id}/reject`, { method: 'POST', body: JSON.stringify(dto) }) }
export async function syncApplicationMatches(id, dto) { return apiFetch(`/Applications/${id}/sync-matches`, { method: 'POST', body: JSON.stringify(dto) }) }
export default { applyRecruitment, getApplicationsByRecruitment, approveApplication, rejectApplication, syncApplicationMatches }

import { apiFetch } from './api'

export async function getRecruitments(params = {}) {
  const q = new URLSearchParams()
  Object.entries(params || {}).forEach(([k, v]) => { if (v != null && v !== '') q.set(k, v) })
  const qs = q.toString()
  return apiFetch(`/Recruitments${qs ? `?${qs}` : ''}`)
}
export async function getRecruitment(id) { return apiFetch(`/Recruitments/${id}`) }
export async function createRecruitment(dto) { return apiFetch('/Recruitments', { method: 'POST', body: JSON.stringify(dto) }) }
export async function updateRecruitment(id, dto) { return apiFetch(`/Recruitments/${id}`, { method: 'PUT', body: JSON.stringify(dto) }) }
export async function deleteRecruitment(id) { return apiFetch(`/Recruitments/${id}`, { method: 'DELETE' }) }
export default { getRecruitments, getRecruitment, createRecruitment, updateRecruitment, deleteRecruitment }

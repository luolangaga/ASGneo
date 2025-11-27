import { apiFetch } from './api'

// 支持按赛事筛选与分页（注意：总数在响应头 X-Total-Count，apiFetch不返回头部）
export async function getMatches(eventId = null, page = 1, pageSize = 10) {
  const params = {}
  if (eventId) params.eventId = eventId
  params.page = page
  params.pageSize = pageSize
  return apiFetch('/Matches', { params })
}
export async function getMatch(id) { return apiFetch(`/Matches/${id}`) }
export async function createMatch(dto) { return apiFetch('/Matches', { method: 'POST', body: dto }) }
export async function updateMatch(id, dto) { return apiFetch(`/Matches/${id}`, { method: 'PUT', body: dto }) }
export async function deleteMatch(id) { return apiFetch(`/Matches/${id}`, { method: 'DELETE' }) }
export async function likeMatch(id) { return apiFetch(`/Matches/${id}/like`, { method: 'POST' }) }

export default { getMatches, getMatch, createMatch, updateMatch, deleteMatch, likeMatch }
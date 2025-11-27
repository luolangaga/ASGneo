import { apiFetch } from './api'

export async function getMatches({ eventId = null, page = 1, pageSize = 10 } = {}) {
  const params = new URLSearchParams()
  if (eventId) params.set('eventId', eventId)
  if (page) params.set('page', page)
  if (pageSize) params.set('pageSize', pageSize)
  const qs = params.toString()
  return apiFetch(`/Matches${qs ? `?${qs}` : ''}`)
}
export async function updateMatch(id, dto) {
  return apiFetch(`/Matches/${id}`, { method: 'PUT', body: JSON.stringify(dto) })
}
export default { getMatches, updateMatch }

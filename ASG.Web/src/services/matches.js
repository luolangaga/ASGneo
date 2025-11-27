import { apiFetch, apiFetchWithHeaders } from './api'

// 获取赛程列表，支持按赛事过滤与分页
export async function getMatches({ eventId = null, page = 1, pageSize = 10, groupIndex = null, groupLabel = null } = {}) {
  const params = new URLSearchParams()
  if (eventId) params.set('eventId', eventId)
  if (page) params.set('page', page)
  if (pageSize) params.set('pageSize', pageSize)
  if (groupIndex != null) params.set('groupIndex', groupIndex)
  if (groupLabel != null && String(groupLabel).trim()) params.set('groupLabel', String(groupLabel).trim())
  const qs = params.toString()
  return apiFetch(`/Matches${qs ? `?${qs}` : ''}`)
}

// 创建赛程（后端限制管理员角色），DTO 参照 CreateMatchDto
export async function createMatch(dto) {
  return apiFetch('/Matches', {
    method: 'POST',
    body: JSON.stringify(dto),
  })
}

// 更新赛程
export async function updateMatch(id, dto) {
  return apiFetch(`/Matches/${id}`, {
    method: 'PUT',
    body: JSON.stringify(dto),
  })
}

export async function updateMatchScores(id, dto) {
  return apiFetch(`/Matches/${id}/scores`, {
    method: 'PUT',
    body: JSON.stringify(dto),
  })
}

// 删除赛程
export async function deleteMatch(id) {
  return apiFetch(`/Matches/${id}`, {
    method: 'DELETE',
  })
}

// 点赞赛程（如果需要）
export async function likeMatch(id) {
  return apiFetch(`/Matches/${id}/like`, { method: 'POST' })
}

export async function generateSchedule(eventId, dto) {
  return apiFetch(`/Matches/generate-schedule/${eventId}`, {
    method: 'POST',
    body: JSON.stringify(dto),
  })
}

export async function getScheduleConflicts(eventId) {
  return apiFetch(`/Matches/conflicts/${eventId}`)
}

export async function generateNextRound(eventId, dto) {
  return apiFetch(`/Matches/next-round/${eventId}`, {
    method: 'POST',
    body: JSON.stringify(dto),
  })
}

export async function getMatchesPaged({ eventId = null, page = 1, pageSize = 10, groupIndex = null, groupLabel = null } = {}) {
  const params = new URLSearchParams()
  if (eventId) params.set('eventId', eventId)
  if (page) params.set('page', page)
  if (pageSize) params.set('pageSize', pageSize)
  if (groupIndex != null) params.set('groupIndex', groupIndex)
  if (groupLabel != null && String(groupLabel).trim()) params.set('groupLabel', String(groupLabel).trim())
  const qs = params.toString()
  const { data, headers } = await apiFetchWithHeaders(`/Matches${qs ? `?${qs}` : ''}`)
  let total = Number(headers.get('X-Total-Count') || 0)
  if (!Number.isFinite(total) || total <= 0) {
    const cr = headers.get('Content-Range') || headers.get('content-range')
    if (cr) {
      const m = String(cr).match(/\/(\d+)/)
      if (m && m[1]) total = Number(m[1])
    }
  }
  return { items: Array.isArray(data) ? data : [], totalCount: total }
}

export default { getMatches, createMatch, updateMatch, updateMatchScores, deleteMatch, likeMatch, generateSchedule, getScheduleConflicts, generateNextRound, getMatchesPaged }

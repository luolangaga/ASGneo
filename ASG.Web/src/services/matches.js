import { apiFetch } from './api'

// 获取赛程列表，支持按赛事过滤与分页
export async function getMatches({ eventId = null, page = 1, pageSize = 10 } = {}) {
  const params = new URLSearchParams()
  if (eventId) params.set('eventId', eventId)
  if (page) params.set('page', page)
  if (pageSize) params.set('pageSize', pageSize)
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

export default { getMatches, createMatch, updateMatch, updateMatchScores, deleteMatch, likeMatch }
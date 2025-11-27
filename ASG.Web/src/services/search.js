import { apiFetch } from './api'

// 统一搜索：type 可选 all/teams/events/articles
export async function search({ type = 'all', query = '', page = 1, pageSize = 12, sortBy = null, desc = true } = {}) {
  const params = new URLSearchParams()
  if (type) params.set('type', type)
  if (query) params.set('query', query)
  if (page) params.set('page', page)
  if (pageSize) params.set('pageSize', pageSize)
  if (type === 'articles') {
    if (sortBy) params.set('sortBy', sortBy)
    if (typeof desc === 'boolean') params.set('desc', String(desc))
  }
  const qs = params.toString()
  return apiFetch(`/Search${qs ? `?${qs}` : ''}`)
}

export default { search }
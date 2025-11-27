import { apiFetch } from './api'

export async function getArticles({ query = '', page = 1, pageSize = 10, sortBy = null, desc = true } = {}) {
  const params = new URLSearchParams()
  if (query) params.set('query', query)
  if (page) params.set('page', page)
  if (pageSize) params.set('pageSize', pageSize)
  if (sortBy) params.set('sortBy', sortBy)
  if (typeof desc === 'boolean') params.set('desc', String(desc))
  const qs = params.toString()
  return apiFetch(`/Articles${qs ? `?${qs}` : ''}`)
}
export async function getArticle(id) { return apiFetch(`/Articles/${id}`) }
export async function createArticle(dto) { return apiFetch('/Articles', { method: 'POST', body: dto }) }
export async function getComments(articleId, { page = 1, pageSize = 20 } = {}) {
  const params = new URLSearchParams(); if (page) params.set('page', page); if (pageSize) params.set('pageSize', pageSize)
  return apiFetch(`/Articles/${articleId}/comments${params.toString() ? `?${params.toString()}` : ''}`)
}
export async function addComment(articleId, dto) { return apiFetch(`/Articles/${articleId}/comments`, { method: 'POST', body: dto }) }
export async function likeArticle(id) { return apiFetch(`/Articles/${id}/like`, { method: 'POST' }) }
export async function addArticleView(id) { return apiFetch(`/Articles/${id}/view`, { method: 'POST' }) }

export default { getArticles, getArticle, createArticle, getComments, addComment, likeArticle, addArticleView }
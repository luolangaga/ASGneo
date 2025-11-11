import { apiFetch } from './api'

// 获取文章列表（分页，支持搜索与排序）
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

// 获取文章详情
export async function getArticle(id) {
  return apiFetch(`/Articles/${id}`)
}

// 创建文章
export async function createArticle(dto) {
  return apiFetch('/Articles', {
    method: 'POST',
    body: JSON.stringify(dto),
  })
}

// 获取评论列表（分页）
export async function getComments(articleId, { page = 1, pageSize = 20 } = {}) {
  const params = new URLSearchParams()
  if (page) params.set('page', page)
  if (pageSize) params.set('pageSize', pageSize)
  const qs = params.toString()
  return apiFetch(`/Articles/${articleId}/comments${qs ? `?${qs}` : ''}`)
}

// 添加评论
export async function addComment(articleId, dto) {
  return apiFetch(`/Articles/${articleId}/comments`, {
    method: 'POST',
    body: JSON.stringify(dto),
  })
}

// 给文章点赞，返回更新后的点赞数
export async function likeArticle(id) {
  return apiFetch(`/Articles/${id}/like`, {
    method: 'POST',
  })
}

// 增加文章浏览量，返回更新后的浏览量
export async function addArticleView(id) {
  return apiFetch(`/Articles/${id}/view`, {
    method: 'POST',
  })
}

export default { getArticles, getArticle, createArticle, getComments, addComment, likeArticle, addArticleView }
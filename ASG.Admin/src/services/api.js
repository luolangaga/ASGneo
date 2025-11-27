import { getToken, clearAuth } from '../stores/auth'

// 与 ASG.Web 保持一致的 API 基础地址策略
// 优先使用 VITE_API_BASE_URL，其次：生产环境走线上域名，开发环境走本地 5250 端口
const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || (import.meta.env.PROD ? 'https://api.idvevent.cn/api' : 'http://localhost:5250/api')

function buildUrl(path) {
  if (!path) return API_BASE_URL
  if (path.startsWith('http')) return path
  const normalized = path.startsWith('/') ? path : `/${path}`
  return `${API_BASE_URL}${normalized}`
}

export async function apiFetch(path, { method = 'GET', headers = {}, body, params } = {}) {
  const urlStr = buildUrl(path)
  if (params && typeof params === 'object') {
    const usp = new URLSearchParams()
    Object.entries(params).forEach(([k, v]) => {
      if (v === undefined || v === null) return
      usp.set(k, String(v))
    })
    const sep = urlStr.includes('?') ? '&' : '?'
    path = `${urlStr}${sep}${usp.toString()}`
  } else {
    path = urlStr
  }

  const token = getToken()
  const reqHeaders = { ...(headers || {}) }
  if (token) reqHeaders['Authorization'] = `Bearer ${token}`

  let payload = body
  if (payload && !(payload instanceof FormData) && !reqHeaders['Content-Type']) {
    reqHeaders['Content-Type'] = 'application/json'
    payload = JSON.stringify(payload)
  }

  const resp = await fetch(path, { method, headers: reqHeaders, body: payload })
  const contentType = resp.headers.get('content-type') || ''
  const isJson = contentType.includes('application/json')
  const isCsv = contentType.includes('text/csv')
  let data
  if (isJson) {
    data = await resp.json().catch(() => ({}))
  } else if (isCsv) {
    data = await resp.blob().catch(() => new Blob([]))
  } else {
    data = await resp.text().catch(() => '')
  }

  if (!resp.ok) {
    if (resp.status === 401 || resp.status === 403) {
      clearAuth()
      if (!window.location.pathname.startsWith('/login')) {
        const redirect = encodeURIComponent(window.location.pathname + window.location.search)
        window.location.assign(`/login?redirect=${redirect}`)
      }
    }
    const err = new Error(data?.message || `Request failed: ${resp.status}`)
    err.status = resp.status
    err.payload = data
    throw err
  }
  return data
}

export const get = (p, opts={}) => apiFetch(p, { ...opts, method: 'GET' })
export const post = (p, body, opts={}) => apiFetch(p, { ...opts, method: 'POST', body })
export const put = (p, body, opts={}) => apiFetch(p, { ...opts, method: 'PUT', body })
export const del = (p, opts={}) => apiFetch(p, { ...opts, method: 'DELETE' })

export { API_BASE_URL }

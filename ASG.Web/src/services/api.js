const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || (import.meta.env.PROD ? 'https://api.idvevent.cn/api' : 'http://localhost:5250/api')
import router from '../router'
import { clearAuth } from '../stores/auth'
import { notify } from '../stores/notify'

let hasNotifiedExpired = false

function buildUrl(path) {
  if (!path) return API_BASE_URL
  const normalized = path.startsWith('/') ? path : `/${path}`
  return `${API_BASE_URL}${normalized}`
}

export async function apiFetch(path, options = {}) {
  const token = localStorage.getItem('token')
  const headers = { ...(options.headers || {}) }
  const isFormData = options?.body instanceof FormData
  if (!isFormData && !headers['Content-Type']) headers['Content-Type'] = 'application/json'
  if (token) headers['Authorization'] = `Bearer ${token}`

  const response = await fetch(buildUrl(path), { ...options, headers })
  const contentType = response.headers.get('content-type') || ''
  if (!response.ok) {
    let errorPayload = null
    try {
      if (contentType.includes('application/json')) {
        errorPayload = await response.json()
      } else {
        const text = await response.text()
        try {
          errorPayload = JSON.parse(text)
        } catch {
          errorPayload = { message: text }
        }
      }
    } catch {
      // 作为兜底，避免二次读取同一body导致错误，改用clone
      try {
        const text = await response.clone().text()
        try {
          errorPayload = JSON.parse(text)
        } catch {
          errorPayload = { message: text }
        }
      } catch {
        errorPayload = null
      }
    }

    const status = response.status
    const root = errorPayload?.error || errorPayload
    let message = root?.message || errorPayload?.message || response.statusText

    if (status === 400) {
      const extracted = extractErrorDetails(errorPayload)
      if (extracted.length) message = `${message}：${extracted.join('；')}`
      errorPayload = { ...(errorPayload || {}), ...(root || {}), message }
    }

    // 统一处理Token过期/无权限：清理登录、提醒并跳转登录页
    if ((status === 401 || status === 403)) {
      clearAuth()
      if (!hasNotifiedExpired) {
        notify({ text: '登录已过期，请重新登录', color: 'warning' })
        hasNotifiedExpired = true
        setTimeout(() => { hasNotifiedExpired = false }, 5000)
      }
      const redirect = router.currentRoute?.value?.fullPath || '/'
      if (router.currentRoute?.value?.name !== 'login') {
        router.push({ name: 'login', query: { redirect } })
      }
    }

    const err = new Error(message)
    err.status = status
    err.payload = errorPayload
    throw err
  }
  if (contentType.includes('application/json')) {
    return response.json()
  }
  if (contentType.includes('text/csv')) {
    // 保留原始字节（含BOM），避免Excel中中文乱码
    return response.blob()
  }
  if (contentType.includes('application/zip') || contentType.includes('application/octet-stream')) {
    return response.blob()
  }
  return response.text()
}

export function extractErrorDetails(payload) {
  const out = []
  if (!payload) return out
  const root = payload.error || payload
  const normalizeStr = (s) => {
    try {
      const str = String(s)
      const m = str.match(/\{[^}]*message\s*=\s*([^}]*?)\s*\}/i)
      if (m && m[1]) return m[1].trim()
      return str.trim()
    } catch {
      return String(s)
    }
  }
  const pushFromObj = (obj) => {
    if (obj && typeof obj === 'object') {
      for (const k of Object.keys(obj)) {
        const v = obj[k]
        const arr = Array.isArray(v) ? v : [v]
        for (const item of arr) {
          if (item == null) continue
          let s
          if (typeof item === 'string') s = item
          else if (typeof item === 'object') s = JSON.stringify(item)
          else s = String(item)
          s = normalizeStr(s)
          if (s) out.push(s)
        }
      }
    }
  }
  pushFromObj(root.details)
  pushFromObj(root.errors)
  if (Array.isArray(root.messages)) {
    for (const m of root.messages) { if (m != null) out.push(normalizeStr(m)) }
  }
  return out
}

export { API_BASE_URL }

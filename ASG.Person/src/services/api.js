const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || (import.meta.env.PROD ? '/api' : 'http://localhost:5250/api')
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
        try { errorPayload = JSON.parse(text) } catch { errorPayload = { message: text } }
      }
    } catch {
      try {
        const text = await response.clone().text()
        try { errorPayload = JSON.parse(text) } catch { errorPayload = { message: text } }
      } catch { errorPayload = null }
    }

    const status = response.status
    const message = errorPayload?.message || response.statusText

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
  if (contentType.includes('application/json')) return response.json()
  if (contentType.includes('text/csv')) return response.blob()
  return response.text()
}

export { API_BASE_URL }

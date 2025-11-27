import { apiFetch } from './api'
import { setAuth, clearAuth } from '../stores/auth'

export async function login(email, password) {
  const payload = { email, password }
  const data = await apiFetch('/Auth/login', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
  setAuth(data)
  return data
}

export async function register({ email, password, fullName, role }) {
  const payload = { email, password, fullName }
  if (role) payload.role = role
  const data = await apiFetch('/Auth/register', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
  setAuth(data)
  return data
}

export function logout() {
  clearAuth()
}

export async function requestPasswordReset(email) {
  const payload = { email }
  const data = await apiFetch('/Auth/password-reset/request', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
  return data
}

export async function resetPassword({ email, token, password }) {
  const payload = { email, token, newPassword: password }
  const data = await apiFetch('/Auth/password-reset/confirm', {
    method: 'POST',
    body: JSON.stringify(payload),
  })
  return data
}

export async function getOAuthAuthorizeUrl(provider, redirect) {
  const qs = new URLSearchParams()
  if (redirect) qs.set('redirect', String(redirect))
  const q = qs.toString()
  const res = await apiFetch(`/OAuth/${provider}/authorize${q ? `?${q}` : ''}`)
  return res?.url || res?.Url || ''
}

export async function startOAuth(provider, redirect) {
  const url = await getOAuthAuthorizeUrl(provider, redirect)
  if (url) {
    window.location.href = url
  }
}

export async function getLinkAuthorizeUrl(provider, redirect) {
  const qs = new URLSearchParams()
  if (redirect) qs.set('redirect', String(redirect))
  const q = qs.toString()
  const res = await apiFetch(`/OAuth/${provider}/link/authorize${q ? `?${q}` : ''}`)
  return res?.url || res?.Url || ''
}

export async function startOAuthLink(provider, redirect) {
  const url = await getLinkAuthorizeUrl(provider, redirect)
  if (url) window.location.href = url
}

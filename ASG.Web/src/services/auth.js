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
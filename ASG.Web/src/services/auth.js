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

export async function register({ email, password, firstName, lastName, role }) {
  const payload = { email, password, firstName, lastName }
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
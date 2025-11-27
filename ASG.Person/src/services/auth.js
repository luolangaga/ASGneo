import { apiFetch } from './api'
import { setAuth } from '../stores/auth'

export async function login({ email, password }) {
  const resp = await apiFetch('/Auth/login', { method: 'POST', body: JSON.stringify({ email, password }) })
  setAuth(resp)
  return resp
}

export async function register({ email, password, fullName }) {
  const resp = await apiFetch('/Auth/register', { method: 'POST', body: JSON.stringify({ email, password, fullName }) })
  setAuth(resp)
  return resp
}

export default { login, register }

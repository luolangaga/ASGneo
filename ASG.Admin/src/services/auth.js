import { post, get } from './api'
import { setToken, setCurrentUser, clearAuth } from '../stores/auth'

export async function login(email, password) {
  const res = await post('/auth/login', { email, password })
  if (res?.token) setToken(res.token)
  if (res?.user) setCurrentUser(res.user)
  return res
}

export async function register(payload) {
  const res = await post('/auth/register', payload)
  if (res?.token) setToken(res.token)
  if (res?.user) setCurrentUser(res.user)
  return res
}

export async function logout() {
  try { await post('/auth/logout') } catch {}
  clearAuth()
}

export async function me() {
  const user = await get('/users/me')
  setCurrentUser(user)
  return user
}
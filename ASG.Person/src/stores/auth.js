import { ref } from 'vue'
import { startRealtime, stopRealtime } from '../services/realtime'

export const currentUser = ref(JSON.parse(localStorage.getItem('user') || 'null'))
export const isAuthenticated = ref(!!localStorage.getItem('token'))

export function setAuth(authResponse) {
  const token = authResponse?.token || authResponse?.Token || null
  const user = authResponse?.user || authResponse?.User || null
  if (token) localStorage.setItem('token', token)
  if (user) localStorage.setItem('user', JSON.stringify(user))
  currentUser.value = user || null
  isAuthenticated.value = !!token
  if (token) startRealtime()
}

export function clearAuth() {
  localStorage.removeItem('token')
  localStorage.removeItem('user')
  currentUser.value = null
  isAuthenticated.value = false
  stopRealtime()
}

export function getToken() { return localStorage.getItem('token') }

export function updateCurrentUser(user) {
  if (!user) return
  localStorage.setItem('user', JSON.stringify(user))
  currentUser.value = user
}

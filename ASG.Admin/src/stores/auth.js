import { ref, computed } from 'vue'

const TOKEN_KEY = 'asg_token'
const USER_KEY = 'asg_current_user'
const ROLE_KEY = 'asg_current_role'

export const token = ref(localStorage.getItem(TOKEN_KEY) || '')
export const currentUser = ref(readJson(USER_KEY))
export const currentRole = ref(readJson(ROLE_KEY))
export const isAuthenticated = computed(() => !!token.value)

function readJson(key) {
  try { const v = localStorage.getItem(key); return v ? JSON.parse(v) : null } catch { return null }
}

export function setToken(t) {
  token.value = t || ''
  if (t) localStorage.setItem(TOKEN_KEY, t)
  else localStorage.removeItem(TOKEN_KEY)
}

export function setCurrentUser(u) {
  currentUser.value = u || null
  if (u) localStorage.setItem(USER_KEY, JSON.stringify(u))
  else localStorage.removeItem(USER_KEY)
}

export function setRole(r) {
  currentRole.value = r || null
  if (r) localStorage.setItem(ROLE_KEY, JSON.stringify(r))
  else localStorage.removeItem(ROLE_KEY)
}

export function clearAuth() {
  setToken('')
  setCurrentUser(null)
  setRole(null)
}

export function getToken() { return token.value }
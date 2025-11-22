import { apiFetch } from './api'

export async function getProfile() {
  return apiFetch('/Users/profile')
}

export async function updateProfile({ fullName }) {
  const payload = { fullName }
  return apiFetch('/Users/profile', {
    method: 'PUT',
    body: JSON.stringify(payload),
  })
}

export async function uploadAvatar(file) {
  const formData = new FormData()
  formData.append('avatar', file)
  return apiFetch('/Users/avatar', {
    method: 'POST',
    body: formData,
  })
}

export async function getUser(id) {
  return apiFetch(`/Users/${id}`)
}

export async function searchUsersByName(name, limit = 10) {
  const qs = new URLSearchParams()
  if (name) qs.set('name', name)
  if (limit) qs.set('limit', String(limit))
  const q = qs.toString()
  return apiFetch(`/Users/search${q ? `?${q}` : ''}`)
}

export default { getProfile, updateProfile, uploadAvatar, getUser, searchUsersByName }
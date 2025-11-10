import { apiFetch } from './api'

export async function getProfile() {
  return apiFetch('/Users/profile')
}

export async function updateProfile({ firstName, lastName }) {
  const payload = { firstName, lastName }
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

export default { getProfile, updateProfile, uploadAvatar, getUser }
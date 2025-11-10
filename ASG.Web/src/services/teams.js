import { apiFetch } from './api'

export async function registerTeam(createTeamDto) {
  return apiFetch('/Teams/register', {
    method: 'POST',
    body: JSON.stringify(createTeamDto),
  })
}

export async function updateTeam(id, updateTeamDto) {
  return apiFetch(`/Teams/${id}`, {
    method: 'PUT',
    body: JSON.stringify(updateTeamDto),
  })
}

export async function bindTeam({ teamId, password }) {
  return apiFetch('/Teams/bind', {
    method: 'POST',
    body: JSON.stringify({ teamId, password }),
  })
}

export async function bindTeamByName({ name, password }) {
  return apiFetch('/Teams/bind-by-name', {
    method: 'POST',
    body: JSON.stringify({ name, password }),
  })
}

export async function unbindTeam() {
  return apiFetch('/Teams/unbind', {
    method: 'POST',
  })
}

export async function getTeam(id) {
  return apiFetch(`/Teams/${id}`)
}

export async function getTeamHonors(id) {
  return apiFetch(`/Teams/${id}/honors`)
}

export async function searchTeamsByName(name, page = 1, pageSize = 10) {
  const params = new URLSearchParams({ name, page, pageSize })
  return apiFetch(`/Teams/search?${params.toString()}`)
}

export async function likeTeam(id) {
  return apiFetch(`/Teams/${id}/like`, { method: 'POST' })
}

export async function uploadTeamLogo(id, file) {
  const formData = new FormData()
  formData.append('logo', file)
  return apiFetch(`/Teams/${id}/logo`, {
    method: 'POST',
    body: formData,
  })
}

export default { registerTeam, updateTeam, bindTeam, bindTeamByName, unbindTeam, getTeam, getTeamHonors, searchTeamsByName, likeTeam, uploadTeamLogo }
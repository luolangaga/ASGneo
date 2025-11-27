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

export async function leaveTeam(id) {
  return apiFetch(`/Teams/${id}/leave`, {
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

export async function deleteTeam(id) {
  return apiFetch(`/Teams/${id}`, {
    method: 'DELETE',
  })
}

export async function applyTeamLogoFromUrl(id, sourceUrl) {
  return apiFetch(`/Teams/${id}/logo-from-url`, {
    method: 'POST',
    body: JSON.stringify({ sourceUrl }),
  })
}

export async function generateInvite(teamId, validDays = 7) {
  const params = new URLSearchParams()
  if (validDays) params.set('validDays', validDays)
  const qs = params.toString()
  return apiFetch(`/Teams/${teamId}/invite${qs ? `?${qs}` : ''}`, { method: 'POST' })
}

export async function getInvite(token) {
  return apiFetch(`/Teams/invites/${token}`)
}

export async function acceptInvite(token, player) {
  const init = { method: 'POST' }
  if (player) init.body = JSON.stringify(player)
  return apiFetch(`/Teams/invites/${token}/accept`, init)
}

export async function transferOwner(teamId, targetUserId) {
  return apiFetch(`/Teams/${teamId}/transfer-owner`, {
    method: 'POST',
    body: JSON.stringify({ targetUserId }),
  })
}

export async function getMyPlayer() {
  return apiFetch('/Teams/me/player')
}

export async function upsertMyPlayer(player) {
  return apiFetch('/Teams/me/player', {
    method: 'POST',
    body: JSON.stringify(player),
  })
}

 

export async function setTeamDispute(teamId, { hasDispute, disputeDetail, communityPostId } = {}) {
  return apiFetch(`/Teams/${teamId}/dispute`, {
    method: 'POST',
    body: JSON.stringify({ hasDispute, disputeDetail, communityPostId }),
  })
}

export default { registerTeam, updateTeam, bindTeam, bindTeamByName, unbindTeam, leaveTeam, getTeam, getTeamHonors, searchTeamsByName, likeTeam, uploadTeamLogo, deleteTeam, applyTeamLogoFromUrl, generateInvite, getInvite, acceptInvite, getMyPlayer, upsertMyPlayer, transferOwner, setTeamDispute }

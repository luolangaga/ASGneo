import { apiFetch } from './api'

export async function getEvents() { return apiFetch('/Events') }
export async function searchEvents(keyword, page = 1, pageSize = 12) {
  const params = new URLSearchParams({ query: keyword || '', page, pageSize })
  return apiFetch(`/Events/search?${params.toString()}`)
}
export async function getEvent(id) { return apiFetch(`/Events/${id}`) }
export async function createEvent(dto) { return apiFetch('/Events', { method: 'POST', body: dto }) }
export async function updateEvent(id, dto) { return apiFetch(`/Events/${id}`, { method: 'PUT', body: dto }) }
export async function deleteEvent(id) { return apiFetch(`/Events/${id}`, { method: 'DELETE' }) }
export async function setChampion(id, teamId) { return apiFetch(`/Events/${id}/champion`, { method: 'PUT', body: { TeamId: teamId } }) }
export async function getEventRegistrations(id) { return apiFetch(`/Events/${id}/registrations`) }
export async function exportRegistrationsCsv(id) { return apiFetch(`/Events/${id}/registrations/export`) }
export async function uploadEventLogo(id, file) {
  const form = new FormData(); form.append('logo', file)
  return apiFetch(`/Events/${id}/logo`, { method: 'POST', body: form })
}
export async function registerTeam(id, teamId, notes = '') { return apiFetch(`/Events/${id}/register`, { method: 'POST', body: { TeamId: teamId, Notes: notes } }) }
export async function unregisterTeam(id, teamId) { return apiFetch(`/Events/${id}/register/${teamId}`, { method: 'DELETE' }) }
export async function updateRegistration(id, teamId, status, notes = '', notifyByEmail = false) {
  // 在“通过(3)”时，默认发送邮件通知；也可显式传 true 强制通知
  const shouldNotify = !!notifyByEmail || Number(status) === 3 || status === 'Approved'
  return apiFetch(`/Events/${id}/register/${teamId}`, {
    method: 'PUT',
    body: { Status: status, Notes: notes, NotifyByEmail: shouldNotify }
  })
}

export default { getEvents, searchEvents, getEvent, createEvent, updateEvent, deleteEvent, setChampion, getEventRegistrations, exportRegistrationsCsv, uploadEventLogo, registerTeam, unregisterTeam, updateRegistration }
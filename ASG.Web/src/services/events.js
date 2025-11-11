import { apiFetch } from './api'

export async function getAllEvents() {
  return apiFetch('/Events')
}

export async function getActiveRegistrationEvents() {
  return apiFetch('/Events/active-registration')
}

export async function getUpcomingEvents() {
  return apiFetch('/Events/upcoming')
}

// 分页获取正在报名的赛事
export async function getActiveRegistrationEventsPaged({ page = 1, pageSize = 12 } = {}) {
  const params = new URLSearchParams()
  if (page) params.set('page', page)
  if (pageSize) params.set('pageSize', pageSize)
  const qs = params.toString()
  return apiFetch(`/Events/active-registration/paged${qs ? `?${qs}` : ''}`)
}

// 分页获取即将开始的赛事
export async function getUpcomingEventsPaged({ page = 1, pageSize = 12 } = {}) {
  const params = new URLSearchParams()
  if (page) params.set('page', page)
  if (pageSize) params.set('pageSize', pageSize)
  const qs = params.toString()
  return apiFetch(`/Events/upcoming/paged${qs ? `?${qs}` : ''}`)
}

export async function getEvent(id) {
  return apiFetch(`/Events/${id}`)
}

export async function createEvent(dto) {
  return apiFetch('/Events', {
    method: 'POST',
    body: JSON.stringify(dto),
  })
}

export async function updateEvent(id, dto) {
  return apiFetch(`/Events/${id}`, {
    method: 'PUT',
    body: JSON.stringify(dto),
  })
}

export async function deleteEvent(id) {
  return apiFetch(`/Events/${id}`, {
    method: 'DELETE',
  })
}

export async function registerTeamToEvent(eventId, { teamId, notes }) {
  const payload = { teamId, notes }
  return apiFetch(`/Events/${eventId}/register`, {
    method: 'POST',
    body: JSON.stringify(payload),
  })
}

export async function unregisterTeamFromEvent(eventId, teamId) {
  return apiFetch(`/Events/${eventId}/register/${teamId}`, {
    method: 'DELETE',
  })
}

export async function getEventRegistrations(eventId) {
  return apiFetch(`/Events/${eventId}/registrations`)
}

export async function getTeamRegistrations(teamId) {
  return apiFetch(`/Events/team/${teamId}/registrations`)
}

export async function getMyEvents() {
  return apiFetch('/Events/my-events')
}

export async function exportEventRegistrationsCsv(eventId) {
  // 返回CSV Blob（含BOM），便于直接触发下载
  return apiFetch(`/Events/${eventId}/registrations/export`)
}

export async function uploadEventLogo(eventId, file) {
  const form = new FormData()
  form.append('logo', file)
  return apiFetch(`/Events/${eventId}/logo`, {
    method: 'POST',
    body: form,
  })
}

// 更新指定队伍在赛事中的报名状态
// status 可传数字枚举：
// 0 Pending, 1 Registered, 2 Confirmed, 3 Approved, 4 Cancelled, 5 Rejected
export async function updateTeamRegistrationStatus(eventId, teamId, { status, notes }) {
  const payload = { status, notes }
  return apiFetch(`/Events/${eventId}/register/${teamId}`, {
    method: 'PUT',
    body: JSON.stringify(payload),
  })
}

// 设置赛事冠军（传 null 清除冠军）
export async function setEventChampion(eventId, teamId) {
  const payload = { teamId }
  return apiFetch(`/Events/${eventId}/champion`, {
    method: 'PUT',
    body: JSON.stringify(payload),
  })
}

export default {
  getAllEvents,
  getActiveRegistrationEvents,
  getUpcomingEvents,
  getEvent,
  createEvent,
  updateEvent,
  deleteEvent,
  registerTeamToEvent,
  unregisterTeamFromEvent,
  getEventRegistrations,
  getTeamRegistrations,
  getMyEvents,
  exportEventRegistrationsCsv,
  uploadEventLogo,
  updateTeamRegistrationStatus,
  setEventChampion,
  getActiveRegistrationEventsPaged,
  getUpcomingEventsPaged,
}
import { apiFetch } from './api'

export async function listNotifications() { return apiFetch('/Notifications') }
export async function markRead(id) { return apiFetch(`/Notifications/${id}/read`, { method: 'POST' }) }
export async function markAllRead() { return apiFetch('/Notifications/read-all', { method: 'POST' }) }

export default { listNotifications, markRead, markAllRead }
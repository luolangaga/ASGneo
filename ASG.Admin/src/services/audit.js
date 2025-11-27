import { apiFetch } from './api'

export async function auditQq(eventId, teamId, action) {
  return apiFetch('/Audit/qq', { method: 'POST', body: { EventId: eventId, TeamId: teamId, Action: action } })
}

export default { auditQq }

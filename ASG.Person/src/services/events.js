import { apiFetch } from './api'

export async function getAllEvents() { return apiFetch('/Events') }
export async function getEvent(id) { return apiFetch(`/Events/${id}`) }
export async function getMatchesByEvent(eventId) { return apiFetch(`/Matches?eventId=${eventId}`) }
export async function getMyEvents() { return apiFetch('/Events/my-events') }
export async function getTeamRegistrations(teamId) { return apiFetch(`/Events/team/${teamId}/registrations`) }
export default { getAllEvents, getEvent, getMatchesByEvent, getMyEvents, getTeamRegistrations }

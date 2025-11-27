import { apiFetch } from './api'

export async function getConversations() { return apiFetch('/Messages/conversations') }
export async function getConversationMessages(id) { return apiFetch(`/Messages/conversations/${id}/messages`) }
export async function sendMessageToUser(userId, content) { return apiFetch(`/Messages/user/${userId}/message`, { method: 'POST', body: JSON.stringify({ content }) }) }

export default { getConversations, getConversationMessages, sendMessageToUser }
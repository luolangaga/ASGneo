import { apiFetch } from './api'

export async function getConversations() { return apiFetch('/Messages/conversations') }
export async function getConversationMessages(id) { return apiFetch(`/Messages/conversations/${id}/messages`) }
export async function sendMessageToUser(userId, content) { return apiFetch(`/Messages/user/${userId}/message`, { method: 'POST', body: JSON.stringify({ content }) }) }
export async function blockUser(userId) { return apiFetch(`/Messages/blocks/${userId}`, { method: 'POST' }) }
export async function unblockUser(userId) { return apiFetch(`/Messages/blocks/${userId}`, { method: 'DELETE' }) }
export async function checkBlock(userId) { return apiFetch(`/Messages/blocks/${userId}`) }
export async function clearConversation(id) { return apiFetch(`/Messages/conversations/${id}/clear`, { method: 'DELETE' }) }
export async function markConversationRead(id) { return apiFetch(`/Messages/conversations/${id}/read`, { method: 'POST' }) }

export default { getConversations, getConversationMessages, sendMessageToUser, blockUser, unblockUser, checkBlock, clearConversation, markConversationRead }
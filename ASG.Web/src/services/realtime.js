import * as signalR from '@microsoft/signalr'
import { API_BASE_URL } from './api'
import { getToken } from '../stores/auth'
import { notify } from '../stores/notify'
import { markConversationRead as markReadApi } from './messages'
import { incrementUnread } from '../stores/notifications'

let connection = null
let reconnectToastShown = false
let startToastShown = false
let retryAttempts = 0
const MAX_RETRIES = 5
let disabled = false
let starting = false

function inMessagesPage() {
  try {
    const p = window.location && window.location.pathname ? window.location.pathname : ''
    return p.startsWith('/messages') || p.startsWith('/chat') || p.startsWith('/notifications')
  } catch { return false }
}

export function getApiBase() {
  try {
    const u = new URL(API_BASE_URL)
    return `${u.protocol}//${u.host}`
  } catch {
    if (API_BASE_URL.startsWith('/')) return window.location.origin
    return API_BASE_URL.replace(/\/api$/, '')
  }
}

export function isConnected() {
  return !!connection && connection.state === signalR.HubConnectionState.Connected
}

function isConnecting() {
  return !!connection && (connection.state === signalR.HubConnectionState.Connecting || connection.state === signalR.HubConnectionState.Reconnecting)
}

export async function startRealtime() {
  if (disabled) return
  if (!inMessagesPage()) return
  const token = getToken()
  if (!token || isConnected() || starting || isConnecting()) return
  starting = true
  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${getApiBase()}/hubs/app`, {
      accessTokenFactory: () => getToken(),
      transport: signalR.HttpTransportType.WebSockets,
      skipNegotiation: true,
    })
    .build()
  connection.on('ReceiveNotification', (data) => {
    try {
      const p = typeof data?.payload === 'string' ? JSON.parse(data.payload) : data?.payload
      if (data?.type === 'article.comment') notify({ text: '你的文章有新评论', color: 'info' })
      else if (data?.type === 'comment.reply') notify({ text: '你的评论有新回复', color: 'info' })
      else if (data?.type === 'recruitment.application') notify({ text: '收到新的报名申请', color: 'success' })
      else if (data?.type === 'team.created') notify({ text: `战队创建成功${p?.teamName ? '：' + p.teamName : ''}`, color: 'success' })
      else if (data?.type === 'team.invite.submitted') notify({ text: `有成员通过邀请加入${p?.teamName ? '：' + p.teamName : ''}${p?.playerName ? '（' + p.playerName + '）' : ''}`, color: 'info' })
      else if (data?.type === 'event.registration.approved') notify({ text: `报名审批通过${p?.eventName ? '：' + p.eventName : ''}${p?.teamName ? '（' + p.teamName + '）' : ''}`, color: 'success' })
      else notify({ text: '收到系统通知', color: 'info' })
      incrementUnread()
      try { window.dispatchEvent(new CustomEvent('asg:notification', { detail: data })) } catch {}
    } catch { notify({ text: '收到系统通知', color: 'info' }) }
  })
  connection.on('ReceiveDirectMessage', (payload) => {
    notify({ text: '收到新消息', color: 'primary' })
    try { window.dispatchEvent(new CustomEvent('asg:direct-message', { detail: payload })) } catch {}
  })
  connection.on('ReceiveDirectMessageError', (err) => {
    notify({ text: err?.message || '无法发送消息', color: 'error' })
  })
  connection.onclose((error) => {
    if (inMessagesPage()) notify({ text: '实时连接已断开，将尝试重新连接', color: 'error', timeout: 4000 })
    reconnectToastShown = false
    try { window.dispatchEvent(new CustomEvent('asg:realtime-closed', { detail: { error } })) } catch {}
    connection = null
    if (!disabled && retryAttempts < MAX_RETRIES) {
      retryAttempts++
      setTimeout(() => { startRealtime().catch(() => {}) }, 3000)
    } else {
      if (inMessagesPage()) disableRealtime('无法建立WebSocket连接，已暂停实时功能')
    }
  })
  await connection.start().then(() => {
    retryAttempts = 0
    startToastShown = false
    starting = false
    try { window.dispatchEvent(new CustomEvent('asg:realtime-connected')) } catch {}
  }).catch((error) => {
    if (inMessagesPage() && !startToastShown) { notify({ text: '实时连接不可用，正在重试…', color: 'warning', timeout: 3000 }); startToastShown = true }
    try { window.dispatchEvent(new CustomEvent('asg:realtime-start-error', { detail: { error } })) } catch {}
    connection = null
    starting = false
    if (!disabled && retryAttempts < MAX_RETRIES) {
      retryAttempts++
      setTimeout(() => { startRealtime().catch(() => {}) }, 3000)
    } else {
      if (inMessagesPage()) disableRealtime('无法建立WebSocket连接，已暂停实时功能')
    }
  })
}

export async function stopRealtime() {
  if (!connection) return
  try { await connection.stop() } catch {}
  connection = null
}

export function enableRealtime() { disabled = false }
export function disableRealtimeGlobal(msg) { disabled = true; try { window.dispatchEvent(new CustomEvent('asg:realtime-disabled')) } catch {}; if (inMessagesPage()) notify({ text: msg || '实时功能已暂停', color: 'warning', timeout: 5000 }) }

export async function sendDirectMessage(toUserId, content) {
  if (!isConnected()) await startRealtime()
  if (!connection) throw new Error('未连接')
  await connection.invoke('SendDirectMessage', toUserId, content)
}

export async function markConversationRead(conversationId) {
  if (!isConnected()) await startRealtime()
  if (!connection) { try { await markReadApi(conversationId) } catch {} ; return }
  try { await connection.invoke('MarkConversationRead', conversationId) } catch { try { await markReadApi(conversationId) } catch {} }
}

export function isDisabled() { return disabled }
function disableRealtime(msg) {
  disabled = true
  try { window.dispatchEvent(new CustomEvent('asg:realtime-disabled')) } catch {}
  notify({ text: msg || '实时功能已暂停', color: 'warning', timeout: 5000 })
}

export default { startRealtime, stopRealtime, sendDirectMessage, isConnected, markConversationRead }

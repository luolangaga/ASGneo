import * as signalR from '@microsoft/signalr'
import { API_BASE_URL } from './api'
import { getToken } from '../stores/auth'
import { notify } from '../stores/notify'
import { incrementUnread } from '../stores/notifications'

let connection = null

export function getApiBase() {
  return API_BASE_URL.replace(/\/api$/, '')
}

export function isConnected() {
  return !!connection && connection.state === signalR.HubConnectionState.Connected
}

export async function startRealtime() {
  const token = getToken()
  if (!token || isConnected()) return
  connection = new signalR.HubConnectionBuilder()
    .withUrl(`${getApiBase()}/hubs/app`, { accessTokenFactory: () => getToken() })
    .withAutomaticReconnect()
    .build()
  connection.on('ReceiveNotification', (data) => {
    try {
      const p = typeof data?.payload === 'string' ? JSON.parse(data.payload) : data?.payload
      if (data?.type === 'article.comment') notify({ text: '你的文章有新评论', color: 'info' })
      else if (data?.type === 'comment.reply') notify({ text: '你的评论有新回复', color: 'info' })
      else if (data?.type === 'recruitment.application') notify({ text: '收到新的报名申请', color: 'success' })
      else notify({ text: '收到系统通知', color: 'info' })
      incrementUnread()
    } catch { notify({ text: '收到系统通知', color: 'info' }) }
  })
  connection.on('ReceiveDirectMessage', (payload) => {
    notify({ text: '收到新消息', color: 'primary' })
  })
  await connection.start().catch(() => {})
}

export async function stopRealtime() {
  if (!connection) return
  try { await connection.stop() } catch {}
  connection = null
}

export async function sendDirectMessage(toUserId, content) {
  if (!isConnected()) await startRealtime()
  if (!connection) throw new Error('未连接')
  await connection.invoke('SendDirectMessage', toUserId, content)
}

export default { startRealtime, stopRealtime, sendDirectMessage }
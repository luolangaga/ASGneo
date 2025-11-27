import { Capacitor } from '@capacitor/core'
import { PushNotifications } from '@capacitor/push-notifications'
import { LocalNotifications } from '@capacitor/local-notifications'
import { Device } from '@capacitor/device'
import { apiFetch } from '../services/api'

function isNative() {
  try { return Capacitor.isNativePlatform() } catch { return false }
}

async function ensureLocalPermission() {
  try {
    const p = await LocalNotifications.requestPermissions()
    return p?.display === 'granted'
  } catch { return false }
}

export async function initMobilePush(router) {
  if (!isNative()) return
  await ensureLocalPermission()
  try {
    const perm = await PushNotifications.requestPermissions()
    if (perm?.receive === 'granted') {
      await PushNotifications.register()
    }
  } catch {}

  PushNotifications.addListener('registration', async (token) => {
    try { localStorage.setItem('device_push_token', token?.value || '') } catch {}
    try {
      const info = await Device.getInfo()
      localStorage.setItem('device_platform', info?.platform || '')
    } catch {}
    try {
      const t = token?.value || ''
      const p = (await Device.getInfo())?.platform || ''
      if (t) await apiFetch('/Devices/register', { method: 'POST', body: JSON.stringify({ token: t, platform: p }) })
    } catch {}
  })

  PushNotifications.addListener('pushNotificationReceived', async (notification) => {
    const title = notification?.title || '收到新通知'
    const body = notification?.body || ''
    const extra = notification?.data || {}
    try {
      await LocalNotifications.schedule({
        notifications: [{ id: Date.now() % 1000000000, title, body, extra }],
      })
    } catch {}
  })

  PushNotifications.addListener('pushNotificationActionPerformed', (action) => {
    const route = action?.notification?.data?.route
    if (route) router.push(route)
  })

  try {
    LocalNotifications.addListener('localNotificationActionPerformed', (e) => {
      const route = e?.notification?.extra?.route
      if (route) router.push(route)
    })
  } catch {}

  window.addEventListener('asg:notification', async (e) => {
    const d = e?.detail || {}
    const title = '系统通知'
    const body = d?.type || '收到系统通知'
    const route = '/notifications'
    try {
      await LocalNotifications.schedule({ notifications: [{ id: Date.now() % 1000000000, title, body, extra: { route } }] })
    } catch {}
  })

  window.addEventListener('asg:direct-message', async (e) => {
    const p = e?.detail || {}
    const title = '新消息'
    const body = typeof p?.content === 'string' ? p.content : '收到新消息'
    const uid = p?.fromUserId || p?.toUserId || ''
    const route = uid ? `/messages/${uid}` : '/messages'
    try {
      await LocalNotifications.schedule({ notifications: [{ id: Date.now() % 1000000000, title, body, extra: { route } }] })
    } catch {}
  })
}

export default { initMobilePush }

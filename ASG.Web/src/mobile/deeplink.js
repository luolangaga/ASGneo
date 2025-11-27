export function resolveRouteFromNotification(data) {
  try {
    const d = data || {}
    const t = (d.type || '').toString()
    if (t === 'direct_message' || t === 'message') {
      const u = d.toUserId || d.fromUserId || ''
      return u ? `/messages/${u}` : '/messages'
    }
    if (t.startsWith('event.') && d.eventId) return `/events/${d.eventId}`
    return '/notifications'
  } catch { return '/notifications' }
}

export default { resolveRouteFromNotification }

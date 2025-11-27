import { ref } from 'vue'
import { listNotifications } from '../services/notifications'

export const unreadCount = ref(0)

export function setUnreadCount(n) { unreadCount.value = Number(n) || 0 }

export async function refreshUnreadCount() {
  try {
    const list = await listNotifications()
    setUnreadCount((list || []).filter(x => x.isRead === false || x.IsRead === false).length)
  } catch {}
}

export function incrementUnread() { unreadCount.value = (unreadCount.value || 0) + 1 }

export default { unreadCount, setUnreadCount, refreshUnreadCount, incrementUnread }
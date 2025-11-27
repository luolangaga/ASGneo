import { ref } from 'vue'

export const loadingOpen = ref(false)
export const loadingText = ref('')

let loadingCount = 0
let firstStartedAt = null
let closeTimer = null
const MIN_MS = 2000

export function startLoading(text) {
  const t = (typeof text === 'string' && text.trim()) ? text.trim() : '正在处理...'
  if (closeTimer) { clearTimeout(closeTimer); closeTimer = null }
  loadingText.value = t
  loadingCount++
  if (!loadingOpen.value) {
    firstStartedAt = Date.now()
    loadingOpen.value = true
  }
}

export function stopLoading() {
  loadingCount = Math.max(0, loadingCount - 1)
  if (loadingCount > 0) return
  const elapsed = typeof firstStartedAt === 'number' ? (Date.now() - firstStartedAt) : MIN_MS
  if (elapsed >= MIN_MS) {
    loadingOpen.value = false
    loadingText.value = ''
    firstStartedAt = null
  } else {
    const wait = MIN_MS - elapsed
    if (closeTimer) { clearTimeout(closeTimer) }
    closeTimer = setTimeout(() => {
      loadingOpen.value = false
      loadingText.value = ''
      firstStartedAt = null
      closeTimer = null
    }, wait)
  }
}

export default { loadingOpen, loadingText, startLoading, stopLoading }

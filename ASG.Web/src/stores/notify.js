import { ref } from 'vue'

export const notifyOpen = ref(false)
export const notifyColor = ref('info')
export const notifyText = ref('')
export const notifyTimeout = ref(3000)

export function notify({ text, color = 'info', timeout = 3000 }) {
  notifyText.value = text || ''
  notifyColor.value = color
  notifyTimeout.value = timeout
  notifyOpen.value = true
}

export default { notifyOpen, notifyColor, notifyText, notifyTimeout, notify }
import { ref } from 'vue'

export const notifyOpen = ref(false)
export const notifyText = ref('')
export const notifyColor = ref('info')
export const notifyTimeout = ref(3000)

export function notify({ text, color = 'info', timeout = 3000 }) {
  notifyText.value = text
  notifyColor.value = color
  notifyTimeout.value = timeout
  notifyOpen.value = true
}
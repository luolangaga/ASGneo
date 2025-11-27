import { Browser } from '@capacitor/browser'
import { Capacitor } from '@capacitor/core'

export async function openInApp(url) {
  try {
    if (Capacitor.isNativePlatform()) {
      await Browser.open({ url })
    } else {
      window.open(url, '_blank')
    }
  } catch { window.open(url, '_blank') }
}

export default { openInApp }

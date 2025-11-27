<script setup>
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Capacitor } from '@capacitor/core'
import { openInApp } from '../mobile/browser'

const route = useRoute()
const router = useRouter()
const src = ref('')

function sanitize(url) {
  try {
    const u = new URL(url)
    if (!['http:', 'https:'].includes(u.protocol)) return ''
    return u.toString()
  } catch { return '' }
}

onMounted(async () => {
  const url = String(route.query.url || '')
  const safe = sanitize(url)
  if (!safe) { router.replace('/'); return }
  if (Capacitor.isNativePlatform()) {
    await openInApp(safe)
    router.back()
  } else {
    src.value = safe
  }
})
</script>

<template>
  <v-container class="py-0" fluid>
    <div v-if="src" style="height: calc(100vh - 64px);">
      <iframe :src="src" title="embed" style="width:100%;height:100%;border:0" sandbox="allow-scripts allow-same-origin allow-forms"></iframe>
    </div>
    <div v-else class="d-flex align-center justify-center" style="height: calc(100vh - 64px);">
      <v-progress-circular indeterminate color="primary" />
    </div>
  </v-container>
  
</template>

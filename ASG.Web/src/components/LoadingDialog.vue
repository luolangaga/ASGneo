<template>
  <v-dialog v-model="open" persistent max-width="420">
    <v-card class="pa-6 d-flex flex-column align-center">
      <lottie-player src="/animations/loading.json" background="transparent" speed="1" loop autoplay style="width:180px;height:180px"></lottie-player>
      <div class="text-subtitle-2 mt-2">{{ text || '正在处理...' }}</div>
    </v-card>
  </v-dialog>
</template>

<script setup>
import { computed } from 'vue'
import { loadingOpen, loadingText } from '../stores/loading'
const open = computed({ get: () => loadingOpen.value, set: (v) => { loadingOpen.value = v } })
const text = computed(() => loadingText.value)
</script>

<style scoped>
.pa-6 { padding: 24px; }
</style>

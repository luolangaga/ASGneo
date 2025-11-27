<script setup>
import { computed, watch, ref } from 'vue'
const props = defineProps({
  modelValue: { type: Boolean, default: false },
  type: { type: String, default: 'success' },
  message: { type: String, default: '' },
  autoCloseMs: { type: Number, default: 2800 },
  successSrc: { type: String, default: '/animations/success.json' },
  errorSrc: { type: String, default: '/animations/error.json' },
  details: { type: Array, default: () => [] },
})
const emit = defineEmits(['update:modelValue'])
const open = computed({
  get() { return props.modelValue },
  set(v) { emit('update:modelValue', v) },
})
const src = computed(() => props.type === 'error' ? props.errorSrc : props.successSrc)
const timer = ref(null)
watch(open, (v) => {
  if (v && props.autoCloseMs > 0) {
    if (timer.value) { clearTimeout(timer.value); timer.value = null }
    timer.value = setTimeout(() => { open.value = false }, props.autoCloseMs)
  }
})
</script>

<template>
  <v-dialog v-model="open" persistent max-width="420">
    <v-card>
      <v-card-text>
        <div class="d-flex flex-column align-center py-4">
          <lottie-player :src="src" autoplay style="width:140px;height:140px"></lottie-player>
          <div class="text-subtitle-1 mt-2">{{ message || (type === 'error' ? '操作失败' : '操作成功') }}</div>
          <div v-if="type === 'error' && details && details.length" class="mt-2" style="width:100%">
            <div v-for="(d, i) in details" :key="i" class="text-body-2">{{ d }}</div>
          </div>
        </div>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<style scoped>
</style>
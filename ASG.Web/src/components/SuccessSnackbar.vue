<script setup>
import { computed } from 'vue'
const props = defineProps({
  modelValue: { type: Boolean, default: false },
  message: { type: String, default: '' },
  timeout: { type: Number, default: 2500 },
  location: { type: String, default: 'bottom right' },
  src: { type: String, default: '/animations/success.json' },
})
const emit = defineEmits(['update:modelValue'])
const open = computed({
  get() { return props.modelValue },
  set(v) { emit('update:modelValue', v) },
})
</script>

<template>
  <v-snackbar v-model="open" color="success" :timeout="timeout" :location="location">
    <div class="d-flex align-center">
      <lottie-player :src="src" autoplay style="width:40px;height:40px;margin-right:8px"></lottie-player>
      <div>{{ message || '操作成功' }}</div>
    </div>
  </v-snackbar>
  
</template>

<style scoped>
</style>
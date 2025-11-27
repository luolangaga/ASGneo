<script setup>
import { ref } from 'vue'
import { useRoute } from 'vue-router'
import { applyRecruitment } from '../services/applications'
import { notify } from '../stores/notify'

const route = useRoute()
const id = route.params.id
const note = ref('')
const submitting = ref(false)

async function submit() {
  submitting.value = true
  try { await applyRecruitment(id, { note: note.value }); notify({ text: '已提交申请', color: 'success' }) } catch (e) { notify({ text: e?.payload?.message || e?.message || '提交失败', color: 'error' }) } finally { submitting.value = false }
}
</script>

<template>
  <v-container class="py-8 narrow-container">
    <div class="text-h6 mb-3">申请任务</div>
    <v-textarea v-model="note" label="备注" rows="4" prepend-inner-icon="note" />
    <div class="mt-3"><v-btn :loading="submitting" color="primary" prepend-icon="send" @click="submit">提交申请</v-btn></div>
  </v-container>
</template>

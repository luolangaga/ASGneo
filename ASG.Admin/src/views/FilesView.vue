<script setup>
import { ref } from 'vue'
import { uploadImage } from '../services/files'
import { notifySuccess, notifyError } from '../notify'

const file = ref(null)
const scope = ref('')
const loading = ref(false)
const result = ref(null)
const errorMsg = ref('')

function onFileChange(e) {
  const f = e?.target?.files?.[0]
  file.value = f || null
}

async function onUpload() {
  errorMsg.value = ''
  result.value = null
  if (!file.value) { errorMsg.value = '请先选择图片文件'; return }
  loading.value = true
  try {
    const res = await uploadImage(file.value, scope.value?.trim())
    result.value = res
    notifySuccess('上传成功')
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '上传失败'
    notifyError(errorMsg.value)
  } finally { loading.value = false }
}
</script>

<template>
  <v-container class="py-6" style="max-width: 800px">
    <div class="text-h5 mb-4">文件上传</div>
    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
    <v-card>
      <v-card-text>
        <v-text-field v-model="scope" label="scope(可选)" placeholder="例如: events, teams" class="mb-2" />
        <input type="file" accept="image/*" @change="onFileChange" />
        <div class="mt-4">
          <v-btn color="primary" :loading="loading" @click="onUpload" prepend-icon="mdi-file-upload">上传图片</v-btn>
        </div>
        <div v-if="result" class="mt-4">
          <div>返回结果：</div>
          <pre style="white-space: pre-wrap">{{ result }}</pre>
          <div v-if="result?.url">
            图片 URL：
            <a :href="result.url" target="_blank">{{ result.url }}</a>
          </div>
        </div>
      </v-card-text>
    </v-card>
  </v-container>
</template>

<style scoped>
</style>
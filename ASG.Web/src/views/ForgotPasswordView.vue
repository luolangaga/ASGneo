<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { requestPasswordReset } from '../services/auth'

const email = ref('')
const loading = ref(false)
const errorMsg = ref('')
const successMsg = ref('')
const formRef = ref(null)
const router = useRouter()

const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const emailRules = [
  v => !!(v && v.trim()) || '请输入邮箱',
  v => emailRegex.test(String(v).trim()) || '邮箱格式不正确',
]

async function onSubmit() {
  errorMsg.value = ''
  successMsg.value = ''
  loading.value = true
  try {
    const res = await formRef.value?.validate?.()
    if (res && res.valid === false) { loading.value = false; return }
    await requestPasswordReset(email.value.trim())
    successMsg.value = '如果该邮箱存在，我们已发送重置邮件'
    router.push({ name: 'reset-password', query: { email: email.value.trim() } })
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '操作失败'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <v-container class="py-10" style="max-width: 520px">
    <v-card>
      <v-card-title>忘记密码</v-card-title>
      <v-card-text>
        <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
        <v-alert v-if="successMsg" type="success" :text="successMsg" class="mb-4" />
        <v-form ref="formRef" @submit.prevent="onSubmit">
          <v-text-field
            v-model="email"
            label="邮箱"
            type="email"
            prepend-inner-icon="mail"
            required
            :rules="emailRules"
          />
          <div class="d-flex align-center justify-space-between">
            <v-btn :loading="loading" type="submit" color="primary">发送重置邮件</v-btn>
            <v-btn to="/login" variant="text">返回登录</v-btn>
          </div>
        </v-form>
      </v-card-text>
    </v-card>
  </v-container>
  </template>

<style scoped>
</style>
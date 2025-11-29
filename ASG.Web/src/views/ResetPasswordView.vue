<script setup>
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { resetPassword } from '../services/auth'

const route = useRoute()
const router = useRouter()

const email = ref('')
const token = ref('')
const password = ref('')
const confirmPassword = ref('')
const showPassword = ref(false)
const loading = ref(false)
const errorMsg = ref('')
const successMsg = ref('')
const formRef = ref(null)

onMounted(() => {
  const q = route.query || {}
  email.value = String(q.email || '')
  token.value = String(q.token || '')
})

const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const emailRules = [
  v => !!(v && v.trim()) || '请输入邮箱',
  v => emailRegex.test(String(v).trim()) || '邮箱格式不正确',
]
const tokenRules = [v => !!(v && v.trim()) || '请输入令牌']
const passwordRules = [
  v => !!v || '请输入新密码',
  v => String(v).length >= 8 || '密码至少 8 位',
  v => /[A-Za-z]/.test(String(v)) && /\d/.test(String(v)) || '需包含字母与数字',
]
const confirmRules = [v => v === password.value || '两次输入不一致']

async function onSubmit() {
  errorMsg.value = ''
  successMsg.value = ''
  loading.value = true
  try {
    const res = await formRef.value?.validate?.()
    if (res && res.valid === false) { loading.value = false; return }
    const payload = { email: email.value.trim(), token: token.value, password: password.value }
    const r = await resetPassword(payload)
    successMsg.value = r?.message || '密码已更新'
    setTimeout(() => router.push('/login'), 1200)
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
      <v-card-title>重置密码</v-card-title>
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
          <v-text-field
            v-model="token"
            label="令牌"
            prepend-inner-icon="key"
            required
            :rules="tokenRules"
          />
          <v-text-field
            v-model="password"
            label="新密码"
            :type="showPassword ? 'text' : 'password'"
            prepend-inner-icon="lock"
            required
            :rules="passwordRules"
            :append-inner-icon="showPassword ? 'visibility_off' : 'visibility'"
            @click:append-inner="showPassword = !showPassword"
          />
          <v-text-field
            v-model="confirmPassword"
            label="确认新密码"
            :type="showPassword ? 'text' : 'password'"
            prepend-inner-icon="lock"
            required
            :rules="confirmRules"
          />
          <div class="d-flex align-center justify-space-between">
            <v-btn :loading="loading" type="submit" color="primary">提交</v-btn>
            <v-btn to="/login" variant="text">返回登录</v-btn>
          </div>
        </v-form>
      </v-card-text>
    </v-card>
  </v-container>
  </template>

<style scoped>
</style>
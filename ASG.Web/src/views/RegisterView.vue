<script setup>
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { register } from '../services/auth'

const router = useRouter()
const email = ref('')
const password = ref('')
const firstName = ref('')
const lastName = ref('')
const loading = ref(false)
const errorMsg = ref('')
const formRef = ref(null)
const showPassword = ref(false)

const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const emailRules = [
  v => !!(v && v.trim()) || '请输入邮箱',
  v => emailRegex.test(String(v).trim()) || '邮箱格式不正确',
]
const passwordRules = [
  v => !!v || '请输入密码',
  v => String(v).length >= 8 || '密码至少 8 位',
  v => /[A-Za-z]/.test(String(v)) && /\d/.test(String(v)) || '需包含字母与数字',
]
const nameRules = [
  v => !!(v && String(v).trim()) || '必填',
  v => String(v).trim().length <= 50 || '长度不超过 50',
]

function analyzePassword(pw) {
  const s = String(pw || '')
  const hasLower = /[a-z]/.test(s)
  const hasUpper = /[A-Z]/.test(s)
  const hasDigit = /\d/.test(s)
  const hasSymbol = /[^A-Za-z0-9]/.test(s)
  const len = s.length
  let score = 0
  // 长度加分：8/12/16
  if (len >= 8) score += 1
  if (len >= 12) score += 1
  if (len >= 16) score += 1
  // 字符类型加分
  score += (hasLower ? 1 : 0) + (hasUpper ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSymbol ? 1 : 0)
  const max = 8
  const percent = Math.min(max, score) / max * 100
  let label = '弱'
  let color = 'red'
  if (percent >= 75) { label = '强'; color = 'green' }
  else if (percent >= 50) { label = '良'; color = 'yellow' }
  else if (percent >= 25) { label = '中'; color = 'orange' }
  const suggestions = []
  if (len < 12) suggestions.push('增加长度到 12+ 位')
  if (!hasUpper) suggestions.push('加入大写字母')
  if (!hasLower) suggestions.push('加入小写字母')
  if (!hasDigit) suggestions.push('加入数字')
  if (!hasSymbol) suggestions.push('加入特殊字符（!@#…）')
  return { percent, label, color, suggestions }
}

const strength = computed(() => analyzePassword(password.value))

async function onSubmit() {
  errorMsg.value = ''
  loading.value = true
  try {
    const res = await formRef.value?.validate?.()
    if (res && res.valid === false) { loading.value = false; return }
    await register({ email: email.value.trim(), password: password.value, firstName: firstName.value.trim(), lastName: lastName.value.trim() })
    router.push('/')
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '注册失败'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <v-container class="py-10" style="max-width: 620px">
    <v-card>
      <v-card-title>注册</v-card-title>
      <v-card-text>
        <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
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
            v-model="password"
            label="密码"
            :type="showPassword ? 'text' : 'password'"
            prepend-inner-icon="lock"
            required
            :rules="passwordRules"
            :append-inner-icon="showPassword ? 'visibility_off' : 'visibility'"
            @click:append-inner="showPassword = !showPassword"
            hint="至少 8 位，包含字母与数字；建议加入特殊字符"
            persistent-hint
          />
          <div class="d-flex align-center mb-2">
            <v-progress-linear :model-value="strength.percent" :color="strength.color" height="8" rounded class="flex-grow-1 mr-3" />
            <span class="text-caption">强度：{{ strength.label }}</span>
          </div>
          <div v-if="strength.suggestions.length" class="text-caption text-medium-emphasis mb-3">
            改进建议：
            <span v-for="(sug, idx) in strength.suggestions" :key="idx">{{ sug }}<span v-if="idx < strength.suggestions.length - 1">，</span></span>
          </div>
          <v-text-field
            v-model="firstName"
            label="名字"
            prepend-inner-icon="person"
            required
            :rules="nameRules"
          />
          <v-text-field
            v-model="lastName"
            label="姓氏"
            prepend-inner-icon="person"
            required
            :rules="nameRules"
          />
          <div class="d-flex align-center justify-space-between">
            <v-btn :loading="loading" type="submit" color="primary">注册</v-btn>
            <v-btn to="/login" variant="text">已有账号？去登录</v-btn>
          </div>
        </v-form>
      </v-card-text>
    </v-card>
  </v-container>
</template>

<style scoped>
</style>
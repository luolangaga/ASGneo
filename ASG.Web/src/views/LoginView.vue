<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { login, startOAuth } from '../services/auth'

const router = useRouter()
const route = useRoute()
const email = ref('')
const password = ref('')
const loading = ref(false)
const errorMsg = ref('')
const formRef = ref(null)
const showPassword = ref(false)
const redirectTarget = computed(() => route.query.redirect || '/')

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

async function onSubmit() {
  errorMsg.value = ''
  loading.value = true
  try {
    const res = await formRef.value?.validate?.()
    if (res && res.valid === false) { loading.value = false; return }
    await login(email.value.trim(), password.value)
    router.push(redirectTarget.value)
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '登录失败'
  } finally {
    loading.value = false
  }
}

async function onOAuth(provider) {
  const redirect = String(redirectTarget.value || '/')
  await startOAuth(provider, redirect)
}
onMounted(() => {
  if (route.query.error) {
    errorMsg.value = String(route.query.error)
  }
})
</script>

<template>
  <v-container class="py-10" style="max-width: 520px">
    <v-card>
      <v-card-title>登录</v-card-title>
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
          <div class="d-flex align-center justify-space-between">
            <v-btn :loading="loading" type="submit" color="primary">登录</v-btn>
            <div>
              <v-btn to="/forgot-password" variant="text" class="mr-2">忘记密码？</v-btn>
              <v-btn to="/register" variant="text">没有账号？去注册</v-btn>
            </div>
          </div>
          <v-divider class="my-4" />
          <div class="d-flex flex-column">
            <div class="mb-2">使用第三方登录</div>
            <div class="d-flex align-center">
              <!-- <v-btn class="mr-2" color="black" variant="tonal" prepend-icon="open_in_new" @click="onOAuth('github')">GitHub</v-btn> -->
              <v-btn class="mr-2" color="blue" variant="tonal" prepend-icon="open_in_new" @click="onOAuth('microsoft')">Microsoft</v-btn>
              <!-- <v-btn color="green" variant="tonal" prepend-icon="open_in_new" @click="onOAuth('qq')">QQ</v-btn> -->
            </div>
          </div>
          
        </v-form>
      </v-card-text>
    </v-card>
  </v-container>
</template>

<style scoped>
.login-hero { display: flex; justify-content: center; align-items: center; margin-bottom: 24px }
</style>

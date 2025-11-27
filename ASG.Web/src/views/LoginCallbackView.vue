<script setup>
import { onMounted, ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { setAuth } from '../stores/auth'
import { getProfile } from '../services/user'

const router = useRouter()
const route = useRoute()
const loading = ref(true)
const errorMsg = ref('')

onMounted(async () => {
  try {
    const token = String(route.query.token || '')
    const redirect = String(route.query.redirect || '/')
    if (!token) { errorMsg.value = '登录失败'; loading.value = false; return }
    localStorage.setItem('token', token)
    const user = await getProfile()
    setAuth({ token, user })
    router.replace(redirect || '/')
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '登录失败'
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <v-container class="py-10" style="max-width: 520px">
    <v-card>
      <v-card-title>正在登录</v-card-title>
      <v-card-text>
        <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
        <v-progress-linear v-else indeterminate color="primary" />
      </v-card-text>
    </v-card>
  </v-container>
  <v-progress-linear v-if="loading" indeterminate color="primary" />
</template>

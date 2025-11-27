<script setup>
import { ref } from 'vue'
import { register } from '../services/auth'
import { setAuth } from '../stores/auth'
import { notify } from '../stores/notify'
import { useRouter } from 'vue-router'
const router = useRouter()
const email = ref(''); const password = ref(''); const fullName = ref(''); const loading = ref(false)
async function onRegister() {
  loading.value = true
  try { const resp = await register({ email: email.value, password: password.value, fullName: fullName.value }); setAuth(resp); notify({ text: '注册并登录成功', color: 'success' }); router.push('/') } catch (e) { notify({ text: e?.payload?.message || e?.message || '注册失败', color: 'error' }) } finally { loading.value = false }
}
</script>

<template>
  <v-container class="py-8 narrow-container">
    <div class="text-h6 mb-3">注册</div>
    <v-text-field v-model="fullName" label="姓名" />
    <v-text-field v-model="email" label="邮箱" prepend-inner-icon="mail" />
    <v-text-field v-model="password" label="密码" type="password" prepend-inner-icon="key" />
    <div class="mt-3"><v-btn :loading="loading" color="primary" prepend-icon="how_to_reg" @click="onRegister">注册</v-btn></div>
  </v-container>
</template>

<script setup>
import { onMounted, ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { setAuth } from '../stores/auth'
import { getProfile, updateProfile } from '../services/user'
import { startOAuthLink } from '../services/auth'

const router = useRouter()
const route = useRoute()
const loading = ref(true)
const saving = ref(false)
const errorMsg = ref('')
const successMsg = ref('')
const fullName = ref('')
const provider = ref('')
const redirect = ref('/')

onMounted(async () => {
  try {
    const token = String(route.query.token || '')
    provider.value = String(route.query.provider || '')
    redirect.value = String(route.query.redirect || '/')
    if (!token) { errorMsg.value = '登录失败'; loading.value = false; return }
    localStorage.setItem('token', token)
    const user = await getProfile()
    setAuth({ token, user })
    fullName.value = user?.fullName || user?.FullName || ''
    successMsg.value = provider.value ? `已通过 ${provider.value} 登录` : '已登录'
    if (fullName.value && redirect.value) {
      router.replace(redirect.value || '/')
      return
    }
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '登录失败'
  } finally {
    loading.value = false
  }
})

async function onSave() {
  try {
    saving.value = true
    await updateProfile({ fullName: fullName.value })
    successMsg.value = '资料已更新'
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '保存失败'
  } finally {
    saving.value = false
  }
}

function onContinue() {
  router.replace(redirect.value || '/')
}

async function bindProvider(p) {
  await startOAuthLink(p, '/profile')
}
</script>

<template>
  <v-container class="py-10" style="max-width: 640px">
    <v-card>
      <v-card-title>完善账号信息</v-card-title>
      <v-card-text>
        <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
        <v-alert v-else-if="successMsg" type="success" :text="successMsg" class="mb-4" />
        <v-form @submit.prevent="onSave">
          <v-text-field v-model="fullName" label="姓名" prepend-inner-icon="badge" />
          <div class="d-flex align-center justify-end">
            <v-btn :loading="saving" type="submit" color="primary">保存</v-btn>
            <v-btn class="ml-2" color="secondary" variant="tonal" @click="onContinue">继续</v-btn>
          </div>
        </v-form>

        <v-divider class="my-4" />
        <div>
          <div class="mb-2">绑定其他第三方账号</div>
          <div class="d-flex align-center">
            <v-btn class="mr-2" color="black" variant="tonal" prepend-icon="link" @click="bindProvider('github')">绑定 GitHub</v-btn>
            <v-btn class="mr-2" color="blue" variant="tonal" prepend-icon="link" @click="bindProvider('microsoft')">绑定 Microsoft</v-btn>
            <v-btn color="green" variant="tonal" prepend-icon="link" @click="bindProvider('qq')">绑定 QQ</v-btn>
          </div>
        </div>
      </v-card-text>
    </v-card>
  </v-container>
  <v-progress-linear v-if="loading" indeterminate color="primary" />
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getUser } from '../services/users'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const errorMsg = ref('')
const user = ref(null)

onMounted(async () => {
  await fetchUser()
})

async function fetchUser() {
  loading.value = true
  errorMsg.value = ''
  try {
    const id = route.params.id
    const u = await getUser(id)
    user.value = u
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载用户详情失败'
  } finally { loading.value = false }
}

function goBack() { router.push('/users') }
</script>

<template>
  <v-container class="py-6" style="max-width: 900px">
    <div class="d-flex align-center mb-4">
      <v-btn variant="text" prepend-icon="mdi-arrow-left" @click="goBack">返回</v-btn>
      <v-spacer></v-spacer>
      <div class="text-h5">用户详情</div>
    </div>
    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
    <v-skeleton-loader v-if="loading" type="article" />
    <template v-else>
      <v-card>
        <v-card-text>
          <v-row>
            <v-col cols="12" md="6">
              <div class="text-medium-emphasis mb-1">邮箱</div>
              <div>{{ user?.Email ?? user?.email }}</div>
            </v-col>
            <v-col cols="12" md="6">
              <div class="text-medium-emphasis mb-1">姓名</div>
              <div>{{ user?.FullName ?? user?.fullName }}</div>
            </v-col>
            <v-col cols="12" md="6">
              <div class="text-medium-emphasis mb-1">角色</div>
              <div>{{ user?.RoleName ?? user?.roleName }}</div>
            </v-col>
            <v-col cols="12" md="6">
              <div class="text-medium-emphasis mb-1">邮件积分</div>
              <div>{{ user?.EmailCredits ?? user?.emailCredits ?? 0 }}</div>
            </v-col>
          </v-row>
        </v-card-text>
      </v-card>
    </template>
  </v-container>
</template>

<style scoped>
</style>
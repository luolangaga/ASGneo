<script setup>
import { ref, onMounted } from 'vue'
import { getAllRoles, getRoleStatistics, getUsersByRole } from '../services/roles'

const roles = ref([])
const stats = ref(null)
const selectedRole = ref(null)
const roleUsers = ref([])
const errorMsg = ref('')
const loadingUsers = ref(false)

onMounted(async () => {
  try { roles.value = await getAllRoles() } catch {}
  try { stats.value = await getRoleStatistics() } catch {}
})

async function onSelectRole(role) {
  selectedRole.value = role
  roleUsers.value = []
  loadingUsers.value = true
  errorMsg.value = ''
  try {
    const list = await getUsersByRole(role?.RoleName || role?.roleName || role)
    roleUsers.value = list || []
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载失败'
  } finally { loadingUsers.value = false }
}
</script>

<template>
  <v-container class="py-6" style="max-width: 1100px">
    <div class="text-h5 mb-4">角色与权限</div>
    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
    <v-row>
      <v-col cols="12" md="4">
        <v-card>
          <v-card-title>角色列表</v-card-title>
          <v-card-text>
            <v-list density="compact">
              <v-list-item v-for="r in roles" :key="r?.Value || r?.RoleName || r" :title="r?.DisplayName || r?.RoleName || r" :subtitle="r?.RoleName" @click="onSelectRole(r)" />
            </v-list>
          </v-card-text>
        </v-card>
        <v-card class="mt-4">
          <v-card-title>角色统计（仅超级管理员）</v-card-title>
          <v-card-text>
            <div v-if="stats" class="d-flex flex-column gap-2">
              <div>用户：{{ stats.User ?? stats['User'] ?? 0 }}</div>
              <div>管理员：{{ stats.Admin ?? stats['Admin'] ?? 0 }}</div>
              <div>超级管理员：{{ stats.SuperAdmin ?? stats['SuperAdmin'] ?? 0 }}</div>
            </div>
            <div v-else class="text-medium-emphasis">无权限或无数据</div>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="12" md="8">
        <v-card>
          <v-card-title>角色用户</v-card-title>
          <v-card-text>
            <div class="text-medium-emphasis mb-2" v-if="!selectedRole">请选择左侧角色</div>
            <v-skeleton-loader v-else-if="loadingUsers" type="table" />
            <v-table v-else>
              <thead>
                <tr>
                  <th>邮箱</th>
                  <th>姓名</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="u in roleUsers" :key="u.id || u.Id">
                  <td>{{ u.email || u.Email }}</td>
                  <td>{{ u.fullName || u.FullName }}</td>
                </tr>
              </tbody>
            </v-table>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>

<style scoped>
.gap-2 { gap: 8px; }
</style>
<script setup>
import { ref, onMounted } from 'vue'
import { getRoleStatistics, getMyRole } from '../services/roles'
import { setRole } from '../stores/auth'
import { useRouter } from 'vue-router'

const router = useRouter()
const stats = ref(null)
const errorMsg = ref('')
const loading = ref(false)

onMounted(async () => {
  loading.value = true
  try {
    try { const r = await getMyRole(); setRole(r) } catch {}
    stats.value = await getRoleStatistics().catch(() => null)
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载失败'
  } finally { loading.value = false }
})

function go(path) { router.push(path) }

const quickLinks = [
  { title: '用户管理', icon: 'mdi-account-multiple', to: '/users', color: 'primary' },
  { title: '角色权限', icon: 'mdi-shield-account', to: '/roles', color: 'indigo' },
  { title: '赛事管理', icon: 'mdi-trophy', to: '/events', color: 'amber-darken-2' },
  { title: '战队管理', icon: 'mdi-account-group', to: '/teams', color: 'teal' },
  { title: '比赛管理', icon: 'mdi-sword-cross', to: '/matches', color: 'red-darken-1' },
  { title: '文章管理', icon: 'mdi-file-document', to: '/articles', color: 'blue-grey' },
  { title: '文件上传', icon: 'mdi-cloud-upload', to: '/files', color: 'cyan' },
]
</script>

<template>
  <v-container fluid class="pa-0">
    <div class="mb-6">
      <h1 class="text-h4 font-weight-bold text-primary mb-1">仪表盘</h1>
      <div class="text-subtitle-1 text-medium-emphasis">欢迎回来，查看系统概况与快捷操作</div>
    </div>
    
    <v-alert v-if="errorMsg" type="error" variant="tonal" :text="errorMsg" class="mb-6" />

    <!-- 统计卡片区域 -->
    <v-row class="mb-6">
      <v-col cols="12" sm="6" md="4">
        <v-card elevation="0" border class="h-100">
          <v-card-text>
            <div class="d-flex align-start justify-space-between mb-4">
              <div>
                <div class="text-overline mb-1">总用户数</div>
                <div class="text-h3 font-weight-bold text-primary">
                  <v-progress-circular v-if="loading" indeterminate size="32" width="4" color="primary" />
                  <span v-else>{{ stats?.User ?? stats?.['User'] ?? '-' }}</span>
                </div>
              </div>
              <v-avatar color="primary-lighten-4" rounded>
                <v-icon color="primary" icon="mdi-account-group"></v-icon>
              </v-avatar>
            </div>
            <div class="text-caption text-medium-emphasis">注册用户总数</div>
          </v-card-text>
        </v-card>
      </v-col>

      <v-col cols="12" sm="6" md="4">
        <v-card elevation="0" border class="h-100">
          <v-card-text>
            <div class="d-flex align-start justify-space-between mb-4">
              <div>
                <div class="text-overline mb-1">管理员</div>
                <div class="text-h3 font-weight-bold text-indigo">
                  <v-progress-circular v-if="loading" indeterminate size="32" width="4" color="indigo" />
                  <span v-else>{{ stats?.Admin ?? stats?.['Admin'] ?? '-' }}</span>
                </div>
              </div>
              <v-avatar color="indigo-lighten-4" rounded>
                <v-icon color="indigo" icon="mdi-shield-account"></v-icon>
              </v-avatar>
            </div>
            <div class="text-caption text-medium-emphasis">系统管理员数量</div>
          </v-card-text>
        </v-card>
      </v-col>
      
      <v-col cols="12" sm="6" md="4">
        <v-card elevation="0" border class="h-100">
          <v-card-text>
            <div class="d-flex align-start justify-space-between mb-4">
              <div>
                <div class="text-overline mb-1">超级管理员</div>
                <div class="text-h3 font-weight-bold text-deep-purple">
                  <v-progress-circular v-if="loading" indeterminate size="32" width="4" color="deep-purple" />
                  <span v-else>{{ stats?.SuperAdmin ?? stats?.['SuperAdmin'] ?? '-' }}</span>
                </div>
              </div>
              <v-avatar color="deep-purple-lighten-4" rounded>
                <v-icon color="deep-purple" icon="mdi-shield-crown"></v-icon>
              </v-avatar>
            </div>
            <div class="text-caption text-medium-emphasis">最高权限拥有者</div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <!-- 快捷入口 -->
    <div class="text-h6 font-weight-bold mb-4">快捷操作</div>
    <v-row>
      <v-col 
        v-for="link in quickLinks" 
        :key="link.to"
        cols="6"
        sm="4" 
        md="3"
        lg="2"
      >
        <v-card 
          @click="go(link.to)"
          elevation="0"
          border
          class="text-center py-6 cursor-pointer hover-card transition-swing"
        >
          <v-avatar :color="link.color" variant="tonal" size="56" class="mb-3">
            <v-icon :icon="link.icon" size="28"></v-icon>
          </v-avatar>
          <div class="font-weight-medium">{{ link.title }}</div>
        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>

<style scoped>
.hover-card:hover {
  border-color: rgb(var(--v-theme-primary));
  transform: translateY(-4px);
  box-shadow: 0 4px 12px rgba(0,0,0,0.05) !important;
}
</style>

<script setup>
import { ref, computed } from 'vue'
import { useDisplay } from 'vuetify'
import { currentUser, currentRole, isAuthenticated } from './stores/auth'
import { logout } from './services/auth'
import { useRouter } from 'vue-router'

const { smAndDown } = useDisplay()
const drawer = ref(!smAndDown.value) // 大屏默认展开
const router = useRouter()

const loggedIn = computed(() => isAuthenticated.value)
const roleName = computed(() => currentRole.value?.DisplayName || currentRole.value?.RoleName || '')
const userName = computed(() => currentUser.value?.fullName || currentUser.value?.email || 'User')
const userInitials = computed(() => userName.value ? userName.value.charAt(0).toUpperCase() : 'U')

function onLogout() { 
  logout()
  router.push('/login')
}

// 导航菜单配置
const menuItems = [
  { title: '仪表盘', icon: 'mdi-view-dashboard-outline', to: '/' },
  { type: 'subheader', title: '用户与权限' },
  { title: '用户管理', icon: 'mdi-account-multiple-outline', to: '/users' },
  { title: '角色权限', icon: 'mdi-shield-account-outline', to: '/roles' },
  { type: 'subheader', title: '赛事运营' },
  { title: '赛事管理', icon: 'mdi-trophy-outline', to: '/events' },
  { title: '战队管理', icon: 'mdi-account-group-outline', to: '/teams' },
  { title: '生成绑定Token', icon: 'mdi-key-outline', to: '/team-invite' },
  { title: '比赛管理', icon: 'mdi-sword-cross', to: '/matches' },
  { type: 'subheader', title: '内容管理' },
  { title: '文章管理', icon: 'mdi-file-document-outline', to: '/articles' },
  { title: '文件上传', icon: 'mdi-cloud-upload-outline', to: '/files' },
  { type: 'divider' },
  { title: '关于系统', icon: 'mdi-information-outline', to: '/about' },
]
</script>

<template>
  <v-app class="bg-background">
    <!-- 侧边栏 -->
    <v-navigation-drawer 
      v-model="drawer" 
      :temporary="smAndDown" 
      elevation="2"
      border="none"
      class="rounded-e-lg my-sidebar"
      width="280"
    >
      <!-- Logo 区域 -->
      <div class="px-6 py-8 d-flex align-center gap-3">
        <v-avatar color="primary" variant="tonal" rounded="lg">
          <v-icon icon="mdi-shield-crown" color="primary"></v-icon>
        </v-avatar>
        <div>
          <div class="text-subtitle-1 font-weight-bold text-primary">ASG Admin</div>
          <div class="text-caption text-medium-emphasis">管理后台系统</div>
        </div>
      </div>

      <v-divider class="mb-2"></v-divider>

      <!-- 导航列表 -->
      <v-list nav density="comfortable" class="px-3">
        <template v-for="(item, index) in menuItems" :key="index">
          
          <v-list-subheader 
            v-if="item.type === 'subheader'" 
            class="text-uppercase font-weight-bold mt-4 mb-1 text-caption"
            style="letter-spacing: 0.1em !important;"
          >
            {{ item.title }}
          </v-list-subheader>
          
          <v-divider v-else-if="item.type === 'divider'" class="my-3"></v-divider>
          
          <v-list-item 
            v-else 
            :to="item.to" 
            :prepend-icon="item.icon" 
            :title="item.title"
            rounded="lg"
            color="primary"
            class="mb-1"
          ></v-list-item>
        </template>
      </v-list>
    </v-navigation-drawer>

    <!-- 顶部栏 -->
    <v-app-bar 
      elevation="0" 
      color="background" 
      class="px-3 border-b"
      style="backdrop-filter: blur(10px); background-color: rgba(243, 244, 246, 0.8) !important;"
    >
      <v-app-bar-nav-icon @click="drawer = !drawer" color="grey-darken-1"></v-app-bar-nav-icon>
      
      <v-spacer></v-spacer>

      <!-- 右侧操作区 -->
      <div class="d-flex align-center gap-2">
        <template v-if="loggedIn">
          <v-chip 
            v-if="roleName" 
            color="primary" 
            variant="flat" 
            size="small" 
            class="font-weight-medium mr-2"
          >
            {{ roleName }}
          </v-chip>
          
          <v-menu offset-y transition="scale-transition">
            <template #activator="{ props }">
              <v-btn 
                v-bind="props" 
                variant="text" 
                class="text-capitalize px-2"
                rounded="lg"
              >
                <div class="d-flex align-center gap-2">
                  <v-avatar color="primary" size="32" variant="flat">
                    <span class="text-white text-subtitle-2">{{ userInitials }}</span>
                  </v-avatar>
                  <span class="hidden-sm-and-down text-body-2 font-weight-medium">{{ userName }}</span>
                  <v-icon icon="mdi-chevron-down" size="small" color="grey"></v-icon>
                </div>
              </v-btn>
            </template>
            
            <v-list width="200" elevation="3" rounded="lg" class="py-2">
              <div class="px-4 py-3 bg-grey-lighten-4 mb-2">
                <div class="text-caption text-medium-emphasis">Signed in as</div>
                <div class="text-body-2 font-weight-bold text-truncate">{{ userName }}</div>
              </div>
              
              <v-list-item to="/" prepend-icon="mdi-view-dashboard-outline" title="仪表盘" density="compact" />
              <v-list-item title="个人设置" prepend-icon="mdi-cog-outline" density="compact" />
              <v-divider class="my-2"></v-divider>
              <v-list-item 
                title="退出登录" 
                prepend-icon="mdi-logout" 
                base-color="error"
                @click="onLogout" 
                density="compact"
              />
            </v-list>
          </v-menu>
        </template>
        
        <template v-else>
          <v-btn 
            to="/login" 
            color="primary" 
            variant="flat" 
            prepend-icon="mdi-login"
          >
            登录
          </v-btn>
        </template>
      </div>
    </v-app-bar>

    <!-- 主内容区 -->
    <v-main class="bg-background">
      <v-container fluid class="pa-6">
        <router-view v-slot="{ Component }">
          <transition name="fade" mode="out-in">
            <component :is="Component" />
          </transition>
        </router-view>
      </v-container>
    </v-main>
  </v-app>
</template>

<style scoped>
.gap-2 {
  gap: 8px;
}
.gap-3 {
  gap: 12px;
}
.my-sidebar :deep(.v-navigation-drawer__content) {
  /* 可以添加一些纹理或渐变，如果需要的话 */
}
</style>

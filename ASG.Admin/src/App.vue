<script setup>
import { ref, computed } from 'vue'
import { useDisplay } from 'vuetify'
import { currentUser, currentRole, isAuthenticated } from './stores/auth'
import { logout } from './services/auth'

const { smAndDown } = useDisplay()
const drawer = ref(false)
const loggedIn = computed(() => isAuthenticated.value)
const roleName = computed(() => currentRole.value?.DisplayName || currentRole.value?.RoleName || '')
const userName = computed(() => currentUser.value?.fullName || currentUser.value?.email || '')

function onLogout() { logout() }
</script>

<template>
  <v-app>
    <v-navigation-drawer v-model="drawer" :temporary="smAndDown" app>
      <v-list density="comfortable">
        <v-list-item title="管理后台" subtitle="ASG.Admin"></v-list-item>
        <v-divider class="mb-2"></v-divider>
        <v-list-item to="/" prepend-icon="mdi-view-dashboard" title="仪表盘" />
        <v-list-item to="/users" prepend-icon="mdi-account-multiple" title="用户管理" />
        <v-list-item to="/roles" prepend-icon="mdi-shield-account" title="角色与权限" />
        <v-list-item to="/events" prepend-icon="mdi-calendar" title="赛事管理" />
        <v-list-item to="/teams" prepend-icon="mdi-account-group" title="战队管理" />
        <v-list-item to="/matches" prepend-icon="mdi-sword-cross" title="比赛管理" />
        <v-list-item to="/articles" prepend-icon="mdi-text" title="文章管理" />
        <v-list-item to="/files" prepend-icon="mdi-file-upload" title="文件上传" />
        <v-list-item to="/about" prepend-icon="mdi-information" title="关于" />
      </v-list>
    </v-navigation-drawer>

    <v-app-bar app color="surface" flat :density="smAndDown ? 'compact' : 'comfortable'">
      <v-btn icon="mdi-menu" variant="text" @click="drawer = !drawer" aria-label="菜单" />
      <v-toolbar-title>管理后台</v-toolbar-title>
      <v-spacer></v-spacer>
      <template v-if="loggedIn">
        <v-chip class="mr-2" variant="tonal" color="primary" size="small">{{ roleName }}</v-chip>
        <v-menu>
          <template #activator="{ props }">
            <v-btn v-bind="props" variant="text" prepend-icon="mdi-account">{{ userName }}</v-btn>
          </template>
          <v-list>
            <v-list-item to="/" title="仪表盘" prepend-icon="mdi-view-dashboard" />
            <v-list-item title="退出登录" prepend-icon="mdi-logout" @click="onLogout" />
          </v-list>
        </v-menu>
      </template>
      <template v-else>
        <v-btn to="/login" variant="text">登录</v-btn>
      </template>
    </v-app-bar>

    <v-main>
      <router-view />
    </v-main>
  </v-app>
</template>

<style scoped>
.app-title {
  display: flex;
  align-items: center;
}
</style>

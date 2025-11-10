<script setup>
import { ref, computed } from 'vue'
import { useDisplay } from 'vuetify'
import { isAuthenticated, currentUser } from './stores/auth'
import { logout } from './services/auth'
import { notifyOpen, notifyText, notifyColor, notifyTimeout } from './stores/notify'

const loggedIn = computed(() => isAuthenticated.value)
const userName = computed(() => currentUser.value?.fullName || currentUser.value?.email || '')

function onLogout() {
  logout()
}

const { smAndDown } = useDisplay()
const drawer = ref(false)
</script>

<template>
  <v-app>
    <v-app-bar app color="surface" flat :density="smAndDown ? 'compact' : 'comfortable'">
      <v-toolbar-title class="app-title">
        <img src="/logo.svg" alt="平台Logo" class="app-logo" />
        <span>第五人格统一赛事平台</span>
      </v-toolbar-title>
      <v-spacer></v-spacer>
      <!-- 移动端：常用功能快捷入口（赛事、文章） -->
      <div class="d-sm-none d-flex align-center mr-1">
        <v-btn icon="grid_view" variant="text" :to="'/events'" aria-label="赛事看板" />
        <v-btn icon="list_alt" variant="text" :to="'/articles'" aria-label="文章列表" />
        <v-btn icon="group" variant="text" :to="'/teams/search'" aria-label="搜索战队" />
        <template v-if="loggedIn">
          <v-btn icon="person" variant="text" :to="'/profile'" aria-label="个人资料" />
        </template>
        <template v-else>
          <v-btn icon="login" variant="text" :to="'/login'" aria-label="登录" />
        </template>
      </div>
      <!-- 移动端：导航抽屉触发 -->
      <v-app-bar-nav-icon class="d-sm-none" @click="drawer = true" />
      <!-- 桌面端：顶部菜单 -->
      <div class="d-none d-sm-flex align-center">
        <v-btn to="/" variant="text">首页</v-btn>
        <v-btn to="/about" variant="text" prepend-icon="info">关于</v-btn>
        <v-menu>
          <template #activator="{ props }">
            <v-btn v-bind="props" variant="text" prepend-icon="group">参赛者</v-btn>
          </template>
          <v-list>
            <v-list-item link :to="'/events'" prepend-icon="grid_view" title="赛事看板" />
            <v-list-item link :to="'/teams/create'" prepend-icon="person_add" title="创建战队" />
            <v-list-item v-if="loggedIn" link :to="'/teams/edit'" prepend-icon="edit" title="编辑我的战队" />
            <v-list-item link :to="'/profile'" prepend-icon="person" title="个人资料" />
            <v-list-item link :to="'/teams/search'" prepend-icon="group" title="搜索战队" />
          </v-list>
        </v-menu>
        <v-menu>
          <template #activator="{ props }">
            <v-btn v-bind="props" variant="text" prepend-icon="article">社区</v-btn>
          </template>
          <v-list>
            <v-list-item link :to="'/articles'" prepend-icon="list_alt" title="文章列表" />
            <template v-if="loggedIn">
              <v-list-item link :to="'/articles/create'" prepend-icon="edit" title="发布文章" />
            </template>
          </v-list>
        </v-menu>
        <template v-if="loggedIn">
          <v-menu>
            <template #activator="{ props }">
              <v-btn v-bind="props" variant="text" prepend-icon="badge">赛事主办方</v-btn>
            </template>
            <v-list>
              <v-list-item link :to="'/events/create'" prepend-icon="add" title="创建赛事" />
              <v-list-item link :to="'/events/manage'" prepend-icon="settings" title="我的赛事" />
            </v-list>
          </v-menu>
        </template>
        <template v-if="!loggedIn">
          <v-btn to="/login" variant="text">登录</v-btn>
          <v-btn to="/register" variant="text">注册</v-btn>
        </template>
        <template v-else>
          <v-btn to="/profile" variant="text" prepend-icon="person">{{ userName }}</v-btn>
          <v-btn @click="onLogout" variant="text">退出</v-btn>
        </template>
      </div>
    </v-app-bar>
    <!-- 移动端导航抽屉 -->
    <v-navigation-drawer v-model="drawer" temporary>
      <v-toolbar flat density="compact">
        <v-toolbar-title class="drawer-title">
          <img src="/logo.svg" alt="平台Logo" class="drawer-logo" />
          <span>赛事平台</span>
        </v-toolbar-title>
        <v-spacer />
        <v-btn icon="close" variant="text" @click="drawer = false" />
      </v-toolbar>
      <v-divider />
      <v-list density="comfortable">
        <v-list-item link :to="'/'" prepend-icon="home" title="首页" @click="drawer=false" />
        <v-list-item link :to="'/about'" prepend-icon="info" title="关于" @click="drawer=false" />
        <v-list-subheader>参赛者</v-list-subheader>
        <v-list-item link :to="'/events'" prepend-icon="grid_view" title="赛事看板" @click="drawer=false" />
        <v-list-item link :to="'/teams/create'" prepend-icon="person_add" title="创建战队" @click="drawer=false" />
        <v-list-item v-if="loggedIn" link :to="'/teams/edit'" prepend-icon="edit" title="编辑我的战队" @click="drawer=false" />
        <v-list-item link :to="'/profile'" prepend-icon="person" title="个人资料" @click="drawer=false" />
        <v-list-item link :to="'/teams/search'" prepend-icon="group" title="搜索战队" @click="drawer=false" />
        <v-divider class="my-2" />
        <v-list-subheader>社区</v-list-subheader>
        <v-list-item link :to="'/articles'" prepend-icon="list_alt" title="文章列表" @click="drawer=false" />
        <template v-if="loggedIn">
          <v-list-item link :to="'/articles/create'" prepend-icon="edit" title="发布文章" @click="drawer=false" />
        </template>
        <template v-if="loggedIn">
          <v-divider class="my-2" />
          <v-list-subheader>赛事主办方</v-list-subheader>
          <v-list-item link :to="'/events/create'" prepend-icon="add" title="创建赛事" @click="drawer=false" />
          <v-list-item link :to="'/events/manage'" prepend-icon="settings" title="我的赛事" @click="drawer=false" />
          <v-divider class="my-2" />
          <v-list-item prepend-icon="logout" title="退出登录" @click="onLogout; drawer=false" />
        </template>
        <template v-else>
          <v-divider class="my-2" />
          <v-list-item link :to="'/login'" prepend-icon="login" title="登录" @click="drawer=false" />
          <v-list-item link :to="'/register'" prepend-icon="person_add" title="注册" @click="drawer=false" />
        </template>
      </v-list>
    </v-navigation-drawer>
    <v-main>
      <router-view />
    </v-main>
    <!-- 全局提示条：用于Token过期等系统级提醒 -->
    <v-snackbar v-model="notifyOpen" :timeout="notifyTimeout" :color="notifyColor" location="bottom right">
      <div class="d-flex align-center">
        <v-icon class="mr-2" icon="info" />
        {{ notifyText }}
      </div>
    </v-snackbar>
  </v-app>
</template>

<style scoped>
.app-title {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  white-space: nowrap;
}
.app-logo {
  width: 32px;
  height: 32px;
  display: inline-block;
}
.drawer-title {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  white-space: nowrap;
}
.drawer-logo {
  width: 28px;
  height: 28px;
  display: inline-block;
}
</style>

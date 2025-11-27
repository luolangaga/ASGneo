<script setup>
import { notifyOpen, notifyColor, notifyText, notifyTimeout, notify } from './stores/notify'
import { isAuthenticated } from './stores/auth'
import { unreadCount, refreshUnreadCount } from './stores/notifications'
import { onMounted, ref } from 'vue'
import { apiFetch } from './services/api'
onMounted(() => { refreshUnreadCount() })
const aiOpen = ref(false)
const aiCommand = ref('')
const aiRunning = ref(false)
const aiResults = ref([])
async function runAI() {
  const cmd = (aiCommand.value || '').trim()
  if (!cmd) { notify({ text: '请输入指令', color: 'warning' }); return }
  aiRunning.value = true
  try {
    const resp = await apiFetch('ai/command', { method: 'POST', body: { command: cmd } })
    aiResults.value = resp?.results || []
    notify({ text: '执行完成', color: 'success' })
  } catch (e) {
    notify({ text: e?.message || '执行失败', color: 'error' })
  } finally {
    aiRunning.value = false
  }
}
</script>

<template>
  <v-app>
    <v-app-bar color="surface" flat>
      <v-container class="d-flex align-center">
        <v-icon class="mr-2" icon="group" />
        <div class="text-subtitle-1 font-weight-bold">ASG.Person 人员管理</div>
        <v-spacer />
        <router-link to="/" class="mr-2">
          <v-btn variant="text" prepend-icon="home">首页</v-btn>
        </router-link>
        <router-link to="/recruitments">
          <v-btn variant="text" prepend-icon="work">招募</v-btn>
        </router-link>
        <router-link to="/me/applications" class="ml-2">
          <v-btn variant="text" prepend-icon="assignment">我的申请</v-btn>
        </router-link>
        <router-link to="/me/payroll" class="ml-2">
          <v-btn variant="text" prepend-icon="payments">工资结算</v-btn>
        </router-link>
        <router-link to="/organizer" class="ml-2">
          <v-btn variant="text" prepend-icon="dashboard">主办方</v-btn>
        </router-link>
        <router-link v-if="isAuthenticated" to="/notifications" class="ml-2">
          <v-badge :content="unreadCount" color="error" v-if="unreadCount">
            <v-btn variant="text" prepend-icon="notifications">通知</v-btn>
          </v-badge>
          <v-btn v-else variant="text" prepend-icon="notifications">通知</v-btn>
        </router-link>
        <router-link v-if="isAuthenticated" to="/chat" class="ml-2">
          <v-btn variant="text" prepend-icon="chat">聊天</v-btn>
        </router-link>
        <router-link v-if="!isAuthenticated" to="/login" class="ml-2">
          <v-btn variant="text" prepend-icon="login">登录</v-btn>
        </router-link>
      </v-container>
    </v-app-bar>
  <v-main>
    <router-view />
  </v-main>
  <v-snackbar v-model="notifyOpen" :timeout="notifyTimeout" :color="notifyColor">{{ notifyText }}</v-snackbar>
  <v-btn icon="smart_toy" color="primary" @click="aiOpen = true" style="position: fixed; right: 24px; bottom: 24px; z-index: 1000" />
  <v-dialog v-model="aiOpen" max-width="720">
    <v-card>
      <v-card-title class="d-flex align-center">
        <v-icon class="mr-2" icon="smart_toy" />
        <span>AI 助手</span>
        <v-spacer />
        <v-btn icon="close" variant="text" @click="aiOpen = false" />
      </v-card-title>
      <v-card-text>
        <v-textarea v-model="aiCommand" label="一句话指令" rows="3" auto-grow :disabled="aiRunning" />
        <div class="mt-2 d-flex align-center">
          <v-btn color="primary" :loading="aiRunning" @click="runAI">执行</v-btn>
          <v-spacer />
        </div>
        <div class="mt-4">
          <div v-if="aiResults && aiResults.length">
            <v-list density="compact">
              <v-list-item v-for="(r, i) in aiResults" :key="i">
                <v-list-item-title>
                  <span>{{ r.action }}</span>
                  <v-chip class="ml-2" :color="r.success ? 'success' : 'error'" size="small">{{ r.success ? '成功' : '失败' }}</v-chip>
                </v-list-item-title>
                <v-list-item-subtitle v-if="r.message">{{ r.message }}</v-list-item-subtitle>
                <div v-if="r.data" class="mt-2" style="white-space: pre-wrap; font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace; font-size: 12px;">
                  {{ typeof r.data === 'string' ? r.data : JSON.stringify(r.data, null, 2) }}
                </div>
              </v-list-item>
            </v-list>
          </div>
          <div v-else class="text-medium-emphasis">无结果</div>
        </div>
      </v-card-text>
    </v-card>
  </v-dialog>
</v-app>
</template>

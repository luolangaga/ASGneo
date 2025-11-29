<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import { useDisplay, useTheme } from 'vuetify'
import { isAuthenticated, currentUser } from './stores/auth'
import { isConnected } from './services/realtime'
import { logout } from './services/auth'
import { notifyOpen, notifyText, notifyColor, notifyTimeout, notify } from './stores/notify'
import { unreadCount, refreshUnreadCount } from './stores/notifications'
import { getConversations } from './services/messages'
import { executeCommand } from './services/ai'
import LoadingDialog from './components/LoadingDialog.vue'

const loggedIn = computed(() => isAuthenticated.value)
const route = useRoute()
const isHome = computed(() => route.name === 'home')
const userName = computed(() => currentUser.value?.fullName || currentUser.value?.email || '')
const userId = computed(() => currentUser.value?.id || currentUser.value?.Id || '')

function onLogout() {
  logout()
}

const { smAndDown } = useDisplay()
const drawer = ref(false)
const onboardingOpen = ref(false)
const onboardingRole = ref('')

const theme = useTheme()
const isDark = ref(false)
const messagesUnread = ref(0)
const realtimeDisabled = ref(false)
async function refreshMessagesUnread() {
  try {
    const convs = await getConversations()
    messagesUnread.value = (convs || []).reduce((sum, c) => sum + (c.unreadCount ?? c.UnreadCount ?? 0), 0)
  } catch { messagesUnread.value = 0 }
}
function applyThemeName(name) {
  try {
    if (theme && typeof theme.change === 'function') {
      theme.change(name)
    } else if (theme?.global?.name) {
      theme.global.name.value = name
    }
  } catch {
    try { theme.global.name.value = name } catch {}
  }
  isDark.value = name === 'md3Dark' || name === 'tapeFuturism'
}
function initTheme() {
  const saved = localStorage.getItem('site-theme')
  if (saved === 'md3Light' || saved === 'md3Dark' || saved === 'tapeFuturism') {
    applyThemeName(saved)
    return
  }
  if (saved === 'dark' || saved === 'light') {
    applyThemeName(saved === 'dark' ? 'md3Dark' : 'md3Light')
    return
  }
  const prefers = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches
  applyThemeName(prefers ? 'md3Dark' : 'md3Light')
}
function toggleTheme() {
  const next = isDark.value ? 'md3Light' : 'md3Dark'
  applyThemeName(next)
  localStorage.setItem('site-theme', next === 'md3Dark' ? 'dark' : 'light')
}
function setTheme(name) {
  applyThemeName(name)
  localStorage.setItem('site-theme', name)
}
onMounted(initTheme)
onMounted(() => {
  refreshUnreadCount()
  refreshMessagesUnread()
  try {
    window.addEventListener('asg:direct-message', refreshMessagesUnread)
    window.addEventListener('asg:notification', () => { refreshUnreadCount(); refreshMessagesUnread() })
    window.addEventListener('asg:conversations-changed', refreshMessagesUnread)
    window.addEventListener('asg:realtime-disabled', () => { realtimeDisabled.value = true; messagesUnread.value = 0 })
    window.addEventListener('asg:realtime-connected', () => { realtimeDisabled.value = false; refreshUnreadCount(); refreshMessagesUnread() })
  } catch {}
})
function tryOpenOnboarding() {
  try {
    const flag = localStorage.getItem('onboarding:welcome')
    if (flag === '1' && loggedIn.value) {
      onboardingOpen.value = true
      localStorage.removeItem('onboarding:welcome')
    }
  } catch {}
}
onMounted(tryOpenOnboarding)
watch(() => loggedIn.value, (v) => { if (v) tryOpenOnboarding() })

const aiOpen = ref(false)
const aiCommand = ref('')
const aiRunning = ref(false)
const aiResults = ref([])
const aiInputs = ref({})
const aiExpanded = ref({})
const aiTop = computed(() => (smAndDown.value ? '84px' : '96px'))
const actionLabels = {
  llm_response: 'AI说明',
  request_user_input: '补充参数',
  polish_text: '润色文本',
  bind_team_by_name: '绑定战队',
  like_team_by_name: '为战队点赞',
  register_team_to_event_by_names: '战队报名赛事',
  search_teams: '搜索战队',
  get_team_by_name: '战队详情',
  get_team_honors_by_name: '战队荣誉',
  search_events: '搜索赛事',
  list_active_events: '正在报名的赛事',
  list_upcoming_events: '即将开始的赛事',
  get_event_registrations_by_name: '赛事报名列表',
  get_my_events: '我的赛事',
  get_my_player: '我的玩家'
}
const actionIcons = {
  llm_response: 'info',
  request_user_input: 'help',
  polish_text: 'notes',
  bind_team_by_name: 'link',
  like_team_by_name: 'thumb_up',
  register_team_to_event_by_names: 'how_to_reg',
  search_teams: 'group',
  get_team_by_name: 'assignment_ind',
  get_team_honors_by_name: 'emoji_events',
  search_events: 'grid_view',
  list_active_events: 'event_available',
  list_upcoming_events: 'event',
  get_event_registrations_by_name: 'format_list_bulleted',
  get_my_events: 'badge',
  get_my_player: 'person'
}
function getActionLabel(a) { return actionLabels[a] || a }
function getActionIcon(a) { return actionIcons[a] || 'task' }
function get(obj, keys) {
  for (const k of keys) {
    const v = obj?.[k]
    if (v !== undefined && v !== null) return v
  }
  return undefined
}
function toJson(v) {
  try { return typeof v === 'string' ? v : JSON.stringify(v, null, 2) } catch { return '' }
}
function toggleExpand(i) { aiExpanded.value[i] = !aiExpanded.value[i] }
async function copyJson(data) {
  try { await navigator.clipboard.writeText(toJson(data)); notify({ text: '已复制', color: 'success' }) } catch {}
}
async function runAI() {
  const cmd = (aiCommand.value || '').trim()
  if (!cmd) { notify({ text: '请输入指令', color: 'warning' }); return }
  aiRunning.value = true
  try {
    const resp = await executeCommand({ command: cmd })
    aiResults.value = resp?.results || resp?.Results || []
    aiInputs.value = {}
    aiResults.value.forEach((_, i) => { aiInputs.value[i] = {} })
    notify({ text: '执行完成', color: 'success' })
  } catch (e) {
    notify({ text: e?.message || '执行失败', color: 'error' })
  } finally {
    aiRunning.value = false
  }
}
function buildCommandFor(r, inputs) {
  const oa = get(r.data || {}, ['original_action','OriginalAction'])
  const v = (k) => (inputs?.[k] || '').trim()
  if (oa === 'like_team_by_name') {
    const tn = v('team_name')
    return tn ? `为 "${tn}" 战队 点赞` : ''
  }
  if (oa === 'register_team_to_event_by_names') {
    const tn = v('team_name'), en = v('event_name'), notes = v('notes')
    if (!tn || !en) return ''
    return notes ? `为 "${tn}" 战队报名 赛事 "${en}"，备注 ${notes}` : `为 "${tn}" 战队报名 赛事 "${en}"`
  }
  if (oa === 'bind_team_by_name') {
    const nm = v('name'), pwd = v('password')
    if (!nm || !pwd) return ''
    return `绑定 战队 "${nm}" 密码 ${pwd}`
  }
  if (oa === 'get_team_by_name') {
    const nm = v('name')
    return nm ? `搜索战队 ${nm} 并查看详情` : ''
  }
  if (oa === 'get_team_honors_by_name') {
    const tn = v('team_name')
    return tn ? `查看战队荣誉 战队 "${tn}"` : ''
  }
  if (oa === 'search_teams') {
    const q = v('query')
    return q ? `搜索战队 ${q}` : ''
  }
  if (oa === 'search_events') {
    const q = v('query')
    return q ? `搜索赛事 ${q}` : ''
  }
  if (oa === 'get_event_registrations_by_name') {
    const en = v('event_name')
    return en ? `查看赛事报名列表 赛事 "${en}"` : ''
  }
  if (oa === 'polish_text') {
    const t = v('text'), s = v('scope') || 'general'
    return t ? `润色文本 ${t}` : ''
  }
  return ''
}
async function submitMissing(i) {
  const r = aiResults.value[i]
  const cmd = buildCommandFor(r, aiInputs.value[i])
  if (!cmd) { notify({ text: '请填写完整参数', color: 'warning' }); return }
  aiRunning.value = true
  try {
    const resp = await executeCommand({ command: cmd })
    const newResults = resp?.results || resp?.Results || []
    aiResults.value = aiResults.value.concat(newResults)
    newResults.forEach((_, idx) => { aiInputs.value[aiResults.value.length - newResults.length + idx] = {} })
    notify({ text: '已补充参数并执行', color: 'success' })
  } catch (e) {
    notify({ text: e?.message || '执行失败', color: 'error' })
  } finally {
    aiRunning.value = false
  }
}
</script>

<template>
  <v-app>
    <v-app-bar app flat :density="smAndDown ? 'compact' : 'comfortable'" class="glass-effect border-b">
      <v-toolbar-title class="app-title font-weight-bold">
           <router-link to="/" class="d-flex align-center text-decoration-none text-high-emphasis">
        <img src="/logo.svg" alt="平台Logo" class="app-logo mr-2" style="height: 32px; width: 32px;" />
          <span class="text-h6 font-weight-bold tracking-tight">第五人格统一赛事平台</span>
         </router-link>
      </v-toolbar-title>
      <v-spacer></v-spacer>
      <!-- 移动端：常用功能快捷入口（首页、赛事、帖子） -->
      <div class="d-sm-none d-flex align-center mr-1">
        <v-btn icon="home" variant="text" :to="'/'" aria-label="首页" />
        <v-btn icon="grid_view" variant="text" :to="'/events'" aria-label="赛事看板" />

        <v-btn icon="search" variant="text" :to="'/teams/search'" aria-label="全站搜索" />

        <v-menu>
          <template #activator="{ props }">
            <v-btn v-bind="props" icon="palette" variant="text" aria-label="主题" />
          </template>
          <v-list density="compact">
            <v-list-item @click="setTheme('md3Light')" title="亮色" prepend-icon="light_mode" />
            <v-list-item @click="setTheme('md3Dark')" title="暗色" prepend-icon="dark_mode" />
            <v-list-item @click="setTheme('tapeFuturism')" title="磁带未来主义" prepend-icon="auto_awesome" />
          </v-list>
        </v-menu>
        <template v-if="loggedIn">
          <v-btn icon="chat" variant="text" :to="'/chat'" aria-label="聊天" />
          <v-btn icon="person" variant="text" :to="'/profile'" aria-label="个人资料" />
        </template>
        <template v-else>
          <v-btn icon="login" variant="text" :to="'/login'" aria-label="登录" />
        </template>
      </div>
      <!-- 移动端：导航抽屉触发 -->
      <v-app-bar-nav-icon class="d-sm-none" @click="drawer = true" />
      <!-- 桌面端：顶部菜单 -->
      <div class="d-none d-sm-flex align-center gap-1">
        <v-btn to="/" variant="text" rounded="pill">首页</v-btn>
        <!-- 关于页面已移除 -->
        <v-menu open-on-hover>
          <template #activator="{ props }">
            <v-btn v-bind="props" variant="text" rounded="pill" prepend-icon="group" append-icon="expand_more">参赛者</v-btn>
          </template>
          <v-list density="comfortable" elevation="3" rounded="lg" class="mt-2">
            <v-list-item link :to="'/events'" prepend-icon="grid_view" title="赛事看板" />
            <v-list-item link :to="'/teams/create'" prepend-icon="person_add" title="创建战队" />
            <v-list-item v-if="loggedIn" link :to="'/teams/edit'" prepend-icon="edit" title="编辑我的战队" />
            <v-list-item link :to="'/stats'" prepend-icon="insights" title="数据分析" />
            <v-list-item link :to="'/profile'" prepend-icon="person" title="个人资料" />
            <v-list-item link :to="'/teams/search'" prepend-icon="search" title="全站搜索" />
          </v-list>
        </v-menu>
        <v-menu open-on-hover>
          <template #activator="{ props }">
            <v-btn v-bind="props" variant="text" rounded="pill" prepend-icon="article" append-icon="expand_more">社区</v-btn>
          </template>
          <v-list density="comfortable" elevation="3" rounded="lg" class="mt-2">
            <v-list-item link :to="'/articles'" prepend-icon="list_alt" title="帖子列表" />
            <template v-if="loggedIn">
              <v-list-item link :to="'/articles/create'" prepend-icon="edit" title="发布帖子" />
            </template>
          </v-list>
        </v-menu>
        <template v-if="loggedIn">
          <v-menu open-on-hover>
            <template #activator="{ props }">
              <v-btn v-bind="props" variant="text" rounded="pill" prepend-icon="badge" append-icon="expand_more">赛事主办方</v-btn>
            </template>
            <v-list density="comfortable" elevation="3" rounded="lg" class="mt-2">
              <v-list-item link :to="'/events/create'" prepend-icon="add" title="创建赛事" />
              <v-list-item link :to="'/events/manage'" prepend-icon="settings" title="我的赛事" />
            </v-list>
          </v-menu>
          
          <v-btn to="/messages" variant="text" rounded="pill" prepend-icon="chat" class="ml-1">
             信息
             <v-badge v-if="messagesUnread > 0 && !realtimeDisabled" :content="messagesUnread" color="error" inline></v-badge>
          </v-btn>
        </template>
        
        <v-divider vertical class="mx-2 my-auto" style="height: 24px" />
        
        <template v-if="!loggedIn">
          <v-btn to="/login" variant="text" rounded="pill">登录</v-btn>
          <v-btn to="/register" color="primary" variant="flat" rounded="pill" class="ml-2">注册</v-btn>
        </template>
        <template v-else>
          <v-menu open-on-hover>
             <template #activator="{ props }">
              <v-btn v-bind="props" variant="text" rounded="pill" prepend-icon="account_circle" :to="'/users/' + userId">
                 {{ userName }}
              </v-btn>
             </template>
             <v-list density="comfortable" elevation="3" rounded="lg" class="mt-2">
               <v-list-item link to="/profile" prepend-icon="person" title="个人资料" />
               <v-divider class="my-1" />
               <v-list-item @click="onLogout" prepend-icon="logout" title="退出登录" color="error" />
             </v-list>
          </v-menu>
        </template>

        <v-menu>
          <template #activator="{ props }">
            <v-btn v-bind="props" class="ml-1" icon="palette" variant="text" density="comfortable" aria-label="主题" />
          </template>
          <v-list density="compact" elevation="3" rounded="lg" class="mt-2">
            <v-list-item @click="setTheme('md3Light')" title="亮色" prepend-icon="light_mode" />
            <v-list-item @click="setTheme('md3Dark')" title="暗色" prepend-icon="dark_mode" />
            <v-list-item @click="setTheme('tapeFuturism')" title="磁带未来主义" prepend-icon="auto_awesome" />
          </v-list>
        </v-menu>
      </div>
    </v-app-bar>
    <!-- 移动端导航抽屉 -->
    <v-navigation-drawer v-model="drawer" temporary class="glass-effect border-e" color="transparent">
      <v-toolbar flat density="compact" color="transparent">
        <v-toolbar-title class="drawer-title font-weight-bold">
          <img src="/logo.svg" alt="平台Logo" class="drawer-logo" />
          <span>赛事平台</span>
        </v-toolbar-title>
        <v-spacer />
        <v-btn icon="close" variant="text" @click="drawer = false" />
      </v-toolbar>
      <v-divider />
        <v-list density="comfortable">
          <v-list-item link :to="'/'" prepend-icon="home" title="首页" @click="drawer=false" />
        <!-- 关于页面已移除 -->
        <v-list-subheader>参赛者</v-list-subheader>
        <v-list-item link :to="'/events'" prepend-icon="grid_view" title="赛事看板" @click="drawer=false" />
        <v-list-item link :to="'/teams/create'" prepend-icon="person_add" title="创建战队" @click="drawer=false" />
        <v-list-item v-if="loggedIn" link :to="'/teams/edit'" prepend-icon="edit" title="编辑我的战队" @click="drawer=false" />

        <v-list-item link :to="'/profile'" prepend-icon="person" title="个人资料" @click="drawer=false" />
        <v-list-item link :to="'/teams/search'" prepend-icon="search" title="全站搜索" @click="drawer=false" />
          <v-divider class="my-2" />
          <v-list-subheader>协议</v-list-subheader>
          <v-list-item link :to="'/terms'" prepend-icon="description" title="用户协议" @click="drawer=false" />
          <v-list-item link :to="'/privacy'" prepend-icon="privacy_tip" title="隐私政策" @click="drawer=false" />

          <v-divider class="my-2" />
          <v-list-subheader>主题</v-list-subheader>
          <v-list-item @click="setTheme('md3Light'); drawer=false" prepend-icon="light_mode" title="亮色" />
          <v-list-item @click="setTheme('md3Dark'); drawer=false" prepend-icon="dark_mode" title="暗色" />
          <v-list-item @click="setTheme('tapeFuturism'); drawer=false" prepend-icon="auto_awesome" title="磁带未来主义" />
          <template v-if="loggedIn">
            <v-list-item link :to="'/messages'" prepend-icon="chat" title="信息" @click="drawer=false">
              <template #append>
                <v-chip v-if="messagesUnread > 0 && !realtimeDisabled" color="error" size="x-small">{{ messagesUnread }}</v-chip>
              </template>
          </v-list-item>
          <v-divider class="my-2" />
          <v-list-subheader>赛事主办方</v-list-subheader>
          <v-list-item link :to="'/events/create'" prepend-icon="add" title="创建赛事" @click="drawer=false" />
          <v-list-item link :to="'/events/manage'" prepend-icon="settings" title="我的赛事" @click="drawer=false" />
          <v-divider class="my-2" />
          <v-list-item prepend-icon="logout" title="退出登录" @click="onLogout(); drawer=false" />
        </template>
        <template v-else>
          <v-divider class="my-2" />
          <v-list-item link :to="'/login'" prepend-icon="login" title="登录" @click="drawer=false" />
          <v-list-item link :to="'/register'" prepend-icon="person_add" title="注册" @click="drawer=false" />
        </template>
      </v-list>
    </v-navigation-drawer>
    <v-main :class="{ 'ads-disabled': isHome }">
      <router-view v-slot="{ Component }">
        <transition name="fade">
          <component :is="Component" />
        </transition>
      </router-view>
    </v-main>
    <!-- 全局提示条：用于Token过期等系统级提醒 -->
    <v-snackbar v-model="notifyOpen" :timeout="notifyTimeout" :color="notifyColor" location="bottom right">
      <div class="d-flex align-center">
        <v-icon class="mr-2" icon="info" />
        {{ notifyText }}
      </div>
    </v-snackbar>
    <v-btn icon="smart_toy" color="primary" elevation="4" @click="aiOpen = true" :style="{ position: 'fixed', right: '24px', top: aiTop, zIndex: 1000 }" class="glass-effect" />
    <v-dialog v-model="aiOpen" max-width="720">
      <v-card rounded="xl" elevation="10" class="glass-effect">
        <v-card-title class="d-flex align-center pa-4">
          <v-icon class="mr-2" icon="smart_toy" color="primary" />
          <span class="font-weight-bold">AI 助手</span>
          <v-spacer />
          <v-btn icon="close" variant="text" @click="aiOpen = false" />
        </v-card-title>
        <v-card-text class="pa-4">
          <v-textarea v-model="aiCommand" label="一句话指令" rows="3" auto-grow :disabled="aiRunning" variant="outlined" hide-details="auto" class="mb-4" placeholder="例如：帮我查找XX战队..." />
          <div class="d-flex align-center">
            <v-btn color="primary" size="large" :loading="aiRunning" @click="runAI" prepend-icon="send" class="flex-grow-1">执行</v-btn>
          </div>
            <div class="mt-4">
              <div v-if="aiResults && aiResults.length">
                <v-list density="comfortable">
                  <v-list-item v-for="(r, i) in aiResults" :key="i">
                    <template #prepend>
                      <v-icon :icon="getActionIcon(r.action)" />
                    </template>
                    <v-list-item-title>
                      <span>{{ getActionLabel(r.action) }}</span>
                      <v-chip class="ml-2" :color="r.success ? 'success' : 'error'" size="small">{{ r.success ? '成功' : '失败' }}</v-chip>
                    </v-list-item-title>
                    <v-list-item-subtitle v-if="r.message">{{ r.message }}</v-list-item-subtitle>
                    <div class="mt-2">
                      <div v-if="r.action === 'llm_response'">
                        <v-alert type="info" variant="tonal">{{ get(r.data || {}, ['text','Text']) || '' }}</v-alert>
                      </div>
                      <div v-else-if="r.action === 'like_team_by_name'">
                        <div>已为 {{ get(r.data || {}, ['teamName','TeamName']) || '' }} 点赞，当前点赞数：{{ get(r.data || {}, ['likes','Likes']) ?? '' }}</div>
                      </div>
                      <div v-else-if="r.action === 'get_team_by_name' && r.data">
                        <div class="d-flex align-center">
                          <v-avatar v-if="get(r.data, ['logoUrl','LogoUrl'])" size="36">
                            <v-img :src="get(r.data, ['logoUrl','LogoUrl'])" cover>
                              <template #placeholder>
                                <div class="d-flex align-center justify-center" style="width:100%;height:100%">
                                  <lottie-player src="/animations/loading.json" background="transparent" speed="1" loop autoplay style="width:36px;height:36px"></lottie-player>
                                </div>
                              </template>
                            </v-img>
                          </v-avatar>
                          <div class="ml-2">
                            <div class="font-weight-medium">{{ get(r.data, ['name','Name']) }}</div>
                            <div class="text-medium-emphasis">{{ get(r.data, ['description','Description']) }}</div>
                            <v-chip class="mt-1" color="primary" size="small">点赞 {{ get(r.data, ['likes','Likes']) ?? 0 }}</v-chip>
                          </div>
                        </div>
                      </div>
                      <div v-else-if="r.action === 'search_teams' && (get(r.data||{}, ['items','Items']) || []).length">
                        <v-list density="compact">
                          <v-list-item v-for="t in get(r.data||{}, ['items','Items'])" :key="get(t,['id','Id'])" :to="'/teams/' + get(t,['id','Id'])">
                            <template #prepend>
                              <v-avatar v-if="get(t,['logoUrl','LogoUrl'])" size="28">
                                <v-img :src="get(t,['logoUrl','LogoUrl'])" cover>
                                  <template #placeholder>
                                    <div class="d-flex align-center justify-center" style="width:100%;height:100%">
                                      <lottie-player src="/animations/loading.json" background="transparent" speed="1" loop autoplay style="width:28px;height:28px"></lottie-player>
                                    </div>
                                  </template>
                                </v-img>
                              </v-avatar>
                            </template>
                            <v-list-item-title>{{ get(t,['name','Name']) }}</v-list-item-title>
                            <v-list-item-subtitle>
                              <span class="text-medium-emphasis">{{ get(t,['description','Description']) || '' }}</span>
                              <v-chip class="ml-2" size="x-small" color="primary">赞 {{ get(t,['likes','Likes']) ?? 0 }}</v-chip>
                            </v-list-item-subtitle>
                          </v-list-item>
                        </v-list>
                      </div>
                      <div v-else-if="(r.action === 'search_events' || r.action === 'list_active_events' || r.action === 'list_upcoming_events') && (get(r.data||{}, ['items','Items']) || []).length">
                        <v-list density="compact">
                          <v-list-item v-for="e in get(r.data||{}, ['items','Items'])" :key="get(e,['id','Id'])" :to="'/events/' + get(e,['id','Id'])">
                            <template #prepend>
                              <v-avatar v-if="get(e,['logoUrl','LogoUrl'])" size="28">
                                <v-img :src="get(e,['logoUrl','LogoUrl'])" cover>
                                  <template #placeholder>
                                    <div class="d-flex align-center justify-center" style="width:100%;height:100%">
                                      <lottie-player src="/animations/loading.json" background="transparent" speed="1" loop autoplay style="width:28px;height:28px"></lottie-player>
                                    </div>
                                  </template>
                                </v-img>
                              </v-avatar>
                            </template>
                            <v-list-item-title>{{ get(e,['name','Name']) }}</v-list-item-title>
                            <v-list-item-subtitle>
                              <span class="text-medium-emphasis">{{ get(e,['description','Description']) || '' }}</span>
                              <v-chip class="ml-2" size="x-small" color="primary">报名 {{ get(e,['registeredTeamsCount','RegisteredTeamsCount']) ?? 0 }}</v-chip>
                            </v-list-item-subtitle>
                          </v-list-item>
                        </v-list>
                      </div>
                      <div v-else-if="r.action === 'get_event_registrations_by_name' && Array.isArray(r.data)">
                        <v-list density="compact">
                          <v-list-item v-for="rg in r.data" :key="get(rg,['teamId','TeamId']) + '-' + get(rg,['eventId','EventId'])">
                            <v-list-item-title>{{ get(rg,['teamName','TeamName']) }}</v-list-item-title>
                            <v-list-item-subtitle>
                              <span class="text-medium-emphasis">{{ get(rg,['status','Status']) }}</span>
                              <span class="ml-2">{{ get(rg,['notes','Notes']) || '' }}</span>
                            </v-list-item-subtitle>
                          </v-list-item>
                        </v-list>
                      </div>
                      <div v-else-if="r.action === 'get_my_events' && Array.isArray(r.data)">
                        <v-list density="compact">
                          <v-list-item v-for="e in r.data" :key="get(e,['id','Id'])">
                            <v-list-item-title>{{ get(e,['name','Name']) }}</v-list-item-title>
                            <v-list-item-subtitle class="text-medium-emphasis">{{ get(e,['description','Description']) || '' }}</v-list-item-subtitle>
                          </v-list-item>
                        </v-list>
                      </div>
                      <div v-else-if="r.action === 'get_my_player' && r.data">
                        <div>{{ get(r.data,['name','Name']) }}</div>
                        <div class="text-medium-emphasis">{{ get(r.data,['gameRank','GameRank']) || '' }}</div>
                        <div class="text-medium-emphasis">{{ get(r.data,['description','Description']) || '' }}</div>
                      </div>
                      <div v-else-if="r.action === 'register_team_to_event_by_names' && r.data">
                        <div>已为 {{ get(r.data,['teamName','TeamName']) || '' }} 报名 {{ get(r.data,['eventName','EventName']) || '' }}</div>
                        <div class="text-medium-emphasis">{{ get(r.data,['status','Status']) || '' }}</div>
                      </div>
                      <div v-else-if="r.action === 'polish_text'">
                        <v-alert type="success" variant="tonal">{{ get(r.data||{}, ['text','Text']) || '' }}</v-alert>
                      </div>
                      <div v-else-if="r.action === 'request_user_input'">
                        <div>
                          <div v-for="f in (get(r.data||{}, ['fields','Fields']) || [])" :key="get(f,['name','Name'])">
                            <v-text-field v-model="aiInputs[i][get(f,['name','Name'])]" :label="get(f,['label','Label']) || get(f,['name','Name'])" :placeholder="get(f,['example','Example']) || ''" />
                          </div>
                          <v-btn color="primary" @click="submitMissing(i)">提交补充</v-btn>
                        </div>
                      </div>
                      <div v-else-if="r.data">
                        <div style="white-space: pre-wrap; font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace; font-size: 12px;">{{ toJson(r.data) }}</div>
                      </div>
                      <div class="d-flex align-center mt-2">
                        <v-btn size="small" variant="text" prepend-icon="content_copy" @click="copyJson(r.data)">复制JSON</v-btn>
                        <v-btn size="small" variant="text" prepend-icon="unfold_more" @click="toggleExpand(i)">{{ aiExpanded[i] ? '收起原始' : '展开原始' }}</v-btn>
                      </div>
                      <div v-if="aiExpanded[i] && r && r.data" class="mt-2" style="white-space: pre-wrap; font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace; font-size: 12px;">
                        {{ toJson(r) }}
                      </div>
                    </div>
                  </v-list-item>
                </v-list>
              </div>
              <div v-else class="text-medium-emphasis">无结果</div>
            </div>
        </v-card-text>
      </v-card>
    </v-dialog>
    <v-dialog v-model="onboardingOpen" max-width="720">
      <v-card>
        <v-card-title class="d-flex align-center">
          <v-icon class="mr-2" icon="school" />
          <span>新手引导</span>
          <v-spacer />
          <v-btn icon="close" variant="text" @click="onboardingOpen = false" />
        </v-card-title>
        <v-card-text>
          <div v-if="!onboardingRole">
            <div class="text-subtitle-1 mb-3">请选择您的身份</div>
            <div class="d-flex align-center">
              <v-btn color="primary" prepend-icon="badge" @click="onboardingRole = 'organizer'">我是主办方</v-btn>
              <v-btn class="ml-2" variant="tonal" prepend-icon="group" @click="onboardingRole = 'captain'">我是队长</v-btn>
            </div>
          </div>
          <div v-else-if="onboardingRole === 'organizer'">
            <div class="text-subtitle-1 mb-2">主办方快速上手</div>
            <v-list density="comfortable">
              <v-list-item link :to="'/events/create'" prepend-icon="add" title="创建赛事" />
              <v-list-item link :to="'/events/manage'" prepend-icon="settings" title="我的赛事管理" />
              <v-list-item link :to="'/events/manage'" prepend-icon="calendar_month" title="赛程管理">
                <template #subtitle>
                  进入某个赛事后打开“赛程”进行对阵安排
                </template>
              </v-list-item>
            </v-list>
            <div class="d-flex align-center mt-2">
              <v-btn variant="text" prepend-icon="arrow_back" @click="onboardingRole = ''">返回选择</v-btn>
              <v-spacer />
              <v-btn color="primary" @click="onboardingOpen = false">关闭</v-btn>
            </div>
          </div>
          <div v-else-if="onboardingRole === 'captain'">
            <div class="text-subtitle-1 mb-2">队长快速上手</div>
            <v-list density="comfortable">
              <v-list-item link :to="'/teams/create'" prepend-icon="person_add" title="创建战队" />
              <v-list-item link :to="'/events'" prepend-icon="grid_view" title="浏览赛事并报名">
                <template #subtitle>
                  打开赛事详情页，选择“报名”加入比赛
                </template>
              </v-list-item>
              <v-list-item link :to="'/messages'" prepend-icon="chat" title="查看消息与通知" />
            </v-list>
            <div class="d-flex align-center mt-2">
              <v-btn variant="text" prepend-icon="arrow_back" @click="onboardingRole = ''">返回选择</v-btn>
              <v-spacer />
              <v-btn color="primary" @click="onboardingOpen = false">关闭</v-btn>
            </div>
          </div>
        </v-card-text>
      </v-card>
    </v-dialog>
    <LoadingDialog />
    <v-footer app class="px-4 py-3 text-center d-flex flex-column" color="transparent" border="t">
      <div class="text-caption text-medium-emphasis">
        &copy; {{ new Date().getFullYear() }} 第五人格统一赛事平台. All rights reserved.
      </div>
      <div class="text-caption text-disabled mt-1">
        免责声明：几乎所有赛事都是来自民间玩家自己举办，请各位侦探注意辨别，以防被骗！
      </div>
    </v-footer>
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

.ads-disabled :deep(.google-auto-placed) {
  display: none !important;
}
</style>

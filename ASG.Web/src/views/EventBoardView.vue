<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { getActiveRegistrationEvents, getUpcomingEvents, registerTeamToEvent, getActiveRegistrationEventsPaged, getUpcomingEventsPaged } from '../services/events'
import { getTeam, uploadTeamLogo } from '../services/teams'
import { currentUser, isAuthenticated } from '../stores/auth'
import PageHero from '../components/PageHero.vue'
import { renderMarkdown } from '../utils/markdown'

const router = useRouter()
const loadingActive = ref(false)
const loadingUpcoming = ref(false)
const registering = ref(false)
const errorMsg = ref('')
const successMsg = ref('')
const showSuccess = ref(false)
const activeEvents = ref([])
const upcomingEvents = ref([])
// 分页状态：正在报名
const activePage = ref(1)
const activePageSize = ref(12)
const activeTotalCount = ref(0)
const activeMaxPage = computed(() => Math.max(1, Math.ceil(activeTotalCount.value / activePageSize.value)))
// 分页状态：即将开始
const upcomingPage = ref(1)
const upcomingPageSize = ref(12)
const upcomingTotalCount = ref(0)
const upcomingMaxPage = computed(() => Math.max(1, Math.ceil(upcomingTotalCount.value / upcomingPageSize.value)))
const showTeamLogoDialog = ref(false)
const teamLogoFile = ref(null)
const teamLogoError = ref('')
const teamLogoUploading = ref(false)
const pendingEventId = ref(null)
const expandedCards = ref({})
const searchText = ref('')

const loggedIn = computed(() => isAuthenticated.value)
const teamId = computed(() => currentUser.value?.teamId || null)

async function loadActive() {
  loadingActive.value = true
  try {
    const res = await getActiveRegistrationEventsPaged({ page: activePage.value, pageSize: activePageSize.value })
    const items = res.Items || res.items || []
    activeEvents.value = items
    activeTotalCount.value = res.TotalCount ?? res.totalCount ?? items.length
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载正在报名赛事失败'
  } finally {
    loadingActive.value = false
  }
}

async function loadUpcoming() {
  loadingUpcoming.value = true
  try {
    const res = await getUpcomingEventsPaged({ page: upcomingPage.value, pageSize: upcomingPageSize.value })
    const items = res.Items || res.items || []
    upcomingEvents.value = items
    upcomingTotalCount.value = res.TotalCount ?? res.totalCount ?? items.length
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载即将开始赛事失败'
  } finally {
    loadingUpcoming.value = false
  }
}

onMounted(() => {
  loadActive()
  loadUpcoming()
})

function toMd(s) {
  return renderMarkdown(s || '')
}

function goCreateEvent() {
  if (!loggedIn.value) {
    router.push('/login')
    return
  }
  router.push('/events/create')
}

function isExpanded(id) {
  return !!expandedCards.value[id]
}
function toggleExpanded(id) {
  expandedCards.value[id] = !expandedCards.value[id]
}

async function onRegister(eventId) {
  if (!loggedIn.value) {
    router.push('/login')
    return
  }
  if (!teamId.value) {
    errorMsg.value = '您还没有战队，请先创建战队后再报名'
    router.push('/teams/create')
    return
  }
  // 检查队伍是否已有Logo
  errorMsg.value = ''
  try {
    const team = await getTeam(teamId.value)
    const hasLogo = !!(team?.logoUrl || team?.LogoUrl)
    if (!hasLogo) {
      pendingEventId.value = eventId
      showTeamLogoDialog.value = true
      return
    }
  } catch (e) {
    console.warn('获取队伍信息失败，跳过Logo检查', e)
  }
  registering.value = true
  try {
    await registerTeamToEvent(eventId, { teamId: teamId.value })
    successMsg.value = '报名提交成功，请等待审核'
    showSuccess.value = true
    await loadActive()
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '报名失败'
  } finally {
    registering.value = false
  }
}

function onTeamLogoSelected(files) {
  teamLogoError.value = ''
  const file = Array.isArray(files) ? files[0] : files
  if (!file) { teamLogoFile.value = null; return }
  const okTypes = ['image/png', 'image/jpeg', 'image/jpg', 'image/webp']
  if (!okTypes.includes(file.type)) { teamLogoError.value = '文件类型不支持'; teamLogoFile.value = null; return }
  if (file.size > 5 * 1024 * 1024) { teamLogoError.value = '文件过大，最大5MB'; teamLogoFile.value = null; return }
  teamLogoFile.value = file
}

async function confirmUploadAndRegister() {
  teamLogoError.value = ''
  if (!pendingEventId.value) { showTeamLogoDialog.value = false; return }
  if (!teamLogoFile.value) { teamLogoError.value = '请先选择队伍Logo'; return }
  teamLogoUploading.value = true
  try {
    await uploadTeamLogo(teamId.value, teamLogoFile.value)
    showTeamLogoDialog.value = false
    registering.value = true
    await registerTeamToEvent(pendingEventId.value, { teamId: teamId.value })
    successMsg.value = '已上传队伍Logo并报名成功'
    showSuccess.value = true
    await loadActive()
  } catch (err) {
    teamLogoError.value = err?.payload?.message || err?.message || '上传队伍Logo失败'
  } finally {
    teamLogoUploading.value = false
    registering.value = false
  }
}
// 搜索过滤：按名称或描述
const normalized = (s) => String(s || '').toLowerCase().trim()
const filteredActiveEvents = computed(() => {
  const q = normalized(searchText.value)
  if (!q) return activeEvents.value
  return activeEvents.value.filter(ev => {
    const name = normalized(ev.name || ev.Name)
    const desc = normalized(ev.description || ev.Description)
    return name.includes(q) || desc.includes(q)
  })
})
const filteredUpcomingEvents = computed(() => {
  const q = normalized(searchText.value)
  if (!q) return upcomingEvents.value
  return upcomingEvents.value.filter(ev => {
    const name = normalized(ev.name || ev.Name)
    const desc = normalized(ev.description || ev.Description)
    return name.includes(q) || desc.includes(q)
  })
})
</script>

<template>
  <PageHero title="赛事看板" subtitle="浏览正在报名与即将开始的赛事" icon="grid_view">
    <template #actions>
      <v-btn color="primary" class="mr-3 mb-3" @click="goCreateEvent" prepend-icon="add">创建赛事</v-btn>
      <v-btn variant="text" class="mb-3" to="/profile" prepend-icon="person">个人资料</v-btn>
    </template>
  </PageHero>
  <v-container class="py-8">
    <v-text-field
      v-model="searchText"
      class="mb-6"
      density="comfortable"
      clearable
      prepend-inner-icon="search"
      label="搜索赛事（按名称或描述）"
      placeholder="输入关键字进行筛选"
    />

    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
    <v-snackbar v-model="showSuccess" color="success" :timeout="3000" location="bottom right">
      <div class="d-flex align-center">
        <v-icon class="mr-2" icon="check_circle" />
        {{ successMsg || '操作成功' }}
      </div>
    </v-snackbar>

    <v-card class="mb-8">
      <v-card-title>正在报名的赛事</v-card-title>
      <v-card-text>
        <v-progress-linear v-if="loadingActive" indeterminate color="primary" />
        <div class="d-flex justify-space-between text-medium-emphasis mb-2" v-if="!loadingActive">
          <div>共 {{ activeTotalCount }} 条</div>
          <div>第 {{ activePage }} 页 / 每页 {{ activePageSize }} 条</div>
        </div>
        <v-row v-if="!loadingActive">
          <v-col v-for="ev in filteredActiveEvents" :key="ev.id" cols="12" sm="6" md="4" lg="3">
            <v-card>
              <v-card-title class="d-flex align-center">
                <v-avatar size="36" class="mr-2">
                  <v-img v-if="ev.logoUrl || ev.LogoUrl" :src="ev.logoUrl || ev.LogoUrl" alt="event logo" cover />
                  <v-icon v-else icon="emoji_events" size="28" />
                </v-avatar>
                <span class="text-truncate">{{ ev.name }}</span>
              </v-card-title>
              <v-card-subtitle>
                报名截止：{{ new Date(ev.registrationEndTime).toLocaleString() }}
              </v-card-subtitle>
              <v-card-text>
                <div v-if="ev.description" :class="['md-content', { 'md-truncate': !isExpanded(ev.id) }]" v-html="toMd(ev.description)"></div>
                <div v-else class="text-medium-emphasis">暂无赛事描述</div>
                <div v-if="(ev.description?.length || 0) > 220" class="mt-1">
                  <v-btn size="x-small" variant="text" @click="toggleExpanded(ev.id)">{{ isExpanded(ev.id) ? '收起' : '展开' }}</v-btn>
                </div>
              </v-card-text>
              <v-card-actions>
                <v-btn :loading="registering" color="primary" @click="onRegister(ev.id)" prepend-icon="task_alt">报名</v-btn>
                <v-spacer />
                <v-btn color="default" variant="text" :to="`/events/${ev.id}`" prepend-icon="visibility">查看详情</v-btn>
              </v-card-actions>
            </v-card>
          </v-col>
        </v-row>
        <div class="d-flex justify-center mt-4" v-if="activeTotalCount > activePageSize">
          <v-pagination v-model="activePage" :length="activeMaxPage" total-visible="7" @update:modelValue="loadActive" />
        </div>
      </v-card-text>
    </v-card>

    <v-card>
      <v-card-title>即将开始的赛事</v-card-title>
      <v-card-text>
        <v-progress-linear v-if="loadingUpcoming" indeterminate color="primary" />
        <div class="d-flex justify-space-between text-medium-emphasis mb-2" v-if="!loadingUpcoming">
          <div>共 {{ upcomingTotalCount }} 条</div>
          <div>第 {{ upcomingPage }} 页 / 每页 {{ upcomingPageSize }} 条</div>
        </div>
        <v-row v-if="!loadingUpcoming">
          <v-col v-for="ev in filteredUpcomingEvents" :key="ev.id" cols="12" sm="6" md="4" lg="3">
            <v-card>
              <v-card-title class="d-flex align-center">
                <v-avatar size="36" class="mr-2">
                  <v-img v-if="ev.logoUrl || ev.LogoUrl" :src="ev.logoUrl || ev.LogoUrl" alt="event logo" cover />
                  <v-icon v-else icon="emoji_events" size="28" />
                </v-avatar>
                <span class="text-truncate">{{ ev.name }}</span>
              </v-card-title>
              <v-card-subtitle>
                开始时间：{{ new Date(ev.competitionStartTime).toLocaleString() }}
              </v-card-subtitle>
              <v-card-text>
                <div v-if="ev.description" :class="['md-content', { 'md-truncate': !isExpanded(ev.id) }]" v-html="toMd(ev.description)"></div>
                <div v-else class="text-medium-emphasis">暂无赛事描述</div>
                <div v-if="(ev.description?.length || 0) > 220" class="mt-1">
                  <v-btn size="x-small" variant="text" @click="toggleExpanded(ev.id)">{{ isExpanded(ev.id) ? '收起' : '展开' }}</v-btn>
                </div>
              </v-card-text>
              <v-card-actions>
                <v-btn color="default" variant="text" :to="`/events/${ev.id}`" prepend-icon="visibility">查看详情</v-btn>
              </v-card-actions>
            </v-card>
          </v-col>
        </v-row>
        <div class="d-flex justify-center mt-4" v-if="upcomingTotalCount > upcomingPageSize">
          <v-pagination v-model="upcomingPage" :length="upcomingMaxPage" total-visible="7" @update:modelValue="loadUpcoming" />
        </div>
      </v-card-text>
    </v-card>
  </v-container>
  
  <!-- 报名前上传队伍Logo -->
  <v-dialog v-model="showTeamLogoDialog" max-width="520">
    <v-card>
      <v-card-title>报名前需上传队伍Logo</v-card-title>
      <v-card-text>
        <div class="text-body-2 mb-2">为提升识别度与审核效率，请先上传队伍Logo（png/jpg/webp，≤5MB）。</div>
        <v-file-input
          prepend-icon="add_photo_alternate"
          density="comfortable"
          accept="image/png, image/jpeg, image/jpg, image/webp"
          show-size
          :disabled="teamLogoUploading"
          :loading="teamLogoUploading"
          @update:modelValue="onTeamLogoSelected"
          label="选择图片文件"
        />
        <v-alert v-if="teamLogoError" type="error" :text="teamLogoError" class="mt-2" />
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="showTeamLogoDialog = false">取消</v-btn>
        <v-btn color="primary" :loading="teamLogoUploading || registering" @click="confirmUploadAndRegister" prepend-icon="check_circle">上传并报名</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

</template>

<style scoped>
</style>
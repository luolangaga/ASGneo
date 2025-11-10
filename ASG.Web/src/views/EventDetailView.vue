<script setup>
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { renderMarkdown } from '../utils/markdown'
import { useRoute, useRouter } from 'vue-router'
import { getEvent, getEventRegistrations, exportEventRegistrationsCsv, registerTeamToEvent, uploadEventLogo, updateTeamRegistrationStatus, setEventChampion } from '../services/events'
import { getTeam, uploadTeamLogo } from '../services/teams'
import { getUser } from '../services/user'
import { currentUser, isAuthenticated } from '../stores/auth'
import PageHero from '../components/PageHero.vue'

const route = useRoute()
const router = useRouter()
const eventId = route.params.id
const loading = ref(false)
const errorMsg = ref('')
const ev = ref(null)
const creatorName = ref('')
const loadingRegs = ref(false)
const regsError = ref('')
const registrations = ref([])
const teamDetails = ref({}) // teamId -> TeamDto
const loadingTeamIds = ref(new Set())
const registering = ref(false)
const actionError = ref('')
const successMsg = ref('')
const showSuccess = ref(false)
const shareDialog = ref(false)
const uploadingLogo = ref(false)
const uploadLogoError = ref('')
const showTeamLogoDialog = ref(false)
const teamLogoFile = ref(null)
const teamLogoError = ref('')
const teamLogoUploading = ref(false)
const pendingTeamId = ref(null)

// 冠军板块相关状态
const championTeam = ref(null)
const loadingChampion = ref(false)
const championDialog = ref(false)
const selectedChampionTeamId = ref(null)
const settingChampion = ref(false)
const championError = ref('')
const championPlayers = computed(() => {
  const t = championTeam.value
  return t ? (t.players || t.Players || []) : []
})
const championDescription = computed(() => {
  const t = championTeam.value
  return t ? (t.description || t.Description || '') : ''
})
const championDescriptionHtml = computed(() => renderMarkdown(championDescription.value || ''))

const eventDescription = computed(() => ev.value?.description || ev.value?.Description || '')
const eventDescriptionHtml = computed(() => renderMarkdown(eventDescription.value || ''))
const creatorUserId = computed(() => ev.value?.createdByUserId || ev.value?.CreatedByUserId || '')
const creatorDisplay = computed(() => {
  if (creatorName.value) return creatorName.value
  return creatorUserId.value || '未知创建者'
})

// 审核备注弹窗（通过可选、拒绝必填）
// 已移除审核备注弹窗相关状态

// 报名期倒计时与状态
const nowMs = ref(Date.now())
let regTimer = null

const shareLink = computed(() => {
  const origin = window.location?.origin || ''
  const id = eventId || ev.value?.id || ev.value?.Id
  return `${origin}/events/${id}`
})

const heroTitle = computed(() => ev.value?.name || '赛事详情')

const canManageEvent = computed(() => {
  if (!isAuthenticated.value || !ev.value) return false
  const me = currentUser.value
  if (!me) return false
  const createdByUserId = ev.value.createdByUserId || ev.value.CreatedByUserId
  const myId = me.id || me.Id
  const roleName = me.roleName || me.RoleName
  const isCreator = !!createdByUserId && !!myId && createdByUserId === myId
  const isAdmin = roleName === 'Admin' || roleName === 'SuperAdmin'
  return isCreator || isAdmin
})

function normalizeStatus(status) {
  if (typeof status === 'string') return status
  switch (status) {
    case 0: return 'Pending'
    case 1: return 'Registered'
    case 2: return 'Confirmed'
    case 3: return 'Approved'
    case 4: return 'Cancelled'
    case 5: return 'Rejected'
    default: return 'Pending'
  }
}

function statusColor(status) {
  const s = normalizeStatus(status)
  if (s === 'Approved') return 'success'
  if (s === 'Confirmed') return 'primary'
  if (s === 'Registered') return 'info'
  if (s === 'Rejected') return 'error'
  if (s === 'Cancelled') return 'grey'
  return 'warning' // Pending
}

function statusLabel(status) {
  const s = normalizeStatus(status)
  if (s === 'Approved') return '已通过'
  if (s === 'Confirmed') return '已确认'
  if (s === 'Registered') return '已报名'
  if (s === 'Rejected') return '已拒绝'
  if (s === 'Cancelled') return '已取消'
  return '待审核'
}

async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    ev.value = await getEvent(eventId)
    // 加载创建者姓名（仅在登录状态下尝试）
    try {
      const uid = ev.value?.createdByUserId || ev.value?.CreatedByUserId
      if (uid && isAuthenticated.value) {
        const u = await getUser(uid)
        const name = u?.fullName || [u?.firstName, u?.lastName].filter(Boolean).join(' ') || u?.email || ''
        creatorName.value = name || ''
      }
    } catch (e) {
      console.warn('加载创建者信息失败', e)
    }
    // 加载冠军战队详情（如已设置）
    try {
      const champId = ev.value?.championTeamId || ev.value?.ChampionTeamId
      if (champId) {
        loadingChampion.value = true
        championTeam.value = await getTeam(champId)
      } else {
        championTeam.value = null
      }
    } catch (e) {
      console.warn('加载冠军战队失败', e)
    } finally {
      loadingChampion.value = false
    }
    // 加载报名队伍
    loadingRegs.value = true
    try {
      registrations.value = await getEventRegistrations(eventId)
    } catch (err2) {
      regsError.value = err2?.payload?.message || err2?.message || '加载报名队伍失败'
    } finally {
      loadingRegs.value = false
    }
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载赛事详情失败'
  } finally {
    loading.value = false
  }
}

onMounted(load)

onMounted(() => {
  // 实时更新时间用于倒计时
  regTimer = setInterval(() => { nowMs.value = Date.now() }, 1000)
})

onUnmounted(() => {
  if (regTimer) { clearInterval(regTimer); regTimer = null }
})

function backToEvents() {
  router.push('/events')
}

async function togglePlayers(teamId) {
  const key = teamId
  const has = !!teamDetails.value[key]
  if (has) {
    // 已加载则移除以折叠
    delete teamDetails.value[key]
    teamDetails.value = { ...teamDetails.value }
    return
  }
  if (loadingTeamIds.value.has(teamId)) return
  loadingTeamIds.value.add(teamId)
  try {
    const team = await getTeam(teamId)
    teamDetails.value[key] = team
    teamDetails.value = { ...teamDetails.value }
  } catch (err) {
    regsError.value = err?.payload?.message || err?.message || '加载队员信息失败'
  } finally {
    loadingTeamIds.value.delete(teamId)
  }
}

// 管理员/创建者：审批报名状态
const updatingStatusIds = ref(new Set())

async function setRegistrationStatus(teamId, status) {
  if (!canManageEvent.value) return
  if (updatingStatusIds.value.has(teamId)) return
  updatingStatusIds.value.add(teamId)
  actionError.value = ''
  successMsg.value = ''
  try {
    const updated = await updateTeamRegistrationStatus(eventId, teamId, { status })
    // 用返回的数据更新本地列表项
    const idx = registrations.value.findIndex(r => (r.teamId || r.TeamId) === teamId)
    if (idx >= 0) {
      registrations.value[idx] = { ...registrations.value[idx], ...updated }
      registrations.value = [...registrations.value]
    } else {
      // 找不到则刷新列表
      registrations.value = await getEventRegistrations(eventId)
    }
    successMsg.value = '报名状态已更新'
    showSuccess.value = true
  } catch (err) {
    actionError.value = err?.payload?.message || err?.message || '更新报名状态失败'
  } finally {
    updatingStatusIds.value.delete(teamId)
  }
}

function approve(teamId) {
  // 3 = Approved
  return setRegistrationStatus(teamId, 3)
}

function reject(teamId) {
  // 5 = Rejected
  return setRegistrationStatus(teamId, 5)
}

async function doExportCsv() {
  try {
    const blob = await exportEventRegistrationsCsv(eventId)
    const url = URL.createObjectURL(blob)
    const nameSafe = (ev.value?.name || 'event').replace(/[^a-zA-Z0-9_\-]/g, '_')
    const a = document.createElement('a')
    a.href = url
    a.download = `${nameSafe}-registrations.csv`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '导出CSV失败'
  }
}

function registrationOpen() {
  if (!ev.value) return false
  try {
    const now = Date.now()
    const start = new Date(ev.value.registrationStartTime).getTime()
    const end = new Date(ev.value.registrationEndTime).getTime()
    return now >= start && now <= end
  } catch { return true }
}

function registrationStatus() {
  if (!ev.value) return 'unknown'
  try {
    const now = nowMs.value
    const start = new Date(ev.value.registrationStartTime).getTime()
    const end = new Date(ev.value.registrationEndTime).getTime()
    if (now < start) return 'not_started'
    if (now > end) return 'ended'
    return 'ongoing'
  } catch { return 'ongoing' }
}

const registrationStatusLabel = computed(() => {
  const s = registrationStatus()
  if (s === 'not_started') return '未开始'
  if (s === 'ongoing') return '进行中'
  if (s === 'ended') return '已结束'
  return '—'
})

const registrationStatusColor = computed(() => {
  const s = registrationStatus()
  if (s === 'not_started') return 'grey'
  // 进行中使用绿色，避免与蓝色背景冲突
  if (s === 'ongoing') return 'success'
  if (s === 'ended') return 'error'
  return 'grey'
})

function formatDuration(ms) {
  if (!Number.isFinite(ms) || ms <= 0) return ''
  const sec = Math.floor(ms / 1000)
  const days = Math.floor(sec / 86400)
  const hours = Math.floor((sec % 86400) / 3600)
  const minutes = Math.floor((sec % 3600) / 60)
  const seconds = sec % 60
  const parts = []
  if (days) parts.push(`${days}天`)
  if (hours) parts.push(`${hours}小时`)
  if (minutes) parts.push(`${minutes}分`)
  parts.push(`${seconds}秒`)
  return parts.join('')
}

const registrationCountdownText = computed(() => {
  if (!ev.value) return ''
  try {
    const now = nowMs.value
    const start = new Date(ev.value.registrationStartTime).getTime()
    const end = new Date(ev.value.registrationEndTime).getTime()
    const s = registrationStatus()
    if (s === 'not_started') {
      const diff = start - now
      const t = formatDuration(diff)
      return t ? `距开始：${t}` : ''
    }
    if (s === 'ongoing') {
      const diff = end - now
      const t = formatDuration(diff)
      return t ? `距结束：${t}` : ''
    }
    return ''
  } catch { return '' }
})

const countdownSegments = computed(() => {
  if (!ev.value) return null
  try {
    const now = nowMs.value
    const start = new Date(ev.value.registrationStartTime).getTime()
    const end = new Date(ev.value.registrationEndTime).getTime()
    let diff = 0
    let type = 'start'
    const s = registrationStatus()
    if (s === 'not_started') { diff = start - now; type = 'start' }
    else if (s === 'ongoing') { diff = end - now; type = 'end' }
    else { return null }
    const sec = Math.max(0, Math.floor(diff / 1000))
    const days = Math.floor(sec / 86400)
    const hours = Math.floor((sec % 86400) / 3600)
    const minutes = Math.floor((sec % 3600) / 60)
    const seconds = sec % 60
    return { days, hours, minutes, seconds, type }
  } catch { return null }
})

async function onRegister() {
  actionError.value = ''
  successMsg.value = ''
  if (!isAuthenticated.value) {
    router.push('/login')
    return
  }
  const me = currentUser.value
  const myTeamId = me?.teamId || me?.TeamId
  if (!myTeamId) {
    actionError.value = '您还没有战队，请先创建战队后再报名'
    router.push('/teams/create')
    return
  }
  if (!registrationOpen()) {
    actionError.value = '当前不在报名时间范围内'
    return
  }
  // 检查队伍是否已有Logo
  try {
    const myTeam = await getTeam(myTeamId)
    const hasLogo = !!(myTeam?.logoUrl || myTeam?.LogoUrl)
    if (!hasLogo) {
      pendingTeamId.value = myTeamId
      showTeamLogoDialog.value = true
      return
    }
  } catch (e) {
    // 获取队伍失败也允许继续报名，但提示
    console.warn('获取队伍信息失败，跳过Logo检查', e)
  }
  registering.value = true
  try {
    await registerTeamToEvent(eventId, { teamId: myTeamId })
    successMsg.value = '报名提交成功，请等待审核'
    showSuccess.value = true
    // 刷新报名列表
    registrations.value = await getEventRegistrations(eventId)
  } catch (err) {
    actionError.value = err?.payload?.message || err?.message || '报名失败'
  } finally {
    registering.value = false
  }
}

// 已批准的报名列表（仅可设置为已批准的队伍）
const approvedRegistrations = computed(() => {
  const list = registrations.value || []
  return list.filter(r => normalizeStatus(r.status || r.Status) === 'Approved')
})

function openChampionDialog() {
  championError.value = ''
  const currentId = ev.value?.championTeamId || ev.value?.ChampionTeamId || null
  selectedChampionTeamId.value = currentId
  championDialog.value = true
}

async function onConfirmSetChampion() {
  if (!canManageEvent.value) return
  settingChampion.value = true
  championError.value = ''
  try {
    const teamId = selectedChampionTeamId.value || null
    const updatedEvent = await setEventChampion(eventId, teamId)
    ev.value = { ...ev.value, ...updatedEvent }
    // 刷新冠军详情
    const champId = ev.value?.championTeamId || ev.value?.ChampionTeamId
    if (champId) {
      championTeam.value = await getTeam(champId)
    } else {
      championTeam.value = null
    }
    championDialog.value = false
    successMsg.value = teamId ? '冠军设置成功' : '已清除冠军'
    showSuccess.value = true
  } catch (err) {
    championError.value = err?.payload?.message || err?.message || '设置冠军失败'
  } finally {
    settingChampion.value = false
  }
}

async function copyShareLink() {
  try {
    await navigator.clipboard.writeText(shareLink.value)
    successMsg.value = '分享链接已复制'
    showSuccess.value = true
  } catch {
    try {
      const input = document.createElement('input')
      input.value = shareLink.value
      document.body.appendChild(input)
      input.select()
      document.execCommand('copy')
      document.body.removeChild(input)
      successMsg.value = '分享链接已复制'
      showSuccess.value = true
    } catch (e) {
      actionError.value = '复制失败，请手动复制链接'
    }
  }
}

async function onEventLogoSelected(files) {
  uploadLogoError.value = ''
  if (!canManageEvent.value) return
  const file = Array.isArray(files) ? files[0] : files
  if (!file) return
  uploadingLogo.value = true
  try {
    const res = await uploadEventLogo(eventId, file)
    const url = res?.logoUrl || res?.LogoUrl
    if (url) {
      ev.value.logoUrl = url
      ev.value.LogoUrl = url
      successMsg.value = '赛事Logo已更新'
      showSuccess.value = true
    } else {
      uploadLogoError.value = '上传成功，但未返回Logo地址'
    }
  } catch (err) {
    uploadLogoError.value = err?.payload?.message || err?.message || '上传赛事Logo失败'
  } finally {
    uploadingLogo.value = false
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
  if (!pendingTeamId.value) { showTeamLogoDialog.value = false; return }
  if (!teamLogoFile.value) { teamLogoError.value = '请先选择队伍Logo'; return }
  teamLogoUploading.value = true
  try {
    await uploadTeamLogo(pendingTeamId.value, teamLogoFile.value)
    showTeamLogoDialog.value = false
    // 上传成功后继续报名
    registering.value = true
    await registerTeamToEvent(eventId, { teamId: pendingTeamId.value })
    successMsg.value = '已上传队伍Logo并报名成功'
    showSuccess.value = true
    registrations.value = await getEventRegistrations(eventId)
  } catch (err) {
    teamLogoError.value = err?.payload?.message || err?.message || '上传队伍Logo失败'
  } finally {
    teamLogoUploading.value = false
    registering.value = false
  }
}
</script>

<template>
  <PageHero :title="heroTitle" subtitle="查看赛事信息与报名队伍" icon="emoji_events">
    <template #actions>
      <v-btn variant="text" prepend-icon="chevron_left" class="mr-3 mb-3" @click="backToEvents">返回赛事列表</v-btn>
      <v-btn
        v-if="ev"
        variant="text"
        class="mr-3 mb-3"
        :to="'/events/' + (ev.id || ev.Id) + '/schedule'"
        prepend-icon="calendar_month"
      >赛程</v-btn>
      <v-chip :color="registrationStatusColor" class="mb-3 mr-2" size="small">报名状态：{{ registrationStatusLabel }}</v-chip>
      <template v-if="registrationOpen()">
        <v-btn
          color="success"
          prepend-icon="check_circle"
          :loading="registering"
          class="mb-3 px-6 py-3 text-h6 font-weight-bold"
          size="large"
          elevation="3"
          rounded="lg"
          @click="onRegister"
        >
          报名参赛
        </v-btn>
      </template>
    </template>
  </PageHero>
  <v-container class="py-8" style="max-width: 1200px">
    <v-btn variant="text" prepend-icon="chevron_left" class="mb-4" @click="backToEvents">返回赛事列表</v-btn>
    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
    <v-progress-linear v-if="loading" indeterminate color="primary" />

    <template v-if="ev">
      <v-card>
        <v-card-title class="d-flex align-center">
          <v-avatar size="56" class="mr-3">
            <v-img v-if="ev.logoUrl || ev.LogoUrl" :src="ev.logoUrl || ev.LogoUrl" alt="event logo" cover />
            <v-icon v-else icon="emoji_events" size="40" />
          </v-avatar>
          <span class="text-h6 text-truncate">{{ ev.name }}</span>
        </v-card-title>
        <v-card-subtitle>
          报名：{{ new Date(ev.registrationStartTime).toLocaleString() }}
          — {{ new Date(ev.registrationEndTime).toLocaleString() }}
        </v-card-subtitle>
        <v-card-text>
          <div class="d-flex align-center text-h6 font-weight-bold mb-2">
            <v-icon icon="workspace_premium" size="large" color="primary" class="mr-2" />
            创建者：{{ creatorDisplay }}
          </div>
          <!-- 倒计时（简洁版） -->
          <v-card v-if="registrationStatus() !== 'ended' && countdownSegments" class="pa-4 mb-4 countdown-simple-card" rounded>
            <div class="countdown-simple">
              <template v-if="countdownSegments.days > 0">
                <span class="countdown-num">{{ countdownSegments.days }}</span>
                <span class="countdown-sep">:</span>
              </template>
              <span class="countdown-num">{{ countdownSegments.hours.toString().padStart(2, '0') }}</span>
              <span class="countdown-sep">:</span>
              <span class="countdown-num">{{ countdownSegments.minutes.toString().padStart(2, '0') }}</span>
              <span class="countdown-sep">:</span>
              <span class="countdown-num countdown-sec">{{ countdownSegments.seconds.toString().padStart(2, '0') }}</span>
            </div>
          </v-card>
          <div class="mb-3">比赛：{{ new Date(ev.competitionStartTime).toLocaleString() }}
            <span v-if="ev.competitionEndTime"> — {{ new Date(ev.competitionEndTime).toLocaleString() }}</span>
          </div>
          <div class="mb-3" v-if="ev.maxTeams">最大队伍数：{{ ev.maxTeams }}</div>
            <div class="text-body-2 md-content" v-if="eventDescription" v-html="eventDescriptionHtml"></div>
          <div class="mt-3">
            <v-btn v-if="canManageEvent" color="primary" prepend-icon="download" class="mr-3" @click="doExportCsv">
              导出报名CSV
            </v-btn>
            <v-btn v-if="canManageEvent" variant="tonal" prepend-icon="share" class="mr-3" @click="shareDialog = true">
              分享赛事
            </v-btn>
          </div>
          <div v-if="canManageEvent" class="mt-4">
            <div class="text-caption mb-2">上传赛事Logo（png/jpg/webp，≤5MB）：</div>
            <v-file-input
              prepend-icon="add_photo_alternate"
              density="comfortable"
              accept="image/png, image/jpeg, image/webp"
              show-size
              :disabled="uploadingLogo"
              :loading="uploadingLogo"
              @update:modelValue="onEventLogoSelected"
              label="选择图片文件"
            />
            <v-alert v-if="uploadLogoError" type="error" :text="uploadLogoError" class="mt-2" />
          </div>
          <v-alert v-if="actionError" type="error" :text="actionError" class="mt-3" />
        </v-card-text>
      </v-card>

      <!-- 冠军战队板块 -->
      <v-card class="mt-6">
        <v-card-title class="d-flex align-center">
          <v-icon icon="workspace_premium" color="amber" class="mr-2" />
          <span class="text-subtitle-1">冠军战队</span>
          <v-spacer />
          <v-btn v-if="canManageEvent" size="small" color="primary" prepend-icon="workspace_premium" @click="openChampionDialog">设置冠军</v-btn>
        </v-card-title>
        <v-card-text>
          <v-progress-linear v-if="loadingChampion" indeterminate color="primary" />
          <template v-else>
            <template v-if="championTeam">
              <div class="d-flex align-center mb-3">
                <v-avatar size="48" class="mr-3">
                  <v-img v-if="championTeam.logoUrl || championTeam.LogoUrl" :src="championTeam.logoUrl || championTeam.LogoUrl" alt="champion logo" cover />
                  <v-icon v-else icon="group" size="32" />
                </v-avatar>
                <div>
                  <div class="text-subtitle-1">{{ championTeam.name || championTeam.Name }}</div>
                  <div class="text-caption text-medium-emphasis">恭喜夺冠！</div>
                </div>
              </div>
              <v-row>
                <v-col cols="12" md="6">
                  <div class="text-subtitle-2 mb-2">战队简介</div>
                  <div v-if="championDescription" class="text-body-2 md-content" v-html="championDescriptionHtml"></div>
                  <div v-else class="text-body-2 text-medium-emphasis">暂无简介</div>
                </v-col>
                <v-col cols="12" md="6">
                  <div class="text-subtitle-2 mb-2">队员名单</div>
                  <template v-if="championPlayers.length">
                    <v-list density="comfortable">
                      <v-list-item v-for="p in championPlayers" :key="p.id || p.Id">
                        <template #prepend>
                          <v-icon icon="person" class="mr-2" />
                        </template>
                        <v-list-item-title>{{ p.name || p.Name }}</v-list-item-title>
                        <v-list-item-subtitle>
                          <span v-if="p.gameId || p.GameId">ID：{{ p.gameId || p.GameId }}</span>
                          <span v-if="p.gameRank || p.GameRank" class="ml-2">段位：{{ p.gameRank || p.GameRank }}</span>
                        </v-list-item-subtitle>
                        <div v-if="p.description || p.Description" class="text-caption mt-1">{{ p.description || p.Description }}</div>
                      </v-list-item>
                    </v-list>
                  </template>
                  <div v-else class="text-body-2 text-medium-emphasis">暂无队员数据</div>
                </v-col>
              </v-row>
            </template>
            <div v-else class="text-body-2 text-medium-emphasis">尚未设置冠军</div>
          </template>
        </v-card-text>
      </v-card>

      <v-card class="mt-6">
        <v-card-title>已报名的队伍</v-card-title>
        <v-card-text>
          <v-alert v-if="regsError" type="error" :text="regsError" class="mb-4" />
          <v-progress-linear v-if="loadingRegs" indeterminate color="primary" />

          <template v-else>
            <div v-if="!registrations || registrations.length === 0" class="text-body-2">暂无报名队伍</div>
            <v-row v-else>
              <v-col v-for="r in registrations" :key="r.teamId" cols="12" sm="6" md="4" lg="3">
                <v-card>
                  <v-card-title class="d-flex align-center">
                    <v-avatar size="28" class="mr-2">
                      <v-img v-if="teamDetails[r.teamId]?.logoUrl || teamDetails[r.teamId]?.LogoUrl" :src="teamDetails[r.teamId]?.logoUrl || teamDetails[r.teamId]?.LogoUrl" alt="team logo" cover />
                      <v-icon v-else icon="group" size="22" />
                    </v-avatar>
                    <span class="text-truncate">{{ r.teamName }}</span>
                    <v-spacer />
                    <v-chip :color="statusColor(r.status)" size="small">{{ statusLabel(r.status) }}</v-chip>
                  </v-card-title>
                  <v-card-subtitle>报名时间：{{ new Date(r.registrationTime).toLocaleString() }}</v-card-subtitle>
                  <v-card-text>
                    <!-- 已移除备注显示 -->
                    <div class="mt-2">
                      <v-btn size="small" variant="text" prepend-icon="group" :loading="loadingTeamIds.has(r.teamId)" @click="togglePlayers(r.teamId)">
                        {{ teamDetails[r.teamId] ? '收起队员' : '查看队员' }}
                      </v-btn>
                      <template v-if="canManageEvent">
                        <v-btn size="small" color="success" class="ml-2" prepend-icon="check_circle" :loading="updatingStatusIds.has(r.teamId)" :disabled="normalizeStatus(r.status) === 'Approved'" @click="approve(r.teamId)">
                          通过
                        </v-btn>
                        <v-btn size="small" color="error" class="ml-1" prepend-icon="cancel" :loading="updatingStatusIds.has(r.teamId)" :disabled="normalizeStatus(r.status) === 'Rejected'" @click="reject(r.teamId)">
                          拒绝
                        </v-btn>
                      </template>
                    </div>
                    <div v-if="teamDetails[r.teamId]" class="mt-2">
                      <v-divider class="mb-2" />
                      <div class="text-caption mb-2">队员列表</div>
                      <v-list density="compact">
                        <v-list-item v-for="p in (teamDetails[r.teamId].players || teamDetails[r.teamId].Players || [])" :key="p.id || p.name">
                          <v-list-item-title>{{ p.name }}</v-list-item-title>
                          <v-list-item-subtitle>
                            GameID: {{ p.gameId || p.GameId || '—' }} | Rank: {{ p.gameRank || p.GameRank || '—' }}
                          </v-list-item-subtitle>
                          <div class="text-caption" v-if="p.description">{{ p.description }}</div>
                        </v-list-item>
                      </v-list>
                    </div>
                  </v-card-text>
                </v-card>
              </v-col>
            </v-row>
          </template>
        </v-card-text>
      </v-card>
      <v-dialog v-model="shareDialog" max-width="520">
        <v-card>
          <v-card-title>分享赛事</v-card-title>
          <v-card-text>
            <div class="text-body-2 mb-2">打开此链接将直接进入赛事详情页，参赛者可在报名期间直接报名：</div>
            <v-sheet class="pa-3" color="grey-lighten-4" rounded>
              <div class="text-caption" style="word-break: break-all;">{{ shareLink }}</div>
            </v-sheet>
            <div class="d-flex mt-3">
              <v-btn color="primary" prepend-icon="content_copy" class="mr-3" @click="copyShareLink">复制链接</v-btn>
              <v-spacer />
            </div>
            <div class="mt-4 text-caption">快捷分享到社交平台：</div>
            <div class="d-flex mt-2">
              <v-btn variant="text" prepend-icon="share" :href="`https://service.weibo.com/share/share.php?title=${encodeURIComponent(ev.name)}&url=${encodeURIComponent(shareLink)}`" target="_blank">微博</v-btn>
              <v-btn variant="text" prepend-icon="chat" :href="`https://connect.qq.com/widget/shareqq/index.html?title=${encodeURIComponent(ev.name)}&url=${encodeURIComponent(shareLink)}`" target="_blank">QQ</v-btn>
              <v-btn variant="text" prepend-icon="public" :href="`https://twitter.com/intent/tweet?text=${encodeURIComponent(ev.name)}&url=${encodeURIComponent(shareLink)}`" target="_blank">Twitter</v-btn>
            </div>
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="shareDialog = false">关闭</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

      <!-- 设置冠军弹窗 -->
      <v-dialog v-model="championDialog" max-width="560">
        <v-card>
          <v-card-title>设置赛事冠军</v-card-title>
          <v-card-text>
            <v-alert v-if="championError" type="error" :text="championError" class="mb-3" />
            <div class="text-caption mb-2">仅可选择“已批准参赛”的战队作为冠军。</div>
            <v-list density="comfortable" v-if="approvedRegistrations.length">
              <v-list-item
                v-for="r in approvedRegistrations"
                :key="r.teamId"
                @click="selectedChampionTeamId = r.teamId"
                :active="selectedChampionTeamId === r.teamId"
              >
                <template #prepend>
                  <v-avatar size="28" class="mr-2">
                    <v-img v-if="teamDetails[r.teamId]?.logoUrl || teamDetails[r.teamId]?.LogoUrl" :src="teamDetails[r.teamId]?.logoUrl || teamDetails[r.teamId]?.LogoUrl" cover />
                    <v-icon v-else icon="group" size="20" />
                  </v-avatar>
                </template>
                <v-list-item-title>{{ r.teamName }}</v-list-item-title>
                <template #append>
                  <v-radio-group v-model="selectedChampionTeamId">
                    <v-radio :value="r.teamId" />
                  </v-radio-group>
                </template>
              </v-list-item>
            </v-list>
            <div v-else class="text-body-2 text-medium-emphasis">暂无已批准的队伍可设置为冠军。</div>
            <v-divider class="my-3" />
            <v-checkbox
              v-model="selectedChampionTeamId"
              :true-value="null"
              :false-value="selectedChampionTeamId"
              label="清除冠军（不设置任何战队）"
            />
          </v-card-text>
          <v-card-actions>
            <v-btn variant="text" @click="championDialog = false">取消</v-btn>
            <v-spacer />
            <v-btn :loading="settingChampion" color="primary" prepend-icon="save" @click="onConfirmSetChampion">确认</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

      <!-- 备注弹窗已移除 -->

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

      <v-snackbar v-model="showSuccess" color="success" :timeout="2500" location="bottom right">
        <div class="d-flex align-center">
          <v-icon class="mr-2" icon="check_circle" />
          {{ successMsg || '操作成功' }}
        </div>
      </v-snackbar>
    </template>
  </v-container>
</template>

<style scoped>
/* 倒计时（简洁版） */
.countdown-simple-card {
  background: linear-gradient(135deg, #4b6cb7 0%, #182848 100%);
  color: #fff;
  border: none;
}
.countdown-simple {
  display: flex;
  align-items: baseline;
  justify-content: center;
  flex-wrap: nowrap;
  gap: 8px;
  text-align: center;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, 'Liberation Mono', 'Courier New', monospace;
}
.countdown-num {
  font-weight: 800;
  font-size: clamp(24px, 8vw, 40px);
  line-height: 1;
  color: #ffffff;
}
.countdown-sec {
  color: rgba(244, 67, 54, 0.9); /* 淡红色（Material Red基调）*/
}
.countdown-sep {
  font-weight: 700;
  font-size: clamp(20px, 7vw, 36px);
  color: rgba(255,255,255,0.85);
}
</style>
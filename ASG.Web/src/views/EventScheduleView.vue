<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import PageHero from '../components/PageHero.vue'
import { getEvent, getEventRegistrations } from '../services/events'
import { getMatches, createMatch, deleteMatch, updateMatch } from '../services/matches'
import { currentUser, isAuthenticated } from '../stores/auth'
import { getTeam } from '../services/teams'

const route = useRoute()
const router = useRouter()
const eventId = computed(() => route.params.id)

const ev = ref(null)
const registrations = ref([])
const matches = ref([])
const loadingEvent = ref(false)
const loadingTeams = ref(false)
const loadingMatches = ref(false)
const errorMsg = ref('')
const successMsg = ref('')

const page = ref(1)
const pageSize = ref(20)

const heroTitle = computed(() => `赛事赛程`)

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

// 赛事创建者或管理员可管理赛程（创建/编辑/删除），与后端服务层一致
const canManageMatches = computed(() => {
  return canManageEvent.value
})

// 队伍详情缓存（用于显示头像/Logo）
const teamDetails = ref({}) // teamId -> TeamDto
const loadingTeamIds = ref(new Set())

function getAvatarLetter(name) {
  if (!name || typeof name !== 'string') return '?'
  return name.trim().charAt(0).toUpperCase()
}

// 分组预览与动画
const previewOpen = ref(false)
const previewItems = ref([]) // { homeId, awayId, matchTime, homeName, awayName }
const previewVisibleCount = ref(0)
const previewAnimating = ref(false)
const previewCreating = ref(false)

function buildPairs(teamIds) {
  const shuffled = shuffle(teamIds)
  const pairs = []
  for (let i = 0; i < shuffled.length; i += 2) {
    const a = shuffled[i]
    const b = shuffled[i + 1]
    if (b) pairs.push([a, b])
  }
  return pairs
}

function computeScheduleTimes(pairs) {
  const intervalMs = (generateIntervalMinutes.value || 60) * 60 * 1000
  let startBase
  if (generateStartDate.value && dailyStartTime.value) {
    startBase = new Date(`${generateStartDate.value}T${dailyStartTime.value}`)
  } else {
    const fallbackIso = toIso(generateStartTime.value || (ev.value?.competitionStartTime || ev.value?.CompetitionStartTime))
    startBase = new Date(fallbackIso)
  }
  const perDay = maxMatchesPerDay.value ? Number(maxMatchesPerDay.value) : null
  return pairs.map((pair, idx) => {
    let scheduled
    if (perDay && perDay > 0) {
      const dayIndex = Math.floor(idx / perDay)
      const withinDay = idx % perDay
      scheduled = new Date(startBase.getTime() + dayIndex * 24 * 60 * 60 * 1000 + withinDay * intervalMs)
    } else {
      scheduled = new Date(startBase.getTime() + idx * intervalMs)
    }
    return scheduled.toISOString()
  })
}

async function onPreviewGenerate() {
  errorMsg.value = ''
  if (!canManageMatches.value) { errorMsg.value = '没有权限生成赛程'; return }
  const baseTeams = (registrations.value || []).filter(t => {
    const s = normalizeStatus(t.status)
    if (s === 'Approved' || s === 'Confirmed') return true
    if (includeRegistered.value && s === 'Registered') return true
    return false
  })
  const teamIds = baseTeams.map(t => t.teamId || t.TeamId)
  if (teamIds.length < 2) { errorMsg.value = '可用于随机分组的队伍不足'; return }
  const pairs = buildPairs(teamIds)
  const times = computeScheduleTimes(pairs)
  const nameMap = teamNameById.value || {}
  previewItems.value = pairs.map(([homeId, awayId], i) => ({
    homeId, awayId,
    matchTime: times[i],
    homeName: nameMap[homeId] || `#${homeId}`,
    awayName: nameMap[awayId] || `#${awayId}`,
  }))
  // 预加载头像
  await Promise.all(previewItems.value.flatMap(item => [ensureTeamDetail(item.homeId), ensureTeamDetail(item.awayId)]))
  previewVisibleCount.value = 0
  previewAnimating.value = true
  previewOpen.value = true
  // 逐项动画（每350ms展示一个）
  for (let i = 1; i <= previewItems.value.length; i++) {
    await new Promise(res => setTimeout(res, 350))
    previewVisibleCount.value = i
  }
  previewAnimating.value = false
}

async function onConfirmGenerateFromPreview() {
  previewCreating.value = true
  try {
    const eventGuid = ev.value?.id || ev.value?.Id
    for (const item of previewItems.value) {
      const payload = {
        homeTeamId: item.homeId,
        awayTeamId: item.awayId,
        matchTime: item.matchTime,
        eventId: eventGuid,
        customData: '{}',
      }
      await createMatch(payload)
    }
    successMsg.value = `已生成 ${previewItems.value.length} 场赛程`
    previewOpen.value = false
    await loadMatches()
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '生成赛程失败'
  } finally {
    previewCreating.value = false
  }
}
async function ensureTeamDetail(id) {
  if (!id) return
  const key = id
  if (teamDetails.value[key]) return
  if (loadingTeamIds.value.has(key)) return
  loadingTeamIds.value.add(key)
  try {
    const dto = await getTeam(key)
    teamDetails.value[key] = dto
  } catch (e) {
    console.warn('加载战队详情失败', key, e)
  } finally {
    loadingTeamIds.value.delete(key)
  }
}

async function loadTeamDetailsForMatches() {
  const ids = new Set()
  for (const m of matches.value || []) {
    ids.add(m.homeTeamId || m.HomeTeamId)
    ids.add(m.awayTeamId || m.AwayTeamId)
  }
  await Promise.all(Array.from(ids).map(id => ensureTeamDetail(id)))
}

const teamNameById = computed(() => {
  const map = {}
  for (const r of registrations.value || []) {
    const id = r.teamId || r.TeamId
    const name = r.teamName || r.TeamName
    if (id) map[id] = name
  }
  return map
})

// 时间轴分组（按日期）
const matchesByDay = computed(() => {
  const groups = {}
  for (const m of matches.value || []) {
    const dt = new Date(m.matchTime || m.MatchTime)
    const y = dt.getFullYear()
    const mm = String(dt.getMonth() + 1).padStart(2, '0')
    const dd = String(dt.getDate()).padStart(2, '0')
    const key = `${y}-${mm}-${dd}`
    ;(groups[key] ||= []).push(m)
  }
  const dates = Object.keys(groups).sort()
  return dates.map(d => ({
    date: d,
    items: groups[d].sort((a, b) => new Date(a.matchTime || a.MatchTime) - new Date(b.matchTime || b.MatchTime))
  }))
})

function formatDateLabel(dateStr) {
  try {
    const [y, m, d] = dateStr.split('-').map(Number)
    const dt = new Date(y, m - 1, d)
    return dt.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
  } catch { return dateStr }
}

function formatTimeLabel(m) {
  const dt = new Date(m.matchTime || m.MatchTime)
  return dt.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

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

const eligibleTeams = computed(() => {
  const teams = registrations.value || []
  return teams.filter(t => {
    const s = normalizeStatus(t.status)
    return s === 'Approved' || s === 'Confirmed'
  })
})

// 手动创建赛程表单
const homeTeamId = ref(null)
const awayTeamId = ref(null)
const matchTime = ref('')
const liveLink = ref('')
const commentator = ref('')
const director = ref('')
const referee = ref('')
const creating = ref(false)

function toIso(dt) {
  return dt ? new Date(dt).toISOString() : null
}

function toLocalDateTimeInput(value) {
  if (!value) return ''
  const d = new Date(value)
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

async function onCreateMatch() {
  errorMsg.value = ''
  successMsg.value = ''
  if (!canManageMatches.value) { errorMsg.value = '没有权限创建赛程'; return }
  if (!homeTeamId.value || !awayTeamId.value) { errorMsg.value = '请选择主客队'; return }
  if (homeTeamId.value === awayTeamId.value) { errorMsg.value = '主客队不能相同'; return }
  const mt = toIso(matchTime.value || (ev.value?.competitionStartTime || ev.value?.CompetitionStartTime))
  if (!mt) { errorMsg.value = '请设置比赛时间'; return }
  const payload = {
    homeTeamId: homeTeamId.value,
    awayTeamId: awayTeamId.value,
    matchTime: mt,
    eventId: ev.value?.id || ev.value?.Id,
    liveLink: liveLink.value?.trim() || null,
    customData: '{}',
    commentator: commentator.value?.trim() || null,
    director: director.value?.trim() || null,
    referee: referee.value?.trim() || null,
  }
  creating.value = true
  try {
    await createMatch(payload)
    successMsg.value = '赛程创建成功'
    // 重置表单并刷新列表
    homeTeamId.value = null
    awayTeamId.value = null
    matchTime.value = ''
    liveLink.value = ''
    commentator.value = ''
    director.value = ''
    referee.value = ''
    await loadMatches()
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '创建赛程失败'
  } finally {
    creating.value = false
  }
}

// 随机生成赛程
const generateOpen = ref(false)
const generateIntervalMinutes = ref(60)
const generateStartTime = ref('')
const generating = ref(false)
const includeRegistered = ref(false) // 是否包含已报名但未审核

// 跨天与每日起始/上限设置
const generateStartDate = ref('') // 开始日期（用于跨天排程）
const dailyStartTime = ref('') // 每天起始时间（HH:mm）
const maxMatchesPerDay = ref(null) // 每天最多比赛场次

function shuffle(arr) {
  const a = [...arr]
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    ;[a[i], a[j]] = [a[j], a[i]]
  }
  return a
}

async function onGenerateRandom() {
  errorMsg.value = ''
  successMsg.value = ''
  if (!canManageMatches.value) { errorMsg.value = '没有权限生成赛程'; return }
  const baseTeams = (registrations.value || []).filter(t => {
    const s = normalizeStatus(t.status)
    if (s === 'Approved' || s === 'Confirmed') return true
    if (includeRegistered.value && s === 'Registered') return true
    return false
  })
  const teamIds = baseTeams.map(t => t.teamId || t.TeamId)
  if (teamIds.length < 2) { errorMsg.value = '可用于随机分组的队伍不足'; return }
  // 计算起始时间（支持开始日期 + 每日起始时间）
  let startBase
  if (generateStartDate.value && dailyStartTime.value) {
    startBase = new Date(`${generateStartDate.value}T${dailyStartTime.value}`)
  } else {
    const fallbackIso = toIso(generateStartTime.value || (ev.value?.competitionStartTime || ev.value?.CompetitionStartTime))
    if (!fallbackIso) { errorMsg.value = '请设置开始时间或每日起始时间'; return }
    startBase = new Date(fallbackIso)
  }

  const shuffled = shuffle(teamIds)
  const pairs = []
  for (let i = 0; i < shuffled.length; i += 2) {
    const a = shuffled[i]
    const b = shuffled[i + 1]
    if (b) pairs.push([a, b])
    // 如果是奇数，最后一个临时轮空（不生成赛程）
  }

  generating.value = true
  try {
    const eventGuid = ev.value?.id || ev.value?.Id
    const intervalMs = (generateIntervalMinutes.value || 60) * 60 * 1000
    const perDay = maxMatchesPerDay.value ? Number(maxMatchesPerDay.value) : null
    for (let idx = 0; idx < pairs.length; idx++) {
      const [homeId, awayId] = pairs[idx]
      let scheduled
      if (perDay && perDay > 0) {
        const dayIndex = Math.floor(idx / perDay)
        const withinDay = idx % perDay
        scheduled = new Date(startBase.getTime() + dayIndex * 24 * 60 * 60 * 1000 + withinDay * intervalMs)
      } else {
        scheduled = new Date(startBase.getTime() + idx * intervalMs)
      }
      const mt = scheduled.toISOString()
      const payload = {
        homeTeamId: homeId,
        awayTeamId: awayId,
        matchTime: mt,
        eventId: eventGuid,
        customData: '{}',
      }
      await createMatch(payload)
    }
    successMsg.value = `已生成 ${pairs.length} 场赛程`
    await loadMatches()
    generateOpen.value = false
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '随机生成失败'
  } finally {
    generating.value = false
  }
}

async function loadEvent() {
  loadingEvent.value = true
  try {
    ev.value = await getEvent(eventId.value)
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '获取赛事信息失败'
  } finally {
    loadingEvent.value = false
  }
}

async function loadRegistrations() {
  loadingTeams.value = true
  try {
    registrations.value = await getEventRegistrations(eventId.value)
  } catch (e) {
    // 不阻断页面
    console.warn('获取报名战队失败', e)
  } finally {
    loadingTeams.value = false
  }
}

async function loadMatches() {
  loadingMatches.value = true
  try {
    matches.value = await getMatches({ eventId: eventId.value, page: page.value, pageSize: pageSize.value })
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '获取赛程失败'
  } finally {
    loadingMatches.value = false
  }
  await loadTeamDetailsForMatches()
}

async function onDeleteMatch(id) {
  if (!canManageMatches.value) return
  try {
    await deleteMatch(id)
    await loadMatches()
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '删除赛程失败'
  }
}

// 编辑赛程弹窗与逻辑
const editOpen = ref(false)
const editTarget = ref(null)
const editMatchTime = ref('')
const editLiveLink = ref('')
const editCommentator = ref('')
const editDirector = ref('')
const editReferee = ref('')
const editing = ref(false)

function getId(m) { return m?.id || m?.Id }

function openEdit(m) {
  editTarget.value = m
  editMatchTime.value = toLocalDateTimeInput(m.matchTime || m.MatchTime)
  editLiveLink.value = m.liveLink || m.LiveLink || ''
  editCommentator.value = m.commentator || m.Commentator || ''
  editDirector.value = m.director || m.Director || ''
  editReferee.value = m.referee || m.Referee || ''
  editOpen.value = true
}

async function onSaveEdit() {
  errorMsg.value = ''
  successMsg.value = ''
  if (!canManageMatches.value) { errorMsg.value = '没有权限编辑赛程'; return }
  const id = getId(editTarget.value)
  if (!id) { errorMsg.value = '无效的赛程'; return }
  const payload = {
    matchTime: editMatchTime.value ? toIso(editMatchTime.value) : null,
    liveLink: editLiveLink.value?.trim() || null,
    commentator: editCommentator.value?.trim() || null,
    director: editDirector.value?.trim() || null,
    referee: editReferee.value?.trim() || null,
  }
  editing.value = true
  try {
    await updateMatch(id, payload)
    successMsg.value = '赛程更新成功'
    editOpen.value = false
    await loadMatches()
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '更新赛程失败'
  } finally {
    editing.value = false
  }
}

// 批量删除选择模式
const selectionMode = ref(false)
const selectedIds = ref([])
const batchDeleting = ref(false)

function isSelected(m) {
  const id = getId(m)
  return selectedIds.value.includes(id)
}

function toggleSelect(m) {
  const id = getId(m)
  const idx = selectedIds.value.indexOf(id)
  if (idx >= 0) selectedIds.value.splice(idx, 1)
  else selectedIds.value.push(id)
}

function selectAll() {
  selectedIds.value = (matches.value || []).map(getId)
}

async function onBatchDelete() {
  if (!canManageMatches.value) return
  const count = selectedIds.value.length
  if (!count) return
  try {
    batchDeleting.value = true
    await Promise.all(selectedIds.value.map(id => deleteMatch(id)))
    successMsg.value = `已删除 ${count} 场赛程`
    selectedIds.value = []
    selectionMode.value = false
    await loadMatches()
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '批量删除失败'
  } finally {
    batchDeleting.value = false
  }
}

function goBackDetail() {
  router.push(`/events/${eventId.value}`)
}

onMounted(async () => {
  await loadEvent()
  await loadRegistrations()
  await loadMatches()
  // 默认生成开始时间为赛事比赛开始时间
  const compStart = ev.value?.competitionStartTime || ev.value?.CompetitionStartTime
  if (compStart) {
    const d = new Date(compStart)
    // 将ISO转为本地datetime-local兼容字符串
    const pad = n => String(n).padStart(2, '0')
    const s = `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
    generateStartTime.value = s
    matchTime.value = s
  }
})
</script>

<template>
  <PageHero :title="heroTitle" subtitle="查看与管理赛事赛程" icon="calendar_month">
    <template #actions>
      <v-btn class="mb-3" variant="text" @click="goBackDetail">
        <template #prepend>
          <span class="material-symbols-outlined">chevron_left</span>
        </template>
        返回赛事详情
      </v-btn>
      <v-btn v-if="canManageMatches" class="mb-3 ml-2" variant="outlined" prepend-icon="shuffle" @click="generateOpen = true">随机分组生成</v-btn>
    </template>
  </PageHero>

  <v-container class="py-6">
    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
    <v-alert v-if="successMsg" type="success" :text="successMsg" class="mb-4" />

    <v-row class="mb-6" dense>
      <v-col cols="12">
        <v-card rounded="lg" elevation="1">
          <v-card-title class="d-flex align-center">
            <v-icon class="mr-2" color="primary" icon="timeline" />
            <span>赛程时间轴</span>
            <v-spacer />
            <v-chip v-if="loadingMatches" label color="default" variant="tonal">加载中…</v-chip>
            <v-chip v-else label color="default" variant="tonal">共 {{ matches.length }} 场</v-chip>
            <v-btn v-if="canManageMatches" class="ml-2" variant="text" prepend-icon="select_all" @click="selectionMode = !selectionMode">{{ selectionMode ? '取消批量删除' : '批量删除' }}</v-btn>
            <v-btn v-if="selectionMode && canManageMatches" class="ml-1" variant="text" @click="selectAll">全选</v-btn>
            <v-btn v-if="selectionMode && canManageMatches" class="ml-1" color="error" variant="tonal" :disabled="!selectedIds.length" :loading="batchDeleting" @click="onBatchDelete">删除已选</v-btn>
          </v-card-title>
          <v-card-text>
            <v-row dense v-if="loadingMatches">
              <v-col v-for="n in 6" :key="n" cols="12" sm="6" md="4" lg="3"><v-skeleton-loader type="card" /></v-col>
            </v-row>
            <template v-else-if="matches.length">
              <div class="timeline">
                <div v-for="group in matchesByDay" :key="group.date" class="timeline-day">
                  <v-sheet class="day-header" color="primary" variant="tonal">
                    <v-icon class="mr-2" icon="calendar_month" />
                    <span class="day-title">{{ formatDateLabel(group.date) }}</span>
                  </v-sheet>
                  <div class="timeline-track">
                    <div v-for="m in group.items" :key="m.id || m.Id" class="timeline-entry">
                      <div class="entry-dot" />
                      <v-card class="entry-card" rounded="xl" elevation="2" variant="elevated">
                        <div class="entry-top d-flex align-center">
                          <v-checkbox v-if="selectionMode" :model-value="isSelected(m)" @update:modelValue="toggleSelect(m)" density="compact" class="mr-2" />
                          <span class="time-pill">
                            <v-icon size="18" class="mr-1" icon="schedule" />
                            {{ formatTimeLabel(m) }}
                          </span>
                          <v-spacer />
                          <v-chip v-if="m.liveLink || m.LiveLink" size="small" color="secondary" variant="tonal" prepend-icon="videocam">直播</v-chip>
                        </div>
                        <div class="entry-main d-flex align-center">
                          <v-avatar size="40" class="mr-3" color="primary" variant="tonal">
                            <template v-if="teamDetails[m.homeTeamId || m.HomeTeamId]?.logoUrl || teamDetails[m.homeTeamId || m.HomeTeamId]?.LogoUrl">
                              <v-img :src="teamDetails[m.homeTeamId || m.HomeTeamId].logoUrl || teamDetails[m.homeTeamId || m.HomeTeamId].LogoUrl" cover />
                            </template>
                            <template v-else>
                              <span class="text-subtitle-2">{{ getAvatarLetter(m.homeTeamName || m.HomeTeamName) }}</span>
                            </template>
                          </v-avatar>
                          <div class="team-names">
                            <div class="team-name">{{ m.homeTeamName || m.HomeTeamName }}</div>
                            <div class="vs"><v-icon icon="sports_martial_arts" /></div>
                            <div class="team-name">{{ m.awayTeamName || m.AwayTeamName }}</div>
                          </div>
                          <v-avatar size="40" class="ml-3" color="secondary" variant="tonal">
                            <template v-if="teamDetails[m.awayTeamId || m.AwayTeamId]?.logoUrl || teamDetails[m.awayTeamId || m.AwayTeamId]?.LogoUrl">
                              <v-img :src="teamDetails[m.awayTeamId || m.AwayTeamId].logoUrl || teamDetails[m.awayTeamId || m.AwayTeamId].LogoUrl" cover />
                            </template>
                            <template v-else>
                              <span class="text-subtitle-2">{{ getAvatarLetter(m.awayTeamName || m.AwayTeamName) }}</span>
                            </template>
                          </v-avatar>
                        </div>
                        <div class="entry-meta d-flex align-center">
                          <div v-if="m.commentator || m.Commentator" class="crew-chip">
                            <v-icon size="18" class="mr-1" color="primary" icon="mic" /> {{ m.commentator || m.Commentator }}
                          </div>
                          <div v-if="m.director || m.Director" class="crew-chip">
                            <v-icon size="18" class="mr-1" color="secondary" icon="video_camera_back" /> {{ m.director || m.Director }}
                          </div>
                          <div v-if="m.referee || m.Referee" class="crew-chip">
                            <v-icon size="18" class="mr-1" color="success" icon="sports" /> {{ m.referee || m.Referee }}
                          </div>
                          <div v-if="!(m.commentator || m.Commentator || m.director || m.Director || m.referee || m.Referee)" class="text-disabled">
                            <v-icon size="18" class="mr-1" icon="info" /> 暂无导播/解说/裁判信息
                          </div>
                        </div>
                        <div class="entry-actions d-flex align-center">
                          <v-btn v-if="canManageMatches && !selectionMode" color="primary" variant="tonal" density="comfortable" prepend-icon="edit" class="mr-2" @click="openEdit(m)">编辑</v-btn>
                          <v-btn v-if="canManageMatches && !selectionMode" color="error" variant="tonal" density="comfortable" prepend-icon="delete" class="mr-2" @click="onDeleteMatch(m.id || m.Id)">删除</v-btn>
                          <v-spacer />
                          <v-btn v-if="m.liveLink || m.LiveLink" :href="m.liveLink || m.LiveLink" target="_blank" color="secondary" variant="tonal" density="comfortable" prepend-icon="open_in_new">打开直播</v-btn>
                        </div>
                      </v-card>
                    </div>
                  </div>
                </div>
              </div>
            </template>
            <v-card v-else class="pa-8 text-center">
              <v-icon size="40" color="primary" icon="calendar_month" />
              <div class="text-h6 mt-3">尚未创建任何赛程</div>
              <div class="text-medium-emphasis">您可以手动创建或使用随机分组生成</div>
            </v-card>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-row v-if="canManageMatches" dense>
      <v-col cols="12">
        <v-card>
          <v-card-title class="d-flex align-center">
            <v-icon class="mr-2" color="primary" icon="add_circle" />
            手动创建赛程
          </v-card-title>
          <v-card-text>
            <v-row dense>
              <v-col cols="12" md="4">
                <v-select
                  v-model="homeTeamId"
                  :items="eligibleTeams.map(t => ({ title: t.teamName || t.TeamName, value: t.teamId || t.TeamId }))"
                  label="主队"
                  prepend-inner-icon="home"
                />
              </v-col>
              <v-col cols="12" md="4">
                <v-select
                  v-model="awayTeamId"
                  :items="eligibleTeams.map(t => ({ title: t.teamName || t.TeamName, value: t.teamId || t.TeamId }))"
                  label="客队"
                  prepend-inner-icon="person"
                />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field v-model="matchTime" label="比赛时间" type="datetime-local" prepend-inner-icon="schedule" />
              </v-col>
            </v-row>
            <v-row dense>
              <v-col cols="12" md="4"><v-text-field v-model="liveLink" label="直播链接" prepend-inner-icon="videocam" /></v-col>
              <v-col cols="12" md="4"><v-text-field v-model="commentator" label="解说" prepend-inner-icon="mic" /></v-col>
              <v-col cols="12" md="4"><v-text-field v-model="director" label="导播" prepend-inner-icon="video_camera_back" /></v-col>
              <v-col cols="12" md="4"><v-text-field v-model="referee" label="裁判" prepend-inner-icon="sports" /></v-col>
            </v-row>
          </v-card-text>
          <v-card-actions>
            <v-btn :loading="creating" color="primary" prepend-icon="save" @click="onCreateMatch">创建赛程</v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>

    <!-- 随机生成弹窗 -->
    <v-dialog v-model="generateOpen" max-width="640">
      <v-card>
        <v-card-title>随机分组生成</v-card-title>
        <v-card-text>
          <v-alert type="info" class="mb-4" text="使用已审核队伍随机两两配对；奇数队伍将自动轮空。" />
          <v-row dense>
            <v-col cols="12" md="6">
              <v-text-field v-model="generateStartTime" label="开始时间" type="datetime-local" prepend-inner-icon="schedule" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model.number="generateIntervalMinutes" label="间隔（分钟）" type="number" min="1" prepend-inner-icon="timer" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="generateStartDate" label="开始日期（跨天排程）" type="date" prepend-inner-icon="calendar_month" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="dailyStartTime" label="每天起始时间" type="time" prepend-inner-icon="schedule" />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model.number="maxMatchesPerDay" label="每天最多场次" type="number" min="1" prepend-inner-icon="format_list_numbered" />
            </v-col>
            <v-col cols="12">
              <v-switch v-model="includeRegistered" label="包含已报名但未审核的队伍" color="primary" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions>
          <v-btn variant="text" @click="generateOpen = false">取消</v-btn>
          <v-spacer />
          <v-btn color="secondary" variant="tonal" prepend-icon="visibility" @click="onPreviewGenerate">预览分组</v-btn>
          <v-btn :loading="generating" color="primary" prepend-icon="shuffle" @click="onGenerateRandom">直接生成</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- 分组预览动画弹窗 -->
    <v-dialog v-model="previewOpen" max-width="720">
      <v-card>
        <v-card-title class="d-flex align-center">
          <v-icon class="mr-2" icon="visibility" /> 分组预览
          <v-spacer />
          <v-chip v-if="previewAnimating" color="default" variant="tonal">展示中…</v-chip>
          <v-chip v-else color="default" variant="tonal">共 {{ previewItems.length }} 组</v-chip>
        </v-card-title>
        <v-card-text>
          <v-list>
            <template v-for="(item, idx) in previewItems" :key="item.homeId + '-' + item.awayId">
              <v-expand-transition>
                <v-list-item v-show="idx < previewVisibleCount">
                  <div class="d-flex align-center">
                    <v-avatar size="36" class="mr-2" color="primary" variant="tonal">
                      <template v-if="teamDetails[item.homeId]?.logoUrl || teamDetails[item.homeId]?.LogoUrl">
                        <v-img :src="teamDetails[item.homeId].logoUrl || teamDetails[item.homeId].LogoUrl" cover />
                      </template>
                      <template v-else>
                        <span class="text-subtitle-2">{{ getAvatarLetter(item.homeName) }}</span>
                      </template>
                    </v-avatar>
                    <v-icon class="mr-2" icon="sports_martial_arts" color="primary" />
                    <v-avatar size="36" class="mr-2" color="secondary" variant="tonal">
                      <template v-if="teamDetails[item.awayId]?.logoUrl || teamDetails[item.awayId]?.LogoUrl">
                        <v-img :src="teamDetails[item.awayId].logoUrl || teamDetails[item.awayId].LogoUrl" cover />
                      </template>
                      <template v-else>
                        <span class="text-subtitle-2">{{ getAvatarLetter(item.awayName) }}</span>
                      </template>
                    </v-avatar>
                    <div class="text-subtitle-1 match-title">{{ item.homeName }} VS {{ item.awayName }}</div>
                    <v-spacer />
                    <div class="text-medium-emphasis ml-2">{{ new Date(item.matchTime).toLocaleString() }}</div>
                  </div>
                </v-list-item>
              </v-expand-transition>
            </template>
          </v-list>
        </v-card-text>
        <v-card-actions>
          <v-btn variant="text" @click="previewOpen = false">取消</v-btn>
          <v-spacer />
          <v-btn :loading="previewCreating" color="primary" prepend-icon="save" @click="onConfirmGenerateFromPreview">确认生成赛程</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- 编辑赛程弹窗 -->
    <v-dialog v-model="editOpen" max-width="640">
      <v-card>
        <v-card-title>编辑赛程</v-card-title>
        <v-card-text>
          <v-row dense>
            <v-col cols="12">
              <v-text-field v-model="editMatchTime" label="比赛时间" type="datetime-local" prepend-inner-icon="schedule" />
            </v-col>
            <v-col cols="12" md="6"><v-text-field v-model="editLiveLink" label="直播链接" prepend-inner-icon="videocam" /></v-col>
            <v-col cols="12" md="6"><v-text-field v-model="editCommentator" label="解说" prepend-inner-icon="mic" /></v-col>
            <v-col cols="12" md="6"><v-text-field v-model="editDirector" label="导播" prepend-inner-icon="video_camera_back" /></v-col>
            <v-col cols="12" md="6"><v-text-field v-model="editReferee" label="裁判" prepend-inner-icon="sports" /></v-col>
          </v-row>
        </v-card-text>
        <v-card-actions>
          <v-btn variant="text" @click="editOpen = false">取消</v-btn>
          <v-spacer />
          <v-btn :loading="editing" color="primary" prepend-icon="save" @click="onSaveEdit">保存</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<style scoped>
.match-card {
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}
.match-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.12);
}
.match-title {
  font-weight: 600;
}
.timeline-item {
  padding-bottom: 8px;
}

/* 新时间轴样式 */
.timeline {
  position: relative;
}
.timeline-day {
  margin-bottom: 24px;
}
.day-header {
  position: sticky;
  top: 64px; /* 贴近页面顶部，避免被 PageHero 挡住 */
  z-index: 2;
  border-radius: 12px;
  padding: 10px 14px;
  margin-bottom: 10px;
}
.day-title {
  font-weight: 600;
}
.timeline-track {
  position: relative;
  margin-left: 18px;
  border-left: 2px solid var(--v-theme-primary);
  padding-left: 24px;
}
.timeline-entry {
  position: relative;
  margin: 16px 0;
}
.entry-dot {
  position: absolute;
  left: -8px;
  top: 22px;
  width: 14px;
  height: 14px;
  border-radius: 50%;
  background: var(--v-theme-primary);
  box-shadow: 0 2px 8px rgba(0,0,0,0.25);
}
.entry-card {
  border-radius: 16px;
  overflow: hidden;
  background: linear-gradient(180deg, rgba(255,255,255,0.95), rgba(255,255,255,0.85));
  border: 1px solid rgba(0,0,0,0.06);
  position: relative;
}
.entry-card::before {
  content: '';
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 4px;
  background: linear-gradient(180deg, var(--v-theme-primary), var(--v-theme-secondary));
  opacity: 0.85;
}
.entry-top {
  padding: 10px 16px 0 16px;
}
.time-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: #ffffff;
  color: var(--v-theme-primary);
  border: 1px solid rgba(0,0,0,0.08);
  border-radius: 999px;
  padding: 6px 12px;
  font-weight: 700;
  letter-spacing: 0.3px;
  box-shadow: 0 4px 12px rgba(0,0,0,0.06);
}
.entry-main {
  padding: 12px 16px;
}
.entry-meta {
  padding: 0 16px 8px 16px;
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
.crew-chip {
  display: inline-flex;
  align-items: center;
  background: rgba(0,0,0,0.04);
  color: #333;
  border-radius: 999px;
  padding: 6px 10px;
  font-weight: 500;
}
.team-names {
  display: flex;
  align-items: center;
  gap: 12px;
  font-weight: 600;
}
.team-name {
  max-width: 220px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.vs {
  display: flex;
  align-items: center;
  color: var(--v-theme-primary);
}
.entry-actions {
  padding: 0 16px 12px 16px;
}

@media (max-width: 600px) {
  .timeline-track { padding-left: 16px; }
  .team-name { max-width: 140px; }
  .entry-top { padding: 8px 12px 0; }
  .entry-main { padding: 10px 12px; }
  .entry-actions { padding: 0 12px 10px; }
}
</style>
<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import PageHero from '../components/PageHero.vue'
import { getEvent, getEventRegistrations, updateTournamentConfig, generateTestRegistrations } from '../services/events'
import { getMatchesPaged, createMatch, deleteMatch, updateMatch, updateMatchScores, generateSchedule, getScheduleConflicts, generateNextRound } from '../services/matches'
import { currentUser, isAuthenticated } from '../stores/auth'
import { getTeam } from '../services/teams'
import ResultDialog from '../components/ResultDialog.vue'
import { extractErrorDetails } from '../services/api'

const route = useRoute()
const router = useRouter()
const eventId = computed(() => route.params.id)

const ev = ref(null)
const registrations = ref([])
const matches = ref([])
const loadingEvent = ref(false)
const loadingTeams = ref(false)
const generatingTestTeams = ref(false)
const loadingMatches = ref(false)
const errorMsg = ref('')
const successMsg = ref('')
const showSuccess = ref(false)
const errorOpen = ref(false)
const errorDetails = ref([])

const page = ref(1)
const pageSize = ref(20)
const totalCount = ref(0)
const maxPage = computed(() => Math.max(1, Math.ceil((totalCount.value || 0) / (pageSize.value || 1))))

const heroTitle = computed(() => `赛事赛程`)

const canManageEvent = computed(() => {
  if (!isAuthenticated.value || !ev.value) return false
  const me = currentUser.value
  if (!me) return false
  const createdByUserId = ev.value.createdByUserId || ev.value.CreatedByUserId
  const adminIds = ev.value.adminUserIds || ev.value.AdminUserIds || []
  const myId = me.id || me.Id
  const roleName = me.roleName || me.RoleName
  const isCreator = !!createdByUserId && !!myId && createdByUserId === myId
  const isAdmin = roleName === 'Admin' || roleName === 'SuperAdmin'
  const isEventAdmin = !!myId && Array.isArray(adminIds) && adminIds.includes(myId)
  return isCreator || isAdmin || isEventAdmin
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
const parseCustom = m => {
  const s = m?.customData || m?.CustomData
  if (!s) return {}
  try { return JSON.parse(s) || {} } catch { return {} }
}
const getStage = m => (m?.stage || m?.Stage || parseCustom(m).stage || '')
const getReplayLink = m => (m?.replayLink || m?.ReplayLink || parseCustom(m).replayLink || '')
const getGroupLabel = m => {
  const d = parseCustom(m)
  const label = d.groupLabel
  if (label) return label
  const gi = d.groupIndex
  if (gi == null || gi < 0) return ''
  return gi < 26 ? String.fromCharCode('A'.charCodeAt(0) + gi) : `G${gi + 1}`
}
function getWinnerName(m) {
  const d = parseCustom(m)
  const n = m?.winnerTeamName || m?.WinnerTeamName || d.winnerTeamName
  if (n) return n
  const wid = m?.winnerTeamId || m?.WinnerTeamId || d.winnerTeamId
  const hid = m?.homeTeamId || m?.HomeTeamId
  const aid = m?.awayTeamId || m?.AwayTeamId
  if (!wid) return ''
  if (wid === hid) return m?.homeTeamName || m?.HomeTeamName || ''
  if (wid === aid) return m?.awayTeamName || m?.AwayTeamName || ''
  return ''
}

// 分组预览与动画
const previewOpen = ref(false)
const previewItems = ref([])
const previewVisibleCount = ref(0)
const previewAnimating = ref(false)
const previewCreating = ref(false)

const ceremonyOpen = ref(false)
const ceremonyItems = ref([])
const ceremonyIndex = ref(0)
const ceremonyProgress = ref(0)
const ceremonyRunning = ref(false)
const ceremonyCelebrating = ref(false)
const currentCeremonyItem = computed(() => ceremonyItems.value[ceremonyIndex.value])
let ceremonyTimer = null

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
    const created = []
    for (const item of previewItems.value) {
      const payload = {
        homeTeamId: item.homeId,
        awayTeamId: item.awayId,
        matchTime: item.matchTime,
        eventId: eventGuid,
        customData: '{}',
      }
      const m = await createMatch(payload)
      created.push(m)
    }
    successMsg.value = `已生成 ${previewItems.value.length} 场赛程`
    previewOpen.value = false
    await showCeremonyForCreated(created)
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

// 小组阶段元信息与积分榜
const groupStageGroups = computed(() => {
  try {
    const cd = ev.value?.customData || ev.value?.CustomData
    if (!cd) return []
    const obj = JSON.parse(cd) || {}
    const gs = obj.groupStage || obj.GroupStage
    const arr = gs?.groups || gs?.Groups
    if (!Array.isArray(arr)) return []
    return arr.map(o => ({
      index: o.index ?? o.Index,
      label: o.label ?? o.Label,
      teamIds: (o.teamIds || o.TeamIds || []).map(x => (typeof x === 'string' ? x : String(x))),
      teamNames: o.teamNames || o.TeamNames || []
    }))
  } catch { return [] }
})

const groupStageStandings = computed(() => {
  try {
    const cd = ev.value?.customData || ev.value?.CustomData
    if (!cd) return []
    const obj = JSON.parse(cd) || {}
    const gs = obj.groupStage || obj.GroupStage
    const arr = gs?.standings || gs?.Standings
    if (!Array.isArray(arr)) return []
    return arr.map(o => ({
      groupIndex: o.groupIndex ?? o.GroupIndex,
      items: (o.items || o.Items || []).map(it => ({ teamId: it.teamId || it.TeamId, points: Number(it.points || it.Points || 0) }))
    }))
  } catch { return [] }
})

// 小组筛选
const selectedGroupIndex = ref(null)
const groupFilterItems = computed(() => {
  const items = [{ title: '全部小组', value: null }]
  for (const g of groupStageGroups.value || []) {
    const lab = g.label ?? (g.index < 26 ? String.fromCharCode('A'.charCodeAt(0) + (g.index ?? 0)) : `G${(g.index ?? 0) + 1}`)
    items.push({ title: `组 ${lab}`, value: g.index })
  }
  return items
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

function toDateInput(value) {
  if (!value) return ''
  const d = new Date(value)
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}`
}

function applyTournamentConfigFrom(e) {
  if (!e) return
  const cd = e?.customData || e?.CustomData
  if (!cd) return
  try {
    const obj = JSON.parse(cd) || {}
    const tc = obj?.tournamentConfig || obj?.TournamentConfig
    if (!tc || typeof tc !== 'object') return
    const fmt = tc.format || tc.Format
    const bo = tc.bestOf ?? tc.BestOf
    const rounds = tc.rounds ?? tc.Rounds
    const gsize = tc.groupSize ?? tc.GroupSize
    const adv = tc.advancePerGroup ?? tc.AdvancePerGroup
    const itv = tc.intervalMinutes ?? tc.IntervalMinutes
    const sdate = tc.startDate || tc.StartDate
    const dstart = tc.dailyStartTime || tc.DailyStartTime
    const maxpd = tc.maxMatchesPerDay ?? tc.MaxMatchesPerDay
    if (fmt) tournamentFormat.value = String(fmt)
    if (bo != null) tournamentBestOf.value = Number(bo) || 1
    if (rounds != null) swissRounds.value = Number(rounds) || swissRounds.value
    if (gsize != null) groupsCount.value = Number(gsize) || groupsCount.value
    if (adv != null) advancePerGroup.value = Number(adv) || advancePerGroup.value
    if (itv != null) scheduleIntervalMinutes.value = Number(itv) || scheduleIntervalMinutes.value
    if (sdate) generateStartDate.value = toDateInput(sdate)
    if (dstart) dailyStartTime.value = String(dstart)
    if (maxpd != null) maxMatchesPerDay.value = Number(maxpd) || null
  } catch {}
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

watch(successMsg, (v) => { if (v) showSuccess.value = true })
watch(errorMsg, (v) => { if (v) errorOpen.value = true })

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
    const created = []
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
      const m = await createMatch(payload)
      created.push(m)
    }
    successMsg.value = `已生成 ${pairs.length} 场赛程`
    await showCeremonyForCreated(created)
    await loadMatches()
    generateOpen.value = false
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '随机生成失败'
    errorDetails.value = extractErrorDetails(e?.payload)
  } finally {
    generating.value = false
  }
}

const tournamentFormat = ref('single_elim')
const tournamentBestOf = ref(1)
const swissRounds = ref(3)
const groupsCount = ref(4)
const advancePerGroup = ref(2)
const scheduleIntervalMinutes = ref(60)
const savingTournamentConfig = ref(false)
const generatingTournament = ref(false)
const generatingNextRound = ref(false)
const loadingConflicts = ref(false)
const conflictItems = ref([])
const conflictError = ref('')

async function onSaveTournamentConfig() {
  errorMsg.value = ''
  successMsg.value = ''
  if (!canManageMatches.value) { errorMsg.value = '没有权限'; return }
  const dto = {
    format: tournamentFormat.value,
    bestOf: Number(tournamentBestOf.value || 0) || 1,
    rounds: tournamentFormat.value === 'swiss' ? Number(swissRounds.value || 0) || 1 : null,
    groupSize: tournamentFormat.value === 'groups' ? Number(groupsCount.value || 0) || 1 : null,
    advancePerGroup: tournamentFormat.value === 'groups' ? Number(advancePerGroup.value || 0) || 1 : null,
    intervalMinutes: Number(scheduleIntervalMinutes.value || 60),
    startDate: generateStartDate.value ? new Date(generateStartDate.value).toISOString() : null,
    dailyStartTime: dailyStartTime.value || null,
    maxMatchesPerDay: maxMatchesPerDay.value ? Number(maxMatchesPerDay.value) : null
  }
  savingTournamentConfig.value = true
  try {
    await updateTournamentConfig(eventId.value, dto)
    successMsg.value = '赛制配置已保存'
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '保存赛制配置失败'
    errorDetails.value = extractErrorDetails(e?.payload)
  } finally {
    savingTournamentConfig.value = false
  }
}

async function onGenerateTournamentSchedule() {
  errorMsg.value = ''
  successMsg.value = ''
  if (!canManageMatches.value) { errorMsg.value = '没有权限生成赛程'; return }
  const stage = tournamentFormat.value
  const dto = {
    stage,
    bestOf: Number(tournamentBestOf.value || 0) || 1,
    round: tournamentFormat.value === 'swiss' ? Number(swissRounds.value || 0) || 1 : null,
    intervalMinutes: Number(scheduleIntervalMinutes.value || 60),
    startDate: generateStartDate.value ? new Date(generateStartDate.value).toISOString() : null,
    dailyStartTime: dailyStartTime.value || null,
    maxMatchesPerDay: maxMatchesPerDay.value ? Number(maxMatchesPerDay.value) : null,
    groupSize: stage === 'groups' ? (Number(groupsCount.value || 0) || 1) : null,
    advancePerGroup: stage === 'groups' ? (Number(advancePerGroup.value || 0) || 1) : null
  }
  generatingTournament.value = true
  try {
    const created = await generateSchedule(eventId.value, dto)
    const list = Array.isArray(created) ? created : []
    successMsg.value = `赛程生成成功（${list.length} 场）`
    if (stage === 'groups') {
      await playGroupRevealFromCreated(list)
    }
    await showCeremonyForCreated(list)
    await loadMatches()
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '生成赛程失败'
    errorDetails.value = extractErrorDetails(e?.payload)
  } finally {
    generatingTournament.value = false
  }
}

async function onGenerateNextRound() {
  errorMsg.value = ''
  successMsg.value = ''
  if (!canManageMatches.value) { errorMsg.value = '没有权限生成下一轮'; return }
  const stage = tournamentFormat.value
  const dto = {
    stage,
    bestOf: Number(tournamentBestOf.value || 0) || 1,
    intervalMinutes: Number(scheduleIntervalMinutes.value || 60),
    startDate: generateStartDate.value ? new Date(generateStartDate.value).toISOString() : null,
    dailyStartTime: dailyStartTime.value || null,
    maxMatchesPerDay: maxMatchesPerDay.value ? Number(maxMatchesPerDay.value) : null
  }
  generatingNextRound.value = true
  try {
    const created = await generateNextRound(eventId.value, dto)
    const list = Array.isArray(created) ? created : []
    successMsg.value = `已添加下一轮赛程（${list.length} 场）`
    await showCeremonyForCreated(list)
    await loadMatches()
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '生成下一轮失败'
    errorDetails.value = extractErrorDetails(e?.payload)
  } finally {
    generatingNextRound.value = false
  }
}

function toCeremonyItemFromMatch(m) {
  const homeId = m.homeTeamId || m.HomeTeamId
  const awayId = m.awayTeamId || m.AwayTeamId
  const homeName = m.homeTeamName || m.HomeTeamName || ''
  const awayName = m.awayTeamName || m.AwayTeamName || ''
  const matchTime = m.matchTime || m.MatchTime
  return { homeId, awayId, homeName, awayName, matchTime }
}

function startCeremonyTimer() {
  if (ceremonyTimer) { clearInterval(ceremonyTimer); ceremonyTimer = null }
  ceremonyRunning.value = true
  ceremonyProgress.value = 0
  ceremonyCelebrating.value = true
  setTimeout(() => { ceremonyCelebrating.value = false }, 1000)
  const durationMs = 8000
  const start = Date.now()
  ceremonyTimer = setInterval(() => {
    const elapsed = Date.now() - start
    const p = Math.min(100, Math.floor((elapsed / durationMs) * 100))
    ceremonyProgress.value = p
    if (elapsed >= durationMs) advanceCeremony()
  }, 50)
}

function stopCeremonyTimer() {
  if (ceremonyTimer) { clearInterval(ceremonyTimer); ceremonyTimer = null }
  ceremonyRunning.value = false
}

function advanceCeremony() {
  stopCeremonyTimer()
  if (ceremonyIndex.value + 1 < ceremonyItems.value.length) {
    ceremonyIndex.value += 1
    startCeremonyTimer()
  } else {
    ceremonyProgress.value = 100
    ceremonyRunning.value = false
  }
}

async function showCeremonyForCreated(list) {
  const items = (Array.isArray(list) ? list : []).map(toCeremonyItemFromMatch)
  ceremonyItems.value = items
  await Promise.all(items.flatMap(it => [ensureTeamDetail(it.homeId), ensureTeamDetail(it.awayId)]))
  ceremonyIndex.value = 0
  ceremonyProgress.value = 0
  ceremonyOpen.value = true
  startCeremonyTimer()
}

watch(ceremonyOpen, (v) => { if (!v) stopCeremonyTimer() })

async function onCheckConflicts() {
  conflictError.value = ''
  loadingConflicts.value = true
  try {
    const items = await getScheduleConflicts(eventId.value)
    conflictItems.value = Array.isArray(items) ? items : (items?.items || items?.Items || [])
    if (!Array.isArray(items) && conflictItems.value.length === 0) {
      conflictItems.value = items ? [items] : []
    }
  } catch (e) {
    conflictError.value = e?.payload?.message || e?.message || '冲突检测失败'
  } finally {
    loadingConflicts.value = false
  }
}

async function loadEvent() {
  loadingEvent.value = true
  try {
    ev.value = await getEvent(eventId.value)
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '获取赛事信息失败'
    errorDetails.value = extractErrorDetails(e?.payload)
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

async function onGenerateTestTeams() {
  errorMsg.value = ''
  successMsg.value = ''
  if (!canManageEvent.value) { errorMsg.value = '没有权限生成测试队伍'; return }
  generatingTestTeams.value = true
  try {
    await generateTestRegistrations(eventId.value, { count: 64, namePrefix: '测试战队', approve: true })
    successMsg.value = '已生成64个测试队伍并报名'
    showSuccess.value = true
    await loadRegistrations()
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '生成测试队伍失败'
    errorDetails.value = extractErrorDetails(e?.payload)
    errorOpen.value = true
  } finally {
    generatingTestTeams.value = false
  }
}

async function loadMatches() {
  loadingMatches.value = true
  try {
    const res = await getMatchesPaged({ eventId: eventId.value, page: page.value, pageSize: pageSize.value, groupIndex: selectedGroupIndex.value })
    matches.value = res.items || []
    totalCount.value = res.totalCount || (matches.value || []).length
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
    errorDetails.value = extractErrorDetails(e?.payload)
  }
}

// 编辑赛程弹窗与逻辑
const editOpen = ref(false)
const editTarget = ref(null)
const editMatchTime = ref('')
const editLiveLink = ref('')
const editStage = ref('')
const editReplayLink = ref('')
const editWinnerTeamId = ref(null)
const editCommentator = ref('')
const editDirector = ref('')
const editReferee = ref('')
const editing = ref(false)

const scoreboardOpenIds = ref(new Set())
const editScoreOpen = ref(false)
const editScoreTarget = ref(null)
const editScoreGames = ref([])
const editScoreBestOf = ref(0)

function getId(m) { return m?.id || m?.Id }

function openEdit(m) {
  editTarget.value = m
  editMatchTime.value = toLocalDateTimeInput(m.matchTime || m.MatchTime)
  editLiveLink.value = m.liveLink || m.LiveLink || ''
  const custom = parseCustom(m)
  editStage.value = m.stage || m.Stage || custom.stage || ''
  editReplayLink.value = m.replayLink || m.ReplayLink || custom.replayLink || ''
  const wid = m.winnerTeamId || m.WinnerTeamId || custom.winnerTeamId
  const hid = m.homeTeamId || m.HomeTeamId
  const aid = m.awayTeamId || m.AwayTeamId
  editWinnerTeamId.value = (wid === hid || wid === aid) ? wid : null
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
    customData: (() => {
      const base = parseCustom(editTarget.value)
      const s = editStage.value?.trim()
      const rl = editReplayLink.value?.trim()
      const hid = editTarget.value?.homeTeamId || editTarget.value?.HomeTeamId
      const aid = editTarget.value?.awayTeamId || editTarget.value?.AwayTeamId
      const nameHome = editTarget.value?.homeTeamName || editTarget.value?.HomeTeamName
      const nameAway = editTarget.value?.awayTeamName || editTarget.value?.AwayTeamName
      if (s) base.stage = s; else delete base.stage
      if (rl) base.replayLink = rl; else delete base.replayLink
      if (editWinnerTeamId.value && (editWinnerTeamId.value === hid || editWinnerTeamId.value === aid)) {
        base.winnerTeamId = editWinnerTeamId.value
        base.winnerTeamName = editWinnerTeamId.value === hid ? nameHome : nameAway
      } else {
        delete base.winnerTeamId
        delete base.winnerTeamName
      }
      try { return JSON.stringify(base) } catch { return '{}' }
    })(),
    commentator: editCommentator.value?.trim() || null,
    director: editDirector.value?.trim() || null,
    referee: editReferee.value?.trim() || null,
  }
  editing.value = true
  try {
    await updateMatch(id, payload)
    successMsg.value = '赛程更新成功'
    editOpen.value = false
    await loadEvent()
    await loadMatches()
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '更新赛程失败'
    errorDetails.value = extractErrorDetails(e?.payload)
  } finally {
    editing.value = false
  }
}

function getGames(m) {
  const d = parseCustom(m)
  const arr = Array.isArray(d.games) ? d.games : []
  return arr.map(g => ({ home: Number(g?.home ?? 0), away: Number(g?.away ?? 0) }))
}
function isScoreOpen(m) {
  const id = getId(m)
  return id ? scoreboardOpenIds.value.has(id) : false
}
function toggleScore(m) {
  const id = getId(m)
  if (!id) return
  const set = scoreboardOpenIds.value
  if (set.has(id)) set.delete(id); else set.add(id)
}
function openEditScore(m) {
  editScoreTarget.value = m
  const games = getGames(m)
  editScoreGames.value = games.length ? games : [{ home: 0, away: 0 }]
  const d = parseCustom(m)
  const bo = Number(d.bestOf || editScoreGames.value.length || 0)
  editScoreBestOf.value = bo > 0 ? bo : editScoreGames.value.length
  editScoreOpen.value = true
}
async function onSaveEditScore() {
  errorMsg.value = ''
  successMsg.value = ''
  const id = getId(editScoreTarget.value)
  if (!id) { errorMsg.value = '无效的赛程'; return }
  const cleaned = (editScoreGames.value || []).map(g => ({
    home: Math.max(0, Number(g?.home ?? 0)),
    away: Math.max(0, Number(g?.away ?? 0)),
  }))
  try {
    const bo = Number(editScoreBestOf.value || cleaned.length || 0)
    await updateMatchScores(id, { bestOf: bo > 0 ? bo : cleaned.length, games: cleaned })
    successMsg.value = '赛果更新成功'
    editScoreOpen.value = false
    await loadEvent()
    await loadMatches()
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '赛果更新失败'
    errorDetails.value = extractErrorDetails(e?.payload)
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
  applyTournamentConfigFrom(ev.value)
  await loadRegistrations()
  await loadMatches()
  const compStart = ev.value?.competitionStartTime || ev.value?.CompetitionStartTime
  if (compStart) {
    const d = new Date(compStart)
    const pad = n => String(n).padStart(2, '0')
    const s = `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
    if (!generateStartTime.value) generateStartTime.value = s
    if (!matchTime.value) matchTime.value = s
  }
})

const groupRevealOpen = ref(false)
const groupRevealPlaying = ref(false)
const groupRevealColumnsData = ref([])
const groupRevealDisplayed = ref({})
const groupRevealQueue = ref([])
let groupRevealTimer = null
let groupRevealResolver = null
const groupRevealIntervalMs = 2000

function buildGroupRevealColumnsFromMatches(list) {
  const map = {}
  for (const m of (list || [])) {
    const s = String(getStage(m)).toLowerCase()
    if (s !== 'groups') continue
    const lab = getGroupLabel(m) || '未标注'
    if (!map[lab]) map[lab] = new Set()
    const hid = String(m.homeTeamId || m.HomeTeamId || '')
    const aid = String(m.awayTeamId || m.AwayTeamId || '')
    if (hid) map[lab].add(hid)
    if (aid) map[lab].add(aid)
  }
  const labels = Object.keys(map).sort((a,b)=>a.localeCompare(b))
  const nameMap = teamNameById.value || {}
  return labels.map(lab => ({ label: lab, names: Array.from(map[lab]).map(id => nameMap[id] || ('#' + id)) }))
}

function labelFromIndex(i) { return i < 26 ? String.fromCharCode('A'.charCodeAt(0) + i) : `G${i + 1}` }

function buildGroupRevealColumnsFromConfigOrRegistrations() {
  const nameMap = teamNameById.value || {}
  const groups = groupStageGroups.value || []
  if (Array.isArray(groups) && groups.length) {
    return groups.map((g, idx) => {
      const lab = g.label ?? labelFromIndex(Number(g.index ?? idx) || idx)
      const names = (Array.isArray(g.teamNames) && g.teamNames.length)
        ? g.teamNames
        : (Array.isArray(g.teamIds) ? g.teamIds.map(id => nameMap[id] || ('#' + id)) : [])
      return { label: lab, names }
    })
  }
  const count = Number(groupsCount.value || 0) || 1
  const arr = new Array(count).fill(0).map(() => [])
  const baseTeams = eligibleTeams.value || []
  const ordered = baseTeams.slice().sort((a, b) => {
    const an = (a.teamName || a.TeamName || '').toLowerCase()
    const bn = (b.teamName || b.TeamName || '').toLowerCase()
    return an.localeCompare(bn)
  })
  ordered.forEach((t, idx) => {
    const gid = idx % count
    const id = t.teamId || t.TeamId
    const nm = nameMap[id] || (t.teamName || t.TeamName || ('#' + id))
    arr[gid].push(nm)
  })
  return arr.map((names, i) => ({ label: labelFromIndex(i), names }))
}

function openGroupReveal(columns) {
  groupRevealColumnsData.value = columns || []
  const obj = {}
  for (const col of groupRevealColumnsData.value) obj[col.label] = []
  groupRevealDisplayed.value = obj
  groupRevealQueue.value = []
  for (const col of groupRevealColumnsData.value) {
    for (const name of (col.names || [])) groupRevealQueue.value.push({ label: col.label, name })
  }
  groupRevealOpen.value = true
}

function startGroupReveal() {
  if (groupRevealPlaying.value) return
  groupRevealPlaying.value = true
  if (groupRevealTimer) { clearInterval(groupRevealTimer); groupRevealTimer = null }
  let idx = 0
  groupRevealTimer = setInterval(() => {
    const step = groupRevealQueue.value[idx]
    if (!step) {
      clearInterval(groupRevealTimer)
      groupRevealTimer = null
      groupRevealPlaying.value = false
      groupRevealOpen.value = false
      const resolve = groupRevealResolver; groupRevealResolver = null; if (resolve) resolve()
      return
    }
    const lab = step.label
    const name = step.name
    const arr = groupRevealDisplayed.value[lab] || []
    arr.push({ name, key: Math.random().toString(36).slice(2) })
    groupRevealDisplayed.value[lab] = arr
    idx += 1
  }, groupRevealIntervalMs)
}

function stopGroupReveal() { if (groupRevealTimer) { clearInterval(groupRevealTimer); groupRevealTimer = null } groupRevealPlaying.value = false }
function resetGroupReveal() {
  const obj = {}
  for (const col of groupRevealColumnsData.value || []) obj[col.label] = []
  groupRevealDisplayed.value = obj
  groupRevealQueue.value = []
}

async function playGroupRevealFromCreated(list) {
  let columns = buildGroupRevealColumnsFromConfigOrRegistrations()
  if (!columns.length) {
    columns = buildGroupRevealColumnsFromMatches(list)
  }
  if (!columns.length) return
  openGroupReveal(columns)
  return new Promise(resolve => { groupRevealResolver = resolve; startGroupReveal() })
}

watch(selectedGroupIndex, async () => {
  page.value = 1
  await loadMatches()
})
</script>

<template>
  <PageHero :title="heroTitle" subtitle="查看与管理赛事赛程" icon="calendar_month">
    <template #actions>
      <v-btn class="mb-3" variant="text" @click="goBackDetail">
        <template #prepend>
          <v-icon icon="chevron_left" />
        </template>
        返回赛事详情
      </v-btn>
      <v-btn class="mb-3 ml-2" variant="text" prepend-icon="account_tree" :to="{ name: 'event-bracket', params: { id: eventId } }">查看赛程图</v-btn>
    </template>
  </PageHero>

  <v-container class="py-6 page-container">
    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
    <v-alert v-if="successMsg" type="success" class="mb-4">
      <div class="d-flex align-center">
        <lottie-player src="https://assets9.lottiefiles.com/packages/lf20_jcikwtux.json" autoplay style="width:32px;height:32px;margin-right:8px"></lottie-player>
        <div>{{ successMsg }}</div>
      </div>
    </v-alert>

    <v-row class="mb-6" dense>
      <v-col cols="12">
        <v-card rounded="lg" elevation="1">
          <v-card-title class="d-flex align-center">
            <v-icon class="mr-2" color="primary" icon="timeline" />
            <span>赛程时间轴</span>
            <v-spacer />
            <v-select
              v-model="selectedGroupIndex"
              :items="groupFilterItems"
              density="compact"
              style="max-width: 180px"
              label="筛选小组"
            />
            <v-chip v-if="loadingMatches" label color="default" variant="tonal">加载中…</v-chip>
            <v-chip v-else label color="default" variant="tonal">共 {{ totalCount }} 场</v-chip>
            <v-btn v-if="canManageMatches" class="ml-2" variant="text" prepend-icon="select_all" @click="selectionMode = !selectionMode">{{ selectionMode ? '取消批量删除' : '批量删除' }}</v-btn>
            <v-btn v-if="selectionMode && canManageMatches" class="ml-1" variant="text" @click="selectAll">全选</v-btn>
            <v-btn v-if="selectionMode && canManageMatches" class="ml-1" color="error" variant="tonal" :disabled="!selectedIds.length" :loading="batchDeleting" @click="onBatchDelete">删除已选</v-btn>
          </v-card-title>
          <v-card-text>
            <div class="d-flex align-center mb-3">
              <div class="text-medium-emphasis">第 {{ page }} 页 / 每页 {{ pageSize }} 条</div>
              <v-spacer />
              <v-select v-model.number="pageSize" :items="[10,20,50,100]" density="compact" style="max-width: 140px" label="每页" @update:modelValue="() => { page = 1; loadMatches() }" />
            </div>
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
                          <v-chip v-if="getStage(m)" size="small" color="primary" variant="tonal">{{ getStage(m) }}</v-chip>
                          <v-chip v-if="getGroupLabel(m)" size="small" color="secondary" variant="tonal" class="ml-2">组 {{ getGroupLabel(m) }}</v-chip>
                          <v-chip v-if="m.liveLink || m.LiveLink" size="small" color="secondary" variant="tonal" prepend-icon="videocam">直播</v-chip>
                        </div>
                        <div class="entry-main d-flex align-center">
                          <router-link :to="{ name: 'team-detail', params: { id: m.homeTeamId || m.HomeTeamId } }" class="d-inline-flex align-center text-decoration-none mr-3">
                            <v-avatar size="64" color="primary" variant="tonal">
                              <template v-if="teamDetails[m.homeTeamId || m.HomeTeamId]?.logoUrl || teamDetails[m.homeTeamId || m.HomeTeamId]?.LogoUrl">
                                <v-img :src="teamDetails[m.homeTeamId || m.HomeTeamId].logoUrl || teamDetails[m.homeTeamId || m.HomeTeamId].LogoUrl" cover />
                              </template>
                              <template v-else>
                                <span class="text-subtitle-2">{{ getAvatarLetter(m.homeTeamName || m.HomeTeamName) }}</span>
                              </template>
                            </v-avatar>
                          </router-link>
                          <div class="team-names">
                            <router-link :to="{ name: 'team-detail', params: { id: m.homeTeamId || m.HomeTeamId } }" class="team-name-link text-decoration-none">
                              <div class="team-name">{{ m.homeTeamName || m.HomeTeamName }}</div>
                            </router-link>
                            <div class="vs"><v-icon icon="sports_martial_arts" /></div>
                            <router-link :to="{ name: 'team-detail', params: { id: m.awayTeamId || m.AwayTeamId } }" class="team-name-link text-decoration-none">
                              <div class="team-name">{{ m.awayTeamName || m.AwayTeamName }}</div>
                            </router-link>
                          </div>
                          <router-link :to="{ name: 'team-detail', params: { id: m.awayTeamId || m.AwayTeamId } }" class="d-inline-flex align-center text-decoration-none ml-3">
                            <v-avatar size="64" color="secondary" variant="tonal">
                              <template v-if="teamDetails[m.awayTeamId || m.AwayTeamId]?.logoUrl || teamDetails[m.awayTeamId || m.AwayTeamId]?.LogoUrl">
                                <v-img :src="teamDetails[m.awayTeamId || m.AwayTeamId].logoUrl || teamDetails[m.awayTeamId || m.AwayTeamId].LogoUrl" cover />
                              </template>
                              <template v-else>
                                <span class="text-subtitle-2">{{ getAvatarLetter(m.awayTeamName || m.AwayTeamName) }}</span>
                              </template>
                            </v-avatar>
                          </router-link>
                        </div>
                        <div class="score-toggle d-flex align-center px-4 pb-2">
                          <v-btn variant="text" density="comfortable" prepend-icon="expand_more" @click="toggleScore(m)">
                            {{ isScoreOpen(m) ? '隐藏赛果' : '查看赛果' }}
                          </v-btn>
                          <v-spacer />
                          <v-chip v-if="getGames(m).length" label color="default" variant="tonal">共 {{ getGames(m).length }} 局</v-chip>
                        </div>
                        <v-expand-transition>
                          <div v-show="isScoreOpen(m)" class="scoreboard px-4 pb-3">
                            <v-table density="comfortable">
                              <thead>
                                <tr>
                                  <th class="text-left" style="width: 140px"></th>
                                  <th v-for="(g, idx) in getGames(m)" :key="idx" class="text-center">Game {{ idx + 1 }}</th>
                                </tr>
                              </thead>
                              <tbody>
                                <tr>
                                  <td class="text-left">{{ m.homeTeamName || m.HomeTeamName }}</td>
                                  <td v-for="(g, idx) in getGames(m)" :key="'h-' + idx" class="text-center">{{ g.home }}</td>
                                </tr>
                                <tr>
                                  <td class="text-left">{{ m.awayTeamName || m.AwayTeamName }}</td>
                                  <td v-for="(g, idx) in getGames(m)" :key="'a-' + idx" class="text-center">{{ g.away }}</td>
                                </tr>
                              </tbody>
                            </v-table>
                          </div>
                        </v-expand-transition>
                        <div class="entry-meta d-flex align-center">
                          <v-chip v-if="getWinnerName(m)" size="small" color="success" variant="tonal" class="mr-2" prepend-icon="emoji_events">{{ getWinnerName(m) }} 获胜</v-chip>
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
                          <v-btn v-if="canManageMatches && !selectionMode" color="secondary" variant="tonal" density="comfortable" prepend-icon="grid_on" class="mr-2" @click="openEditScore(m)">编辑赛果</v-btn>
                          <v-btn v-if="canManageMatches && !selectionMode" color="error" variant="tonal" density="comfortable" prepend-icon="delete" class="mr-2" @click="onDeleteMatch(m.id || m.Id)">删除</v-btn>
                          <v-spacer />
                          <v-btn v-if="m.liveLink || m.LiveLink" :href="m.liveLink || m.LiveLink" target="_blank" color="secondary" variant="tonal" density="comfortable" prepend-icon="open_in_new">打开直播</v-btn>
                          <v-btn v-if="getReplayLink(m)" :href="getReplayLink(m)" target="_blank" color="secondary" variant="tonal" density="comfortable" prepend-icon="open_in_new" class="ml-2">打开录播</v-btn>
                        </div>
                      </v-card>
                    </div>
                  </div>
                </div>
              </div>
              <div class="d-flex align-center justify-center mt-4">
                <v-pagination v-model="page" :length="maxPage" total-visible="7" @update:modelValue="loadMatches" />
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
            <v-icon class="mr-2" color="primary" icon="account_tree" />
            赛制配置与自动生成
          </v-card-title>
          <v-card-text>
            <v-row dense>
              <v-col cols="12" md="4">
                <v-select
                  v-model="tournamentFormat"
                  :items="[
                    { title: '单淘', value: 'single_elim' },
                    { title: '双淘', value: 'double_elim' },
                    { title: '瑞士轮', value: 'swiss' },
                    { title: '小组赛', value: 'groups' }
                  ]"
                  label="赛制"
                  prepend-inner-icon="sports_martial_arts"
                />
              </v-col>
              <v-col cols="12" md="4">
                <v-select v-model.number="tournamentBestOf" :items="[1,3,5,7]" label="BO 局数" prepend-inner-icon="grid_on" />
              </v-col>
              <v-col cols="12" md="4" v-if="tournamentFormat === 'swiss'">
                <v-text-field v-model.number="swissRounds" label="轮次" type="number" min="1" prepend-inner-icon="format_list_numbered" />
              </v-col>
              <v-col cols="12" md="4" v-if="tournamentFormat === 'groups'">
                <v-text-field v-model.number="groupsCount" label="小组数量" type="number" min="1" prepend-inner-icon="group" />
              </v-col>
              <v-col cols="12" md="4" v-if="tournamentFormat === 'groups'">
                <v-text-field v-model.number="advancePerGroup" label="每组出线数" type="number" min="1" prepend-inner-icon="emoji_events" />
              </v-col>
            </v-row>
            <v-divider class="my-4" />
            <v-row dense>
              <v-col cols="12" md="6">
                <v-text-field v-model.number="scheduleIntervalMinutes" label="间隔（分钟）" type="number" min="1" prepend-inner-icon="timer" />
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
            </v-row>
            <v-alert v-if="conflictError" type="error" :text="conflictError" class="mt-2" />
            <div v-if="conflictItems.length" class="mt-4">
              <div class="text-subtitle-2 mb-2">检测到冲突</div>
              <v-table density="comfortable">
                <thead>
                  <tr>
                    <th class="text-left">队伍</th>
                    <th class="text-left">起始</th>
                    <th class="text-left">结束</th>
                    <th class="text-left">涉及场次</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(c, i) in conflictItems" :key="i">
                    <td>{{ teamNameById[c.teamId || c.TeamId] || (c.teamId || c.TeamId) }}</td>
                    <td>{{ new Date(c.start || c.Start).toLocaleString() }}</td>
                    <td>{{ new Date(c.end || c.End).toLocaleString() }}</td>
                    <td>{{ (c.matchIds || c.MatchIds || []).length }}</td>
                  </tr>
                </tbody>
              </v-table>
            </div>
          </v-card-text>
          <v-card-actions>
            <v-btn :loading="savingTournamentConfig" variant="tonal" prepend-icon="save" color="secondary" @click="onSaveTournamentConfig">保存配置</v-btn>
            <v-spacer />
            <v-btn :loading="generatingTournament" color="primary" prepend-icon="account_tree" @click="onGenerateTournamentSchedule">生成赛程</v-btn>
            <v-btn :loading="generatingNextRound" class="ml-2" color="primary" prepend-icon="forward" @click="onGenerateNextRound">添加下一轮</v-btn>
            <v-btn :loading="loadingConflicts" class="ml-2" color="orange" prepend-icon="warning" @click="onCheckConflicts">冲突检测</v-btn>
            <v-btn v-if="canManageEvent" :loading="generatingTestTeams" class="ml-2" color="secondary" prepend-icon="handshake" @click="onGenerateTestTeams">生成64个测试队伍</v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>

    <v-row v-if="tournamentFormat === 'groups' && groupStageGroups.length" dense>
      <v-col cols="12">
        <v-card>
          <v-card-title class="d-flex align-center">
            <v-icon class="mr-2" color="primary" icon="groups" />
            小组信息与积分
          </v-card-title>
          <v-card-text>
            <div class="text-subtitle-2 mb-2">分组</div>
            <v-row>
              <v-col v-for="g in groupStageGroups" :key="g.index" cols="12" md="6" lg="4">
                <v-sheet class="pa-3" variant="tonal">
                  <div class="d-flex align-center mb-2">
                    <v-chip color="primary" label>组 {{ g.label ?? (g.index < 26 ? String.fromCharCode('A'.charCodeAt(0) + (g.index ?? 0)) : `G${(g.index ?? 0) + 1}`) }}</v-chip>
                    <v-spacer />
                    <span class="text-caption text-medium-emphasis">队伍数：{{ (g.teamIds || []).length }}</span>
                  </div>
                  <div class="text-body-2">
                    {{ (g.teamNames && g.teamNames.length ? g.teamNames : (g.teamIds || []).map(id => teamNameById[id] || id)).join('、') }}
                  </div>
                </v-sheet>
              </v-col>
            </v-row>
            <v-divider class="my-4" v-if="tournamentFormat === 'groups' && groupStageStandings.length" />
            <template v-if="tournamentFormat === 'groups' && groupStageStandings.length">
              <div class="text-subtitle-2 mb-2">积分榜</div>
              <v-row>
                <v-col v-for="st in groupStageStandings" :key="st.groupIndex" cols="12" md="6" lg="4">
                  <v-table density="comfortable">
                    <thead>
                      <tr><th class="text-left">队伍</th><th class="text-right">积分</th></tr>
                    </thead>
                    <tbody>
                      <tr v-for="(it, idx) in st.items" :key="it.teamId" :class="{ 'top-advance': idx < (advancePerGroup || 0) }">
                        <td>{{ teamNameById[it.teamId] || it.teamId }}</td>
                        <td class="text-right">
                          <span>{{ it.points }}</span>
                          <v-icon v-if="idx < (advancePerGroup || 0)" size="18" color="orange" class="ml-1" icon="emoji_events" />
                        </td>
                      </tr>
                    </tbody>
                  </v-table>
                </v-col>
              </v-row>
            </template>
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
                    <v-avatar size="56" class="mr-2" color="primary" variant="tonal">
                      <template v-if="teamDetails[item.homeId]?.logoUrl || teamDetails[item.homeId]?.LogoUrl">
                        <v-img :src="teamDetails[item.homeId].logoUrl || teamDetails[item.homeId].LogoUrl" cover />
                      </template>
                      <template v-else>
                        <span class="text-subtitle-2">{{ getAvatarLetter(item.homeName) }}</span>
                      </template>
                    </v-avatar>
                    <v-icon class="mr-2" icon="sports_martial_arts" color="primary" />
                    <v-avatar size="56" class="mr-2" color="secondary" variant="tonal">
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

    <!-- 生成仪式弹窗 -->
    <v-dialog v-model="ceremonyOpen" max-width="960">
      <v-card theme="dark">
        <v-card-title class="d-flex align-center">
          <v-icon class="mr-2" icon="celebration" /> 赛程生成仪式
          <v-spacer />
          <v-chip color="default" variant="tonal">{{ ceremonyIndex + 1 }} / {{ ceremonyItems.length }}</v-chip>
        </v-card-title>
        <v-card-text>
          <v-sheet rounded="lg" class="ceremony-sheet">
            <v-fade-transition>
              <div v-if="currentCeremonyItem" :key="ceremonyIndex" class="d-flex align-center">
                <v-avatar size="120" class="mr-3">
                  <template v-if="teamDetails[currentCeremonyItem.homeId]?.logoUrl || teamDetails[currentCeremonyItem.homeId]?.LogoUrl">
                    <v-img :src="teamDetails[currentCeremonyItem.homeId].logoUrl || teamDetails[currentCeremonyItem.homeId].LogoUrl" cover />
                  </template>
                  <template v-else>
                    <span class="text-subtitle-1">{{ getAvatarLetter(currentCeremonyItem.homeName) }}</span>
                  </template>
                </v-avatar>
                <v-chip label variant="outlined" class="mx-3 font-weight-bold">VS</v-chip>
                <v-avatar size="120" class="mr-3">
                  <template v-if="teamDetails[currentCeremonyItem.awayId]?.logoUrl || teamDetails[currentCeremonyItem.awayId]?.LogoUrl">
                    <v-img :src="teamDetails[currentCeremonyItem.awayId].logoUrl || teamDetails[currentCeremonyItem.awayId].LogoUrl" cover />
                  </template>
                  <template v-else>
                    <span class="text-subtitle-1">{{ getAvatarLetter(currentCeremonyItem.awayName) }}</span>
                  </template>
                </v-avatar>
                <div class="text-subtitle-1 font-weight-bold">{{ currentCeremonyItem.homeName }} VS {{ currentCeremonyItem.awayName }}</div>
                <v-spacer />
                <div class="text-medium-emphasis ml-2">{{ new Date(currentCeremonyItem.matchTime).toLocaleString() }}</div>
              </div>
            </v-fade-transition>
            <div v-if="ceremonyCelebrating" class="ceremony-confetti">
              <lottie-player src="/animations/Ribbon.json" autoplay loop style="width:200px;height:200px"></lottie-player>
            </div>
          </v-sheet>
          <v-progress-linear :model-value="ceremonyProgress" color="primary" height="6" rounded class="mt-4" />
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn color="primary" variant="tonal" prepend-icon="check_circle" @click="ceremonyOpen = false;">
            完成
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    
    <v-dialog v-model="groupRevealOpen" fullscreen scrollable>
      <v-card>
        <v-card-title class="d-flex align-center">
          <v-icon class="mr-2" icon="movie" /> 小组赛分组展示
          <v-spacer />
          <v-btn color="warning" variant="tonal" prepend-icon="pause" class="mr-2" :disabled="!groupRevealPlaying" @click="stopGroupReveal">暂停</v-btn>
          <v-btn color="default" variant="tonal" prepend-icon="restart_alt" class="mr-2" @click="resetGroupReveal">重置</v-btn>
          <v-btn color="error" variant="tonal" prepend-icon="close" @click="() => { stopGroupReveal(); groupRevealOpen = false; const r = groupRevealResolver; groupRevealResolver = null; if (r) r() }">关闭</v-btn>
        </v-card-title>
        <v-card-text>
          <div class="reveal-grid">
            <div v-for="col in groupRevealColumnsData" :key="col.label" class="reveal-card">
              <div class="reveal-header">
                <v-icon class="mr-2" icon="diversity_3" />
                <span>组 {{ col.label }}</span>
              </div>
              <div class="reveal-body">
                <div v-for="item in (groupRevealDisplayed[col.label] || [])" :key="item.key" class="reveal-item pop-item">
                  <span class="text-body-1">{{ item.name }}</span>
                </div>
              </div>
            </div>
          </div>
        </v-card-text>
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
            <v-col cols="12" md="6"><v-text-field v-model="editReplayLink" label="录播链接" prepend-inner-icon="movie" /></v-col>
            <v-col cols="12" md="6"><v-text-field v-model="editStage" label="阶段" prepend-inner-icon="flag" /></v-col>
            <v-col cols="12">
              <v-radio-group v-model="editWinnerTeamId">
                <template #label>胜者</template>
                <v-radio :label="editTarget?.homeTeamName || editTarget?.HomeTeamName" :value="editTarget?.homeTeamId || editTarget?.HomeTeamId" />
                <v-radio :label="editTarget?.awayTeamName || editTarget?.AwayTeamName" :value="editTarget?.awayTeamId || editTarget?.AwayTeamId" />
                <v-radio label="未设置" :value="null" />
              </v-radio-group>
            </v-col>
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

    <v-dialog v-model="editScoreOpen" max-width="720">
      <v-card>
        <v-card-title>编辑赛果比分</v-card-title>
        <v-card-text>
          <v-row dense>
            <v-col cols="12" md="6">
              <v-select v-model.number="editScoreBestOf" :items="[1,3,5,7]" label="局数" prepend-inner-icon="filter_5" />
            </v-col>
            <v-col cols="12">
              <v-btn variant="tonal" prepend-icon="add" class="mr-2" @click="editScoreGames.push({ home: 0, away: 0 })">添加一局</v-btn>
              <v-btn variant="tonal" prepend-icon="remove" @click="editScoreGames.splice(editScoreGames.length - 1, 1)" :disabled="!editScoreGames.length">删除末尾一局</v-btn>
            </v-col>
          </v-row>
          <v-table density="comfortable" class="mt-2">
            <thead>
              <tr>
                <th class="text-left" style="width: 160px"></th>
                <th v-for="(g, idx) in editScoreGames" :key="'eh-' + idx" class="text-center">Game {{ idx + 1 }}</th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td class="text-left">{{ editScoreTarget?.homeTeamName || editScoreTarget?.HomeTeamName }}</td>
                <td v-for="(g, idx) in editScoreGames" :key="'ehv-' + idx" class="text-center">
                  <v-text-field v-model.number="g.home" type="number" min="0" hide-details density="compact" />
                </td>
              </tr>
              <tr>
                <td class="text-left">{{ editScoreTarget?.awayTeamName || editScoreTarget?.AwayTeamName }}</td>
                <td v-for="(g, idx) in editScoreGames" :key="'eav-' + idx" class="text-center">
                  <v-text-field v-model.number="g.away" type="number" min="0" hide-details density="compact" />
                </td>
              </tr>
            </tbody>
          </v-table>
        </v-card-text>
        <v-card-actions>
          <v-btn variant="text" @click="editScoreOpen = false">取消</v-btn>
          <v-spacer />
          <v-btn color="primary" prepend-icon="save" @click="onSaveEditScore">保存</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
  <ResultDialog v-model="showSuccess" :type="'success'" :message="successMsg" />
  <ResultDialog v-model="errorOpen" :type="'error'" :message="errorMsg" :details="errorDetails" />
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
.scoreboard {
  border: 1px solid rgba(0,0,0,0.06);
  border-radius: 12px;
  background: rgba(255,255,255,0.95);
}
.score-toggle {
  padding-top: 6px;
}

@media (max-width: 600px) {
  .timeline-track { padding-left: 16px; }
  .team-name { max-width: 140px; }
  .entry-top { padding: 8px 12px 0; }
  .entry-main { padding: 10px 12px; }
  .entry-actions { padding: 0 12px 10px; }
}

.ceremony-sheet {
  position: relative;
  padding: 2rem;
  border-radius: 12px;
  background: linear-gradient(145deg, #2c3e50, #4a0e4e);
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.3), 0 1px 8px rgba(0, 0, 0, 0.2);
  color: #fff;
  overflow: hidden;
}

.ceremony-sheet::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 1px;
  background: linear-gradient(90deg, rgba(255,255,255,0), rgba(255,255,255,0.4), rgba(255,255,255,0));
}
.ceremony-confetti { position: absolute; inset: 0; pointer-events: none; display: flex; justify-content: center; align-items: center; }

.reveal-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px }
.reveal-card { border-radius: 16px; background: white; border: 2px solid rgba(25,118,210,0.35); box-shadow: 0 4px 12px rgba(0,0,0,0.06); padding: 12px }
.reveal-header { display: flex; align-items: center; font-weight: 600; margin-bottom: 8px }
.reveal-body { min-height: 160px; display: flex; flex-direction: column; gap: 8px }
.reveal-item { display: flex; align-items: center; gap: 10px; padding: 10px 12px; border-radius: 12px; background: rgba(0,0,0,0.04) }
@keyframes pop { 0% { transform: scale(0.6); opacity: 0 } 60% { transform: scale(1.08); opacity: 1 } 100% { transform: scale(1); opacity: 1 } }
.pop-item { animation: pop 0.5s ease-out }
</style>

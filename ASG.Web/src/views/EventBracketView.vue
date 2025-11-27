<script setup>
import { ref, computed, onMounted, watch, onBeforeUnmount, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import PageHero from '../components/PageHero.vue'
import { getEvent, getEventRegistrations, getBracketCanvas, saveBracketCanvas } from '../services/events'
import { getMatches, updateMatch, createMatch, deleteMatch } from '../services/matches'
import { getTeam } from '../services/teams'
import { currentUser, isAuthenticated } from '../stores/auth'

const route = useRoute()
const router = useRouter()
const eventId = computed(() => route.params.id)

const ev = ref(null)
const registrations = ref([])
const matches = ref([])
const loadingEvent = ref(false)
const loadingMatches = ref(false)
const errorMsg = ref('')
const successMsg = ref('')
const showSuccess = ref(false)

const heroTitle = computed(() => '赛事赛程图')

const canManageEvent = computed(() => {
  if (!isAuthenticated.value || !ev.value) return false
  const me = currentUser.value
  if (!me) return false
  const createdByUserId = ev.value.createdByUserId || ev.value.CreatedByUserId
  const adminIdsRaw = ev.value.adminUserIds || ev.value.AdminUserIds || []
  const adminIds = Array.isArray(adminIdsRaw) ? adminIdsRaw.map(x => String(x)) : []
  const myId = me.id || me.Id
  const myIdStr = String(myId || '')
  const roleName = me.roleName || me.RoleName
  const isCreator = !!createdByUserId && !!myIdStr && String(createdByUserId) === myIdStr
  const isAdmin = roleName === 'Admin' || roleName === 'SuperAdmin'
  const isEventAdmin = !!myIdStr && adminIds.includes(myIdStr)
  return isCreator || isAdmin || isEventAdmin
})

const editMode = ref(false)
watch(canManageEvent, v => { editMode.value = !!v })
const canEditCanvas = computed(() => canManageEvent.value && editMode.value)

const teamDetails = ref({})
const loadingTeamIds = ref(new Set())
function getAvatarLetter(name) { if (!name || typeof name !== 'string') return '?' ; return name.trim().charAt(0).toUpperCase() }
const parseCustom = m => { const s = m?.customData || m?.CustomData ; if (!s) return {} ; try { return JSON.parse(s) || {} } catch { return {} } }
const getStage = m => (m?.stage || m?.Stage || parseCustom(m).stage || '')
function normalizeStatus(status) { if (typeof status === 'string') return status ; switch (status) { case 0: return 'Pending' ; case 1: return 'Registered' ; case 2: return 'Confirmed' ; case 3: return 'Approved' ; case 4: return 'Cancelled' ; case 5: return 'Rejected' ; default: return 'Pending' } }
const eligibleTeams = computed(() => { const teams = registrations.value || [] ; return teams.filter(t => { const s = normalizeStatus(t.status) ; return s === 'Approved' || s === 'Confirmed' }) })
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

async function ensureTeamDetail(id) {
  if (!id) return
  const key = id
  if (teamDetails.value[key]) return
  if (loadingTeamIds.value.has(key)) return
  loadingTeamIds.value.add(key)
  try { const dto = await getTeam(key) ; teamDetails.value[key] = dto } finally { loadingTeamIds.value.delete(key) }
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

const getRound = m => {
  const d = parseCustom(m)
  let r = Number(d.bracketRound)
  if (!Number.isFinite(r) || r <= 0) {
    const s = String(d.stage || '').toLowerCase()
    const mres = s.match(/round\s*(\d+)/)
    if (mres && Number.isFinite(Number(mres[1]))) r = Number(mres[1])
    else if (s === 'single_elim' || s === 'double_elim') r = 1
    else r = 0
  }
  return r
}
const getOrder = m => {
  const d = parseCustom(m)
  let o = Number(d.bracketOrder)
  if (!Number.isFinite(o) || o <= 0) {
    const t = new Date(m.matchTime || m.MatchTime).getTime()
    o = Number.isFinite(t) ? t : 0
  }
  return o
}

function computeBracketTime(round, order) {
  const startRaw = ev.value?.competitionStartTime || ev.value?.CompetitionStartTime
  const start = startRaw ? new Date(startRaw) : new Date()
  const existing = (matches.value || []).filter(m => getRound(m) === round)
  existing.sort((a,b) => new Date(a.matchTime || a.MatchTime).getTime() - new Date(b.matchTime || b.MatchTime).getTime())
  const intervalMs = 60 * 60 * 1000
  const roundGapMs = 24 * 60 * 60 * 1000
  let scheduled
  if (existing.length > 0) {
    const last = existing[existing.length - 1]
    const lastTime = new Date(last.matchTime || last.MatchTime)
    scheduled = new Date(lastTime.getTime() + intervalMs)
  } else {
    const base = new Date(start.getTime() + Math.max(0, (round - 1)) * roundGapMs)
    scheduled = new Date(base.getTime() + Math.max(0, (order - 1)) * intervalMs)
  }
  return scheduled.toISOString()
}

const roundsKnockout = computed(() => {
  const byRound = {}
  for (const m of matches.value || []) {
    const s = String(getStage(m)).toLowerCase()
    if (s === 'groups' || s === 'swiss') continue
    const r = getRound(m)
    (byRound[r] ||= []).push(m)
  }
  const sorted = Object.keys(byRound).map(Number).sort((a,b)=>a-b)
  for (const k of sorted) { const arr = byRound[k] || [] ; arr.sort((a,b)=>getOrder(a)-getOrder(b)) ; byRound[k] = arr }
  return sorted.map(r => ({ round: r, items: byRound[r] }))
})
const roundsSwiss = computed(() => {
  const byRound = {}
  for (const m of matches.value || []) {
    const s = String(getStage(m)).toLowerCase()
    if (s !== 'swiss') continue
    const r = getRound(m)
    (byRound[r] ||= []).push(m)
  }
  const sorted = Object.keys(byRound).map(Number).sort((a,b)=>a-b)
  for (const k of sorted) { const arr = byRound[k] || [] ; arr.sort((a,b)=>getOrder(a)-getOrder(b)) ; byRound[k] = arr }
  return sorted.map(r => ({ round: r, items: byRound[r] }))
})

const hasGroupMatches = computed(() => (matches.value || []).some(m => String(getStage(m)).toLowerCase() === 'groups'))
const hasSwissMatches = computed(() => (matches.value || []).some(m => String(getStage(m)).toLowerCase() === 'swiss'))
const layoutMode = ref('knockout')
const layoutModeItems = [ { title: '淘汰赛', value: 'knockout' }, { title: '瑞士轮', value: 'swiss' }, { title: '小组赛', value: 'groups' } ]
watch(matches, () => {
  const hasKnockout = roundsKnockout.value.length > 0
  if (hasSwissMatches.value && !hasKnockout) layoutMode.value = 'swiss'
  else if (hasGroupMatches.value && !hasKnockout && !hasSwissMatches.value) layoutMode.value = 'groups'
})
watch(layoutMode, () => {
  if (!Object.keys(nodes.value || {}).length) {
    if (layoutMode.value === 'knockout') autoLayoutFromMatches(); else if (layoutMode.value === 'groups') autoLayoutGroupsToCanvas(); else autoLayoutSwissFromMatches()
  }
})

function getGroupLabel(m) {
  const d = parseCustom(m)
  const label = d.groupLabel
  if (label) return label
  const gi = Number(d.groupIndex)
  if (Number.isFinite(gi) && gi >= 0) return gi < 26 ? String.fromCharCode('A'.charCodeAt(0) + gi) : `G${gi + 1}`
  return '未标注'
}

const groupColumns = computed(() => {
  const map = {}
  for (const m of matches.value || []) {
    const s = String(getStage(m)).toLowerCase()
    if (s !== 'groups') continue
    const lab = getGroupLabel(m)
    if (!map[lab]) map[lab] = []
    map[lab].push(m)
  }
  const labels = Object.keys(map).sort((a,b)=>a.localeCompare(b))
  return labels.map(lab => ({ label: lab, items: (map[lab] || []).slice().sort((a,b)=> new Date(a.matchTime || a.MatchTime).getTime() - new Date(b.matchTime || b.MatchTime).getTime() ) }))
})

const swissMatches = computed(() => (matches.value || []).filter(m => String(getStage(m)).toLowerCase() === 'swiss'))
const swissTeams = computed(() => {
  const s = new Set()
  for (const m of swissMatches.value) {
    const hid = String(m.homeTeamId || m.HomeTeamId)
    const aid = String(m.awayTeamId || m.AwayTeamId)
    if (hid) s.add(hid)
    if (aid) s.add(aid)
  }
  return Array.from(s)
})
const swissPrevPairs = computed(() => {
  const s = new Set()
  for (const m of swissMatches.value) {
    const a = String(m.homeTeamId || m.HomeTeamId)
    const b = String(m.awayTeamId || m.AwayTeamId)
    if (!a || !b) continue
    const k = a < b ? (a + '-' + b) : (b + '-' + a)
    s.add(k)
  }
  return s
})
const swissUniquePairs = computed(() => swissPrevPairs.value.size)
const swissTotalMatches = computed(() => swissMatches.value.length)
const swissDuplicatePairs = computed(() => Math.max(0, swissTotalMatches.value - swissUniquePairs.value))
const swissWins = computed(() => {
  const w = {}
  for (const id of swissTeams.value) w[id] = 0
  for (const m of swissMatches.value) {
    const d = parseCustom(m)
    const wid = String(d.winnerTeamId || m.winnerTeamId || m.WinnerTeamId || '')
    const hid = String(m.homeTeamId || m.HomeTeamId)
    const aid = String(m.awayTeamId || m.AwayTeamId)
    if (hid && !(hid in w)) w[hid] = 0
    if (aid && !(aid in w)) w[aid] = 0
    if (wid && (wid === hid || wid === aid)) w[wid] = (w[wid] || 0) + 1
  }
  return w
})
const swissGroups = computed(() => {
  const g = {}
  for (const id of swissTeams.value) {
    const wins = Number(swissWins.value[id] || 0)
    if (!(wins in g)) g[wins] = []
    g[wins].push(id)
  }
  return g
})
const swissOddGroups = computed(() => Object.entries(swissGroups.value).filter(([k, arr]) => (arr.length % 2) === 1).map(([k]) => Number(k)))
const swissCurrentRound = computed(() => {
  let max = 0
  for (const m of swissMatches.value) {
    const r = getRound(m)
    if (Number.isFinite(r) && r > max) max = r
  }
  return max
})
const swissNextRound = computed(() => Math.max(1, swissCurrentRound.value + 1))
const swissGroupRows = computed(() => {
  const rows = []
  const keys = Object.keys(swissGroups.value).map(Number).sort((a, b) => b - a)
  for (const k of keys) {
    const ids = swissGroups.value[k] || []
    const names = ids.map(id => teamNameById.value[id] || ('#' + id))
    rows.push({ wins: k, count: ids.length, names })
  }
  return rows
})


const draggingId = ref(null)
function onDragStart(m) { draggingId.value = m.id || m.Id }
function onDragEnd() { draggingId.value = null }

async function dropToRound(round) {
  if (!editMode.value || !draggingId.value) return
  const m = (matches.value || []).find(x => (x.id || x.Id) === draggingId.value)
  if (!m) return
  const current = layoutMode.value === 'swiss' ? roundsSwiss.value.find(r => r.round === round) : roundsKnockout.value.find(r => r.round === round)
  const order = (current?.items?.length || 0) + 1
  await updateMatchBracket(m, round, order)
  successMsg.value = '已更新赛程图位置'
  await loadMatches()
}

async function updateMatchBracket(m, round, order) {
  const d = parseCustom(m)
  d.bracketRound = round
  d.bracketOrder = order
  const payload = { customData: JSON.stringify(d) }
  await updateMatch(m.id || m.Id, payload)
}

async function loadEvent() {
  loadingEvent.value = true
  try { ev.value = await getEvent(eventId.value) } finally { loadingEvent.value = false }
}

async function loadRegistrations() {
  try { registrations.value = await getEventRegistrations(eventId.value) }
  catch (e) { errorMsg.value = e?.payload?.message || e?.message || '加载报名队伍失败'; registrations.value = [] }
}

async function loadMatches() {
  loadingMatches.value = true
  try {
    const list = await getMatches({ eventId: eventId.value, page: 1, pageSize: 500 })
    matches.value = list
    const ids = new Set()
    for (const m of matches.value || []) { ids.add(m.homeTeamId || m.HomeTeamId); ids.add(m.awayTeamId || m.AwayTeamId) }
    await Promise.all(Array.from(ids).map(id => ensureTeamDetail(id)))
    refreshWinnerNodes()
    if (!Object.keys(nodes.value || {}).length) {
      if (layoutMode.value === 'knockout') autoLayoutFromMatches(); else if (layoutMode.value === 'groups') autoLayoutGroupsToCanvas(); else autoLayoutSwissFromMatches()
    }
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '加载赛程失败'
    matches.value = []
  } finally { loadingMatches.value = false }
}

function goBackDetail() { router.push({ name: 'event-detail', params: { id: eventId.value } }) }
function goSchedule() { router.push({ name: 'event-schedule', params: { id: eventId.value } }) }

onMounted(async () => { await loadEvent(); await loadCanvas(); await Promise.all([loadRegistrations(), loadMatches()]) })

const draggingTeamId = ref(null)
function onTeamDragStart(t) { if (!canEditCanvas.value) return; draggingTeamId.value = t.teamId || t.TeamId }
function onTeamDragEnd() { draggingTeamId.value = null }
const drafts = ref({})
function addDraft(round) { const id = Math.random().toString(36).slice(2) ; const arr = drafts.value[round] || [] ; arr.push({ id, homeId: null, awayId: null }) ; drafts.value[round] = arr }
function dropToSlot(round, draftId, slot) { if (!editMode.value || !draggingTeamId.value) return ; const arr = drafts.value[round] || [] ; const d = arr.find(x => x.id === draftId) ; if (!d) return ; if (slot === 'home') d.homeId = draggingTeamId.value ; else d.awayId = draggingTeamId.value }
function removeDraft(round, draftId) { const arr = drafts.value[round] || [] ; drafts.value[round] = arr.filter(x => x.id !== draftId) }
async function saveDraft(round, draft) {
  if (!draft.homeId || !draft.awayId) { errorMsg.value = '请先拖入两支队伍' ; return }
  const eventGuid = ev.value?.id || ev.value?.Id
  const selectedRound = Number(draft.round || round || 1)
  const order = ((matches.value || []).filter(m => getRound(m) === selectedRound).length) + 1
  const timeIso = computeBracketTime(selectedRound, order)
  const d = { bracketRound: selectedRound, bracketOrder: order, stage: `Round ${selectedRound}` }
  const payload = { homeTeamId: draft.homeId, awayTeamId: draft.awayId, matchTime: timeIso, eventId: eventGuid, customData: JSON.stringify(d) }
  try {
    const created = await createMatch(payload)
    successMsg.value = '已创建比赛'
    const r = selectedRound
    importDraftTeamsToCanvas(draft.homeId, draft.awayId, r)
    addWinnerNodeAndEdges(String(draft.homeId), String(draft.awayId), created?.id || created?.Id, r)
    removeDraft(round, draft.id)
    saveCanvas()
    await loadMatches()
  } catch (e) { errorMsg.value = e?.payload?.message || e?.message || '创建比赛失败' }
}
async function autoGenerateFirstRound() {
  if (!canManageEvent.value) { errorMsg.value = '没有权限' ; return }
  const baseTeams = eligibleTeams.value || []
  const teamIds = baseTeams.map(t => t.teamId || t.TeamId)
  if (teamIds.length < 2) { errorMsg.value = '可用于生成的队伍不足' ; return }
  const shuffled = [...teamIds].sort(() => Math.random() - 0.5)
  const pairs = []
  for (let i = 0; i < shuffled.length; i += 2) { const a = shuffled[i], b = shuffled[i+1] ; if (b) pairs.push([a,b]) }
  const eventGuid = ev.value?.id || ev.value?.Id
  for (let i = 0; i < pairs.length; i++) {
    const [homeId, awayId] = pairs[i]
    const order = i + 1
    const timeIso = computeBracketTime(1, order)
    const d = { bracketRound: 1, bracketOrder: order, stage: 'Round 1' }
    const payload = { homeTeamId: homeId, awayTeamId: awayId, matchTime: timeIso, eventId: eventGuid, customData: JSON.stringify(d) }
    await createMatch(payload)
  }
  successMsg.value = `已生成 ${pairs.length} 场首轮比赛`
  await loadMatches()
}

const selectedTeamIds = ref(new Set())
const nodes = ref({})
const edges = ref([])
const activeNodeIds = ref(new Set())
const draggingNodeId = ref(null)
const dragOffset = ref({ x: 0, y: 0 })
const selectedRoundToClear = ref(null)
const nodeRounds = computed(() => {
  const set = new Set()
  for (const id of Object.keys(nodes.value)) {
    const r = nodes.value[id]?.round
    if (Number.isFinite(r)) set.add(r)
  }
  return Array.from(set).sort((a,b)=>a-b)
})

const scale = ref(1)
const pan = ref({ x: 0, y: 0 })
const minScale = 0.5
const maxScale = 2
const canvasTransformStyle = computed(() => ({ transform: `translate(${pan.value.x}px, ${pan.value.y}px) scale(${scale.value})`, transformOrigin: '0 0' }))

function storageKey() { return `bracket-canvas:${String(eventId.value)}` }
async function saveCanvas() {
  const obj = { nodes: nodes.value, edges: edges.value, selected: Array.from(selectedTeamIds.value), view: { scale: scale.value, pan: pan.value } }
  localStorage.setItem(storageKey(), JSON.stringify(obj))
  try {
    if (canManageEvent.value) await saveBracketCanvas(eventId.value, obj)
  } catch (e) { /* ignore */ }
}
async function loadCanvas() {
  try {
    const serverObj = await getBracketCanvas(eventId.value)
    if (serverObj && (serverObj.nodes || serverObj.edges)) {
      nodes.value = serverObj.nodes || {}
      edges.value = serverObj.edges || []
      selectedTeamIds.value = new Set(serverObj.selected || [])
      if (serverObj.view) {
        const v = serverObj.view || {}
        const sc = Number(v.scale)
        if (Number.isFinite(sc) && sc >= minScale && sc <= maxScale) scale.value = sc
        if (v.pan && typeof v.pan === 'object') pan.value = { x: Number(v.pan.x) || 0, y: Number(v.pan.y) || 0 }
      }
      return
    }
  } catch {}
  try {
    const s = localStorage.getItem(storageKey())
    if (!s) return
    const obj = JSON.parse(s)
    nodes.value = obj.nodes || {}
    edges.value = obj.edges || []
    selectedTeamIds.value = new Set(obj.selected || [])
    if (obj.view) {
      const v = obj.view || {}
      const sc = Number(v.scale)
      if (Number.isFinite(sc) && sc >= minScale && sc <= maxScale) scale.value = sc
      if (v.pan && typeof v.pan === 'object') pan.value = { x: Number(v.pan.x) || 0, y: Number(v.pan.y) || 0 }
    }
  } catch {}
}

function toggleSelectTeam(t) {
  if (!canEditCanvas.value) return
  const id = t.teamId || t.TeamId
  if (!id) return
  if (selectedTeamIds.value.has(id)) { selectedTeamIds.value.delete(id); delete nodes.value[id]; edges.value = edges.value.filter(e => e.fromId !== id && e.toId !== id) }
  else {
    selectedTeamIds.value.add(id)
    const canvas = document.querySelector('#bracket-canvas')
    const rect = canvas?.getBoundingClientRect()
    const cx = Math.max(16, Math.floor((rect?.width || 600) / 2))
    const cy = Math.max(16, 32)
    nodes.value[id] = { x: cx, y: cy, round: 1, type: 'team', refId: id }
  }
  saveCanvas()
}

function onNodePointerDown(id, e) {
  if (!canEditCanvas.value) return
  const n = nodes.value[id]
  if (!n) return
  draggingNodeId.value = id
  const target = e.currentTarget
  const box = target.getBoundingClientRect()
  dragOffset.value = { x: (e.clientX - box.left) / scale.value, y: (e.clientY - box.top) / scale.value }
  window.addEventListener('pointermove', onGlobalPointerMove)
  window.addEventListener('pointerup', onGlobalPointerUp)
}

function onGlobalPointerMove(e) {
  if (!draggingNodeId.value) return
  const canvas = document.querySelector('#bracket-canvas')
  const rect = canvas?.getBoundingClientRect()
  const x = (e.clientX - (rect?.left || 0) - pan.value.x) / scale.value - dragOffset.value.x
  const y = (e.clientY - (rect?.top || 0) - pan.value.y) / scale.value - dragOffset.value.y
  const id = draggingNodeId.value
  const n = nodes.value[id]
  if (!n) return
  const cw = (rect?.width || 800) / scale.value
  const ch = (rect?.height || 520) / scale.value
  n.x = Math.max(0, Math.min(Math.floor(x), cw - 160))
  n.y = Math.max(0, Math.min(Math.floor(y), ch - 72))
}

function onGlobalPointerUp() {
  if (!draggingNodeId.value) return
  draggingNodeId.value = null
  window.removeEventListener('pointermove', onGlobalPointerMove)
  window.removeEventListener('pointerup', onGlobalPointerUp)
  saveCanvas()
}

function setNodeRound(id, dir) {
  if (!canEditCanvas.value) return
  const n = nodes.value[id]
  if (!n) return
  const r = (n.round || 1) + dir
  n.round = Math.max(1, Math.min(16, r))
  saveCanvas()
}

function toggleActiveNode(id) {
  if (!canEditCanvas.value) return
  if (activeNodeIds.value.has(id)) activeNodeIds.value.delete(id)
  else {
    if (activeNodeIds.value.size >= 2) activeNodeIds.value.clear()
    activeNodeIds.value.add(id)
  }
}

function nodeWidth() { return 160 }
function nodeHeight() { return 56 }
const roundsItems = computed(() => Array.from({ length: 16 }, (_, i) => i + 1))

function teamIdForNode(id) {
  const n = nodes.value[id]
  if (!n) return null
  if (n.type === 'team') return n.refId || id
  return null
}

function ensureNodeForTeam(teamId, round) {
  const key = Object.keys(nodes.value).find(k => teamIdForNode(k) === teamId)
  if (key) return key
  const canvas = document.querySelector('#bracket-canvas')
  const rect = canvas?.getBoundingClientRect()
  const baseX = 32 + Math.max(0, (round || 1) - 1) * 160
  const baseY = 32 + Object.keys(nodes.value).length * 12
  const nodeId = `${teamId}`
  const cw = (rect?.width || 800) / scale.value
  const ch = (rect?.height || 520) / scale.value
  nodes.value[nodeId] = { x: Math.min(baseX, cw - 160), y: Math.min(baseY, ch - 72), round: Math.max(1, round || 1), type: 'team', refId: teamId }
  selectedTeamIds.value.add(teamId)
  return nodeId
}

function resolveNodeToTeamId(id) {
  const n = nodes.value[id]
  if (!n) return null
  if (n.type === 'team') return n.refId || id
  if (n.type === 'winner') {
    const matchId = n.refId
    const m = (matches.value || []).find(x => (x.id || x.Id) === matchId)
    const tid = m?.winnerTeamId || m?.WinnerTeamId
    return tid || null
  }
  return null
}

function convertWinnerNodeToTeam(id) {
  const n = nodes.value[id]
  if (!n || n.type !== 'winner') return
  const tId = resolveNodeToTeamId(id)
  if (!tId) return
  n.type = 'team'
  n.refId = tId
}

function addWinnerNodeAndEdges(homeId, awayId, matchId, round) {
  const h = nodes.value[homeId]
  const a = nodes.value[awayId]
  const baseX = Math.max(h?.x || 0, a?.x || 0)
  const nx = baseX + 160
  const ny = Math.floor(((h?.y || 0) + (a?.y || 0)) / 2)
  const wId = `w:${String(matchId)}`
  nodes.value[wId] = { x: nx, y: ny, round: (round || 1) + 1, type: 'winner', refId: matchId }
  edges.value.push({ fromId: homeId, toId: wId })
  edges.value.push({ fromId: awayId, toId: wId })
}

async function createMatchFromActiveNodes() {
  if (!canManageEvent.value) { errorMsg.value = '没有权限' ; return }
  const ids = Array.from(activeNodeIds.value)
  if (ids.length !== 2) { errorMsg.value = '请选择两支队伍或胜者占位' ; return }
  const tHome = resolveNodeToTeamId(ids[0])
  const tAway = resolveNodeToTeamId(ids[1])
  if (!tHome || !tAway) { errorMsg.value = '胜者尚未产生，无法创建下一场' ; return }
  const homeId = tHome
  const awayId = tAway
  // 将胜者占位自动转换为队伍节点
  convertWinnerNodeToTeam(ids[0])
  convertWinnerNodeToTeam(ids[1])
  const r = Math.max(nodes.value[ids[0]]?.round || 1, nodes.value[ids[1]]?.round || 1)
  const eventGuid = ev.value?.id || ev.value?.Id
  const order = ((matches.value || []).filter(m => getRound(m) === r).length) + 1
  const timeIso = computeBracketTime(r, order)
  const d = { bracketRound: r, bracketOrder: order, stage: `Round ${r}` }
  const isGuid = s => typeof s === 'string' && /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(s)
  if (!isGuid(homeId) || !isGuid(awayId) || !isGuid(String(eventGuid))) { errorMsg.value = '参数无效：队伍或赛事ID不正确' ; return }
  const payload = { homeTeamId: homeId, awayTeamId: awayId, matchTime: timeIso, eventId: eventGuid, customData: JSON.stringify(d) }
  try {
    const created = await createMatch(payload)
    successMsg.value = '已创建比赛'
    addWinnerNodeAndEdges(ids[0], ids[1], created?.id || created?.Id, r)
    activeNodeIds.value.clear()
    saveCanvas()
    await loadMatches()
  } catch (e) { errorMsg.value = e?.payload?.message || e?.message || '创建比赛失败' }
}

function getWinnerLabel(node) {
  if (!node) return ''
  if (node.type === 'team') {
    const tid = node.refId
    return teamNameById.value[tid] || ('#' + tid)
  }
  const matchId = node.refId
  const m = (matches.value || []).find(x => (x.id || x.Id) === matchId)
  const name = getWinnerName(m)
  if (name) return name
  const tid = m?.winnerTeamId || m?.WinnerTeamId
  return tid ? (teamNameById.value[tid] || ('#' + tid)) : '胜者'
}

function refreshWinnerNodes() {
  let changed = false
  const ids = Object.keys(nodes.value)
  for (const id of ids) {
    const n = nodes.value[id]
    if (n?.type === 'winner') {
      const tId = resolveNodeToTeamId(id)
      if (tId) { convertWinnerNodeToTeam(id); changed = true }
    }
  }
  if (changed) saveCanvas()
}

function clearCanvas() {
  if (!canManageEvent.value) { errorMsg.value = '没有权限' ; return }
  nodes.value = {}
  edges.value = []
  selectedTeamIds.value.clear()
  activeNodeIds.value.clear()
  saveCanvas()
}

function autoLayoutFromMatches() {
  const byRound = {}
  for (const m of matches.value || []) {
    const r = getRound(m)
    if (!Number.isFinite(r) || r <= 0) continue
    (byRound[r] ||= []).push(m)
  }
  const roundsSorted = Object.keys(byRound).map(Number).sort((a,b)=>a-b)
  nodes.value = {}
  edges.value = []
  selectedTeamIds.value.clear()
  for (const r of roundsSorted) {
    const arr = byRound[r].slice().sort((a,b)=>getOrder(a)-getOrder(b))
    for (let i = 0; i < arr.length; i++) {
      const m = arr[i]
      const homeId = String(m.homeTeamId || m.HomeTeamId)
      const awayId = String(m.awayTeamId || m.AwayTeamId)
      const baseX = 32 + (r - 1) * 180
      const baseY = 32 + i * 120
      nodes.value[homeId] = { x: baseX, y: baseY, round: r, type: 'team', refId: homeId }
      nodes.value[awayId] = { x: baseX, y: baseY + 64, round: r, type: 'team', refId: awayId }
      selectedTeamIds.value.add(homeId)
      selectedTeamIds.value.add(awayId)
      const midIdH = homeId
      const midIdA = awayId
      addWinnerNodeAndEdges(midIdH, midIdA, (m.id || m.Id), r)
    }
  }
  refreshWinnerNodes()
  saveCanvas()
}

function autoLayoutGroupsToCanvas() {
  const map = {}
  for (const m of matches.value || []) {
    const s = String(getStage(m)).toLowerCase()
    if (s !== 'groups') continue
    const d = parseCustom(m)
    const lab = d.groupLabel || (Number.isFinite(Number(d.groupIndex)) && Number(d.groupIndex) >= 0 ? (Number(d.groupIndex) < 26 ? String.fromCharCode('A'.charCodeAt(0) + Number(d.groupIndex)) : `G${Number(d.groupIndex) + 1}`) : '未标注')
    if (!map[lab]) map[lab] = { teams: new Set(), matches: [] }
    const hid = String(m.homeTeamId || m.HomeTeamId)
    const aid = String(m.awayTeamId || m.AwayTeamId)
    if (hid) map[lab].teams.add(hid)
    if (aid) map[lab].teams.add(aid)
    map[lab].matches.push(m)
  }
  const labels = Object.keys(map).sort((a,b)=>a.localeCompare(b))
  nodes.value = {}
  edges.value = []
  selectedTeamIds.value.clear()
  let col = 0
  for (const lab of labels) {
    const colX = 32 + col * 240
    const teams = Array.from(map[lab].teams)
    let row = 0
    for (const tid of teams) {
      const y = 32 + row * 80
      nodes.value[tid] = { x: colX, y: y, round: 1, type: 'team', refId: tid }
      selectedTeamIds.value.add(tid)
      row++
    }
    const ms = map[lab].matches.slice().sort((a,b)=> new Date(a.matchTime || a.MatchTime).getTime() - new Date(b.matchTime || b.MatchTime).getTime())
    for (const m of ms) {
      const hid = String(m.homeTeamId || m.HomeTeamId)
      const aid = String(m.awayTeamId || m.AwayTeamId)
      if (hid && aid) edges.value.push({ fromId: hid, toId: aid })
    }
    col++
  }
  saveCanvas()
}

function autoLayoutSwissFromMatches() {
  const byRound = {}
  for (const m of matches.value || []) {
    const s = String(getStage(m)).toLowerCase()
    if (s !== 'swiss') continue
    const r = getRound(m)
    if (!Number.isFinite(r) || r <= 0) continue
    (byRound[r] ||= []).push(m)
  }
  const roundsSorted = Object.keys(byRound).map(Number).sort((a,b)=>a-b)
  nodes.value = {}
  edges.value = []
  selectedTeamIds.value.clear()
  for (const r of roundsSorted) {
    const arr = byRound[r].slice().sort((a,b)=>getOrder(a)-getOrder(b))
    for (let i = 0; i < arr.length; i++) {
      const m = arr[i]
      const homeId = String(m.homeTeamId || m.HomeTeamId)
      const awayId = String(m.awayTeamId || m.AwayTeamId)
      const baseX = 32 + (r - 1) * 180
      const baseY = 32 + i * 120
      nodes.value[homeId] = { x: baseX, y: baseY, round: r, type: 'team', refId: homeId }
      nodes.value[awayId] = { x: baseX, y: baseY + 64, round: r, type: 'team', refId: awayId }
      selectedTeamIds.value.add(homeId)
      selectedTeamIds.value.add(awayId)
      edges.value.push({ fromId: homeId, toId: awayId })
    }
  }
  saveCanvas()
}

function deleteActiveNodes() {
  if (!canManageEvent.value) { errorMsg.value = '没有权限' ; return }
  const ids = Array.from(activeNodeIds.value)
  if (!ids.length) return
  for (const id of ids) {
    delete nodes.value[id]
    selectedTeamIds.value.delete(id)
  }
  edges.value = edges.value.filter(e => !activeNodeIds.value.has(e.fromId) && !activeNodeIds.value.has(e.toId))
  activeNodeIds.value.clear()
  saveCanvas()
}

function clearCanvasRound(round) {
  if (!canManageEvent.value) { errorMsg.value = '没有权限' ; return }
  if (!Number.isFinite(round)) return
  const ids = Object.keys(nodes.value).filter(id => (nodes.value[id]?.round || 0) === round)
  if (!ids.length) return
  for (const id of ids) {
    delete nodes.value[id]
    selectedTeamIds.value.delete(id)
    activeNodeIds.value.delete(id)
  }
  const set = new Set(ids)
  edges.value = edges.value.filter(e => !set.has(e.fromId) && !set.has(e.toId))
  saveCanvas()
}

async function deleteMatchesOfRound(round) {
  if (!canManageEvent.value) { errorMsg.value = '没有权限' ; return }
  if (round <= 0) { errorMsg.value = '不可删除未分配' ; return }
  const list = (matches.value || []).filter(m => getRound(m) === round)
  if (!list.length) { errorMsg.value = '本轮无比赛' ; return }
  if (typeof window !== 'undefined') { const ok = window.confirm(`确认删除第 ${round} 轮的所有比赛？`) ; if (!ok) return }
  for (const m of list) { try { await deleteMatch(m.id || m.Id) } catch (e) { errorMsg.value = e?.payload?.message || e?.message || '删除比赛失败'; return } }
  const winnersToRemove = new Set(list.map(m => `w:${String(m.id || m.Id)}`))
  for (const id of winnersToRemove) { if (nodes.value[id]) delete nodes.value[id] ; activeNodeIds.value.delete(id) }
  edges.value = edges.value.filter(e => !winnersToRemove.has(e.fromId) && !winnersToRemove.has(e.toId))
  saveCanvas()
  successMsg.value = `已删除第 ${round} 轮 ${list.length} 场比赛`
  await loadMatches()
}

// 修改：草稿保存后自动导入到画布
function importDraftTeamsToCanvas(homeId, awayId, round) {
  if (!canEditCanvas.value) return { nh: null, na: null }
  const nh = ensureNodeForTeam(homeId, round)
  const na = ensureNodeForTeam(awayId, round)
  return { nh, na }
}

// 画布加载已集成到上方 onMounted
onBeforeUnmount(() => { window.removeEventListener('pointermove', onGlobalPointerMove); window.removeEventListener('pointerup', onGlobalPointerUp) })

function zoomIn() { scale.value = Math.min(maxScale, scale.value + 0.1); saveCanvas() }
function zoomOut() { scale.value = Math.max(minScale, scale.value - 0.1); saveCanvas() }
function resetView() { scale.value = 1; pan.value = { x: 0, y: 0 }; saveCanvas() }
let panning = false
let panStart = { x: 0, y: 0 }
let panOrigin = { x: 0, y: 0 }
function onCanvasPointerDown(e) { if (e.target.closest('.canvas-node')) return; panning = true; panStart = { x: e.clientX, y: e.clientY }; panOrigin = { ...pan.value }; window.addEventListener('pointermove', onPanMove); window.addEventListener('pointerup', onPanEnd) }
function onPanMove(e) { if (!panning) return; pan.value = { x: panOrigin.x + (e.clientX - panStart.x), y: panOrigin.y + (e.clientY - panStart.y) } }
function onPanEnd() { if (!panning) return; panning = false; window.removeEventListener('pointermove', onPanMove); window.removeEventListener('pointerup', onPanEnd); saveCanvas() }
function onCanvasWheel(e) { e.preventDefault(); const dir = e.deltaY > 0 ? -0.1 : 0.1; scale.value = Math.min(maxScale, Math.max(minScale, scale.value + dir)); saveCanvas() }

async function exportCanvasImage() {
  const ids = Object.keys(nodes.value)
  if (!ids.length) return
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity
  for (const id of ids) {
    const n = nodes.value[id]
    const x = n.x || 0
    const y = n.y || 0
    minX = Math.min(minX, x)
    minY = Math.min(minY, y)
    maxX = Math.max(maxX, x + nodeWidth())
    maxY = Math.max(maxY, y + nodeHeight())
  }
  const padding = 20
  const width = Math.max(300, Math.floor(maxX - minX) + padding * 2)
  const height = Math.max(200, Math.floor(maxY - minY) + padding * 2)
  const canvas = document.createElement('canvas')
  canvas.width = width
  canvas.height = height
  const ctx = canvas.getContext('2d')
  ctx.fillStyle = '#f5f7fa'
  ctx.fillRect(0, 0, width, height)
  ctx.strokeStyle = 'rgba(0,0,0,0.3)'
  ctx.lineWidth = 2
  for (const e of edges.value) {
    const from = nodes.value[e.fromId] || { x: 0, y: 0 }
    const to = nodes.value[e.toId] || { x: 0, y: 0 }
    const fx = (from.x || 0) + (nodeWidth() / 2) - minX + padding
    const fy = (from.y || 0) + (nodeHeight() / 2) - minY + padding
    const tx = (to.x || 0) + (nodeWidth() / 2) - minX + padding
    const ty = (to.y || 0) + (nodeHeight() / 2) - minY + padding
    const mx = Math.floor((fx + tx) / 2)
    ctx.beginPath()
    ctx.moveTo(fx, fy)
    ctx.lineTo(mx, fy)
    ctx.lineTo(mx, ty)
    ctx.lineTo(tx, ty)
    ctx.stroke()
  }
  const logoCache = {}
  const toLoad = []
  for (const id of ids) {
    const n = nodes.value[id]
    const tid = n.type === 'team' ? n.refId : (n.type === 'winner' ? resolveNodeToTeamId(id) : null)
    const url = tid ? (teamDetails.value[tid]?.logoUrl || teamDetails.value[tid]?.LogoUrl || null) : null
    if (tid && url) {
      toLoad.push((async () => {
        try {
          const img = await new Promise((resolve, reject) => { const im = new Image(); im.crossOrigin = 'anonymous'; im.onload = () => resolve(im); im.onerror = reject; im.src = url });
          logoCache[tid] = img
        } catch {}
      })())
    }
  }
  if (toLoad.length) { try { await Promise.all(toLoad) } catch {} }
  for (const id of ids) {
    const n = nodes.value[id]
    const x = (n.x || 0) - minX + padding
    const y = (n.y || 0) - minY + padding
    const w = nodeWidth()
    const h = nodeHeight()
    const r = 12
    ctx.fillStyle = '#ffffff'
    ctx.strokeStyle = 'rgba(0,0,0,0.15)'
    ctx.lineWidth = 1
    ctx.beginPath()
    ctx.moveTo(x + r, y)
    ctx.lineTo(x + w - r, y)
    ctx.quadraticCurveTo(x + w, y, x + w, y + r)
    ctx.lineTo(x + w, y + h - r)
    ctx.quadraticCurveTo(x + w, y + h, x + w - r, y + h)
    ctx.lineTo(x + r, y + h)
    ctx.quadraticCurveTo(x, y + h, x, y + h - r)
    ctx.lineTo(x, y + r)
    ctx.quadraticCurveTo(x, y, x + r, y)
    ctx.closePath()
    ctx.fill()
    ctx.stroke()
    let textOffset = 12
    const tid = n.type === 'team' ? n.refId : (n.type === 'winner' ? resolveNodeToTeamId(id) : null)
    const logoImg = tid ? logoCache[tid] : null
    if (logoImg) {
      const side = 28
      const ix = x + 8
      const iy = y + (h - side) / 2
      try { ctx.drawImage(logoImg, ix, iy, side, side); textOffset = side + 16 } catch {}
    }
    ctx.fillStyle = '#333333'
    ctx.font = '14px system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif'
    ctx.textBaseline = 'middle'
    const label = n.type === 'team' ? (teamNameById.value[n.refId] || ('#' + n.refId)) : getWinnerLabel(n)
    ctx.fillText(String(label || ''), x + textOffset, y + h / 2)
  }
  const url = canvas.toDataURL('image/png')
  const a = document.createElement('a')
  a.href = url
  a.download = `bracket-${String(eventId.value)}.png`
  a.click()
}
</script>

<template>
  <PageHero :title="heroTitle" subtitle="查看与管理赛事赛程图" icon="account_tree">
    <template #actions>
      <v-btn class="mb-3" variant="text" @click="goBackDetail">
        <template #prepend><v-icon icon="chevron_left" /></template>
        返回赛事详情
      </v-btn>
      <v-btn class="mb-3 ml-2" variant="text" prepend-icon="calendar_month" @click="goSchedule">查看时间轴</v-btn>
  
    </template>
  </PageHero>

  <v-container class="py-6 page-container">
    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
    <v-alert v-if="successMsg" type="success" class="mb-4">{{ successMsg }}</v-alert>

    <v-row v-if="loadingMatches" dense>
      <v-col v-for="n in 6" :key="n" cols="12" sm="6" md="4" lg="3"><v-skeleton-loader type="card" /></v-col>
    </v-row>

    <div v-else>
      <v-card class="mb-4" rounded="lg">
        <v-card-title class="d-flex align-center">
          <v-icon class="mr-2" icon="group" /> 未分配队伍
          <v-spacer />
          <v-btn v-if="canManageEvent" color="primary" variant="tonal" prepend-icon="shuffle" @click="autoGenerateFirstRound">一键生成首轮</v-btn>
        </v-card-title>
        <v-card-text>
          <div class="team-pool">
            <div v-for="t in eligibleTeams" :key="t.teamId || t.TeamId" class="team-chip" :class="{ active: selectedTeamIds.has(t.teamId || t.TeamId) }" :draggable="canEditCanvas" @dragstart="onTeamDragStart(t)" @dragend="onTeamDragEnd" @click="toggleSelectTeam(t)">
              <v-avatar size="36" class="mr-2">
                <template v-if="teamDetails[t.teamId || t.TeamId]?.logoUrl || teamDetails[t.teamId || t.TeamId]?.LogoUrl">
                  <v-img :src="teamDetails[t.teamId || t.TeamId].logoUrl || teamDetails[t.teamId || t.TeamId].LogoUrl" cover />
                </template>
                <template v-else>
                  <span class="text-subtitle-2">{{ getAvatarLetter(t.teamName || t.TeamName) }}</span>
                </template>
              </v-avatar>
              <span class="text-body-2">{{ t.teamName || t.TeamName }}</span>
            </div>
          </div>
        </v-card-text>
      </v-card>

      <v-card class="mb-4" rounded="lg">
        <v-card-title class="d-flex align-center">
          <v-icon class="mr-2" icon="draw" /> 画布
          <v-spacer />
          <v-btn variant="text" prepend-icon="zoom_out" class="mr-1" @click="zoomOut">缩小</v-btn>
          <v-btn variant="text" prepend-icon="zoom_in" class="mr-1" @click="zoomIn">放大</v-btn>
          <v-btn variant="text" prepend-icon="restart_alt" class="mr-3" @click="resetView">重置视图</v-btn>
          <v-btn color="default" variant="tonal" prepend-icon="image" class="mr-3" @click="exportCanvasImage">导出图片</v-btn>
          <v-select v-model="layoutMode" :items="layoutModeItems" density="compact" hide-details style="max-width: 160px" class="mr-3" label="布局模式" />
          <v-btn v-if="layoutMode==='knockout'" color="primary" variant="tonal" prepend-icon="auto_awesome" class="mr-2" @click="autoLayoutFromMatches">自动布局</v-btn>
          <v-btn v-if="layoutMode==='swiss'" color="primary" variant="tonal" prepend-icon="auto_awesome" class="mr-2" @click="autoLayoutSwissFromMatches">瑞士轮自动布局</v-btn>
          <v-btn v-if="layoutMode==='groups'" color="primary" variant="tonal" prepend-icon="auto_awesome" class="mr-2" @click="autoLayoutGroupsToCanvas">生成小组画布</v-btn>
          <v-btn v-if="canManageEvent" color="primary" variant="tonal" prepend-icon="sports_kabaddi" :disabled="activeNodeIds.size !== 2" @click="createMatchFromActiveNodes">用选中两队创建比赛</v-btn>
          <v-btn v-if="canManageEvent" color="error" variant="tonal" prepend-icon="delete" class="ml-2" :disabled="activeNodeIds.size === 0" @click="deleteActiveNodes">删除选中节点</v-btn>
          <v-btn v-if="canManageEvent" color="error" variant="tonal" prepend-icon="delete_forever" class="ml-2" @click="clearCanvas">清空画布</v-btn>
        </v-card-title>
        <v-card-text>
          <v-card v-if="layoutMode==='swiss'" class="mb-4" variant="outlined">
            <v-card-title class="d-flex align-center">
              <v-icon class="mr-2" color="primary" icon="insights" />
              瑞士轮状态
            </v-card-title>
            <v-card-text>
              <div class="d-flex align-center flex-wrap mb-3" style="gap: 8px">
                <v-chip label color="primary" variant="tonal">参赛队伍 {{ swissTeams.length }}</v-chip>
                <v-chip label color="default" variant="tonal">已进行轮次 {{ swissCurrentRound }}</v-chip>
                <v-chip label color="secondary" variant="tonal">下一轮 {{ swissNextRound }}</v-chip>
                <v-chip label :color="swissDuplicatePairs > 0 ? 'error' : 'success'" variant="tonal">历史重赛 {{ swissDuplicatePairs }}</v-chip>
              </div>
              <div class="text-subtitle-2 mb-2">胜场分布</div>
              <v-table density="comfortable">
                <thead>
                  <tr>
                    <th class="text-left">胜场</th>
                    <th class="text-left">队伍数量</th>
                    <th class="text-left">队伍</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="row in swissGroupRows" :key="row.wins">
                    <td>{{ row.wins }}</td>
                    <td>
                      <v-chip size="small" label :color="(swissOddGroups.includes(row.wins)) ? 'warning' : 'primary'" variant="tonal">{{ row.count }}</v-chip>
                    </td>
                    <td>
                      <span class="text-body-2">{{ row.names.join('、') }}</span>
                    </td>
                  </tr>
                </tbody>
              </v-table>
              <v-alert v-if="swissOddGroups.length" type="warning" class="mt-3" text="存在奇数队伍组，末位队伍将下调至下一组以完成配对" />
              <v-alert v-else type="success" class="mt-3" text="所有分组人数为偶数，可直接进行下一轮配对" />
            </v-card-text>
          </v-card>
          <div id="bracket-canvas" class="canvas" @pointerdown="onCanvasPointerDown" @wheel.prevent="onCanvasWheel">
            <div class="canvas-content" :style="canvasTransformStyle">
              <svg class="edges" xmlns="http://www.w3.org/2000/svg">
                <template v-for="(e, idx) in edges" :key="idx">
                  <path :d="(() => {
                    const from = nodes[e.fromId] || { x: 0, y: 0 }
                    const to = nodes[e.toId] || { x: 0, y: 0 }
                    const fx = (from.x || 0) + (nodeWidth() / 2)
                    const fy = (from.y || 0) + (nodeHeight() / 2)
                    const tx = (to.x || 0) + (nodeWidth() / 2)
                    const ty = (to.y || 0) + (nodeHeight() / 2)
                    const mx = Math.floor((fx + tx) / 2)
                    return `M ${fx} ${fy} L ${mx} ${fy} L ${mx} ${ty} L ${tx} ${ty}`
                  })()" class="edge-path" />
                </template>
              </svg>
              <div v-for="id in Object.keys(nodes)" :key="id" class="canvas-node" :class="{ active: activeNodeIds.has(id) }" :style="{ left: (nodes[id]?.x || 0) + 'px', top: (nodes[id]?.y || 0) + 'px' }" @pointerdown.prevent.stop="onNodePointerDown(id, $event)" @click.stop="toggleActiveNode(id)">
                <div class="node-title">
                  <v-avatar size="28" class="mr-2">
                    <template v-if="nodes[id]?.type === 'team' && (teamDetails[teamIdForNode(id)]?.logoUrl || teamDetails[teamIdForNode(id)]?.LogoUrl)"><v-img :src="teamDetails[teamIdForNode(id)].logoUrl || teamDetails[teamIdForNode(id)].LogoUrl" cover /></template>
                    <template v-else>
                      <span class="text-subtitle-2">
                        {{ nodes[id]?.type === 'team' ? getAvatarLetter(teamNameById[teamIdForNode(id)]) : '胜' }}
                      </span>
                    </template>
                  </v-avatar>
                  <span class="text-body-2">
                    {{ nodes[id]?.type === 'team' ? (teamNameById[teamIdForNode(id)] || ('#'+teamIdForNode(id))) : (getWinnerLabel(nodes[id]) ) }}
                  </span>
                </div>
                <div class="node-tools">
                  <v-btn size="x-small" variant="text" icon="chevron_left" @click.stop="setNodeRound(id, -1)" />
                  <v-chip size="x-small" label color="primary" variant="tonal">第 {{ nodes[id]?.round || 1 }} 轮</v-chip>
                  <v-btn size="x-small" variant="text" icon="chevron_right" @click.stop="setNodeRound(id, 1)" />
                </div>
              </div>
            </div>
          </div>
          <div class="d-flex align-center mt-3">
            <v-select v-model="selectedRoundToClear" class="mr-2" label="选择轮次" :items="nodeRounds" density="compact" style="max-width: 160px" />
            <v-btn v-if="canManageEvent" color="warning" variant="tonal" prepend-icon="backspace" :disabled="!selectedRoundToClear" @click="clearCanvasRound(selectedRoundToClear)">清空该轮画布</v-btn>
          </div>
        </v-card-text>
      </v-card>

      <div v-if="layoutMode==='knockout'" class="bracket-grid">
        <div v-for="group in roundsKnockout" :key="group.round" class="round-column" @dragover.prevent @drop="dropToRound(group.round)">
          <v-sheet class="round-header" color="primary" variant="tonal">
            <v-icon class="mr-2" icon="flag" />
            <span>{{ `第 ${group.round} 轮` }}</span>
            <v-chip label color="default" variant="tonal" class="ml-2">{{ group.items.length }} 场</v-chip>
            <v-spacer />
            <v-btn v-if="canManageEvent && editMode && group.round > 0" size="small" variant="text" prepend-icon="add" @click="addDraft(group.round)">添加比赛卡片</v-btn>
            <v-btn v-if="canManageEvent && editMode && group.round > 0" size="small" variant="text" color="error" prepend-icon="delete" @click="deleteMatchesOfRound(group.round)">删除本轮比赛</v-btn>
          </v-sheet>
          <div class="match-list">
            <v-card v-for="m in group.items" :key="m.id || m.Id" class="match-card" rounded="xl" elevation="2" variant="elevated" :draggable="editMode" @dragstart="onDragStart(m)" @dragend="onDragEnd">
              <div class="match-top d-flex align-center">
                <v-chip v-if="getStage(m)" size="small" color="primary" variant="tonal">{{ getStage(m) }}</v-chip>
                <v-spacer />
                <v-chip v-if="getWinnerName(m)" size="small" color="success" variant="tonal" prepend-icon="emoji_events">{{ getWinnerName(m) }} 获胜</v-chip>
              </div>
              <div class="match-main d-flex align-center">
                <div class="team-slot">
                  <v-avatar size="44" color="primary" variant="tonal">
                    <template v-if="teamDetails[m.homeTeamId || m.HomeTeamId]?.logoUrl || teamDetails[m.homeTeamId || m.HomeTeamId]?.LogoUrl">
                      <v-img :src="teamDetails[m.homeTeamId || m.HomeTeamId].logoUrl || teamDetails[m.homeTeamId || m.HomeTeamId].LogoUrl" cover />
                    </template>
                    <template v-else>
                      <span class="text-subtitle-2">{{ getAvatarLetter(m.homeTeamName || m.HomeTeamName) }}</span>
                    </template>
                  </v-avatar>
                  <div class="team-name">{{ m.homeTeamName || m.HomeTeamName }}</div>
                </div>
                <v-icon class="mx-2" icon="sports_martial_arts" />
                <div class="team-slot">
                  <v-avatar size="44" color="secondary" variant="tonal">
                    <template v-if="teamDetails[m.awayTeamId || m.AwayTeamId]?.logoUrl || teamDetails[m.awayTeamId || m.AwayTeamId]?.LogoUrl">
                      <v-img :src="teamDetails[m.awayTeamId || m.AwayTeamId].logoUrl || teamDetails[m.awayTeamId || m.AwayTeamId].LogoUrl" cover />
                    </template>
                    <template v-else>
                      <span class="text-subtitle-2">{{ getAvatarLetter(m.awayTeamName || m.AwayTeamName) }}</span>
                    </template>
                  </v-avatar>
                  <div class="team-name">{{ m.awayTeamName || m.AwayTeamName }}</div>
                </div>
              </div>
            </v-card>
              <template v-if="editMode && (drafts[group.round] || []).length">
                <v-card v-for="d in drafts[group.round]" :key="d.id" class="match-card" rounded="xl" elevation="1" variant="outlined">
                  <div class="match-top d-flex align-center">
                    <span class="text-body-2">未保存比赛</span>
                    <v-spacer />
                    <v-btn size="small" variant="text" prepend-icon="delete" color="error" @click="removeDraft(group.round, d.id)">移除</v-btn>
                  </div>
                  <div class="match-main d-flex align-center">
                    <div class="mr-2" style="min-width: 140px">
                      <v-select v-model="d.round" label="轮次" density="compact" :items="roundsItems" hide-details style="max-width: 140px" />
                    </div>
                    <div class="team-slot drop-zone" @dragover.prevent @drop="dropToSlot(group.round, d.id, 'home')">
                      <v-avatar size="44" color="primary" variant="tonal">
                        <template v-if="d.homeId && (teamDetails[d.homeId]?.logoUrl || teamDetails[d.homeId]?.LogoUrl)">
                          <v-img :src="teamDetails[d.homeId].logoUrl || teamDetails[d.homeId].LogoUrl" cover />
                        </template>
                      <template v-else>
                        <span class="text-subtitle-2">{{ d.homeId ? getAvatarLetter(teamNameById[d.homeId]) : '拖入主队' }}</span>
                      </template>
                    </v-avatar>
                    <div class="team-name">{{ d.homeId ? (teamNameById[d.homeId] || ('#'+d.homeId)) : '' }}</div>
                  </div>
                  <v-icon class="mx-2" icon="sports_martial_arts" />
                  <div class="team-slot drop-zone" @dragover.prevent @drop="dropToSlot(group.round, d.id, 'away')">
                    <v-avatar size="44" color="secondary" variant="tonal">
                      <template v-if="d.awayId && (teamDetails[d.awayId]?.logoUrl || teamDetails[d.awayId]?.LogoUrl)">
                        <v-img :src="teamDetails[d.awayId].logoUrl || teamDetails[d.awayId].LogoUrl" cover />
                      </template>
                      <template v-else>
                        <span class="text-subtitle-2">{{ d.awayId ? getAvatarLetter(teamNameById[d.awayId]) : '拖入客队' }}</span>
                      </template>
                    </v-avatar>
                    <div class="team-name">{{ d.awayId ? (teamNameById[d.awayId] || ('#'+d.awayId)) : '' }}</div>
                  </div>
                </div>
                <div class="d-flex align-center">
                  <v-spacer />
                  <v-btn color="primary" variant="tonal" prepend-icon="save" @click="saveDraft(group.round, d)">保存为比赛</v-btn>
                </div>
              </v-card>
            </template>
          </div>
        </div>
      </div>
      <div v-else-if="layoutMode==='swiss'" class="bracket-grid">
        <div v-for="group in roundsSwiss" :key="group.round" class="round-column" @dragover.prevent @drop="dropToRound(group.round)">
          <v-sheet class="round-header" color="primary" variant="tonal">
            <v-icon class="mr-2" icon="flag" />
            <span>{{ `第 ${group.round} 轮` }}</span>
            <v-chip label color="default" variant="tonal" class="ml-2">{{ group.items.length }} 场</v-chip>
            <v-spacer />
            <v-btn v-if="canManageEvent && editMode && group.round > 0" size="small" variant="text" prepend-icon="add" @click="addDraft(group.round)">添加比赛卡片</v-btn>
            <v-btn v-if="canManageEvent && editMode && group.round > 0" size="small" variant="text" color="error" prepend-icon="delete" @click="deleteMatchesOfRound(group.round)">删除本轮比赛</v-btn>
          </v-sheet>
          <div class="match-list">
            <v-card v-for="m in group.items" :key="m.id || m.Id" class="match-card" rounded="xl" elevation="2" variant="elevated" :draggable="editMode" @dragstart="onDragStart(m)" @dragend="onDragEnd">
              <div class="match-top d-flex align-center">
                <v-chip v-if="getStage(m)" size="small" color="primary" variant="tonal">{{ getStage(m) }}</v-chip>
                <v-spacer />
                <v-chip v-if="getWinnerName(m)" size="small" color="success" variant="tonal" prepend-icon="emoji_events">{{ getWinnerName(m) }} 获胜</v-chip>
              </div>
              <div class="match-main d-flex align-center">
                <div class="team-slot">
                  <v-avatar size="44" color="primary" variant="tonal">
                    <template v-if="teamDetails[m.homeTeamId || m.HomeTeamId]?.logoUrl || teamDetails[m.homeTeamId || m.HomeTeamId]?.LogoUrl">
                      <v-img :src="teamDetails[m.homeTeamId || m.HomeTeamId].logoUrl || teamDetails[m.homeTeamId || m.HomeTeamId].LogoUrl" cover />
                    </template>
                    <template v-else>
                      <span class="text-subtitle-2">{{ getAvatarLetter(m.homeTeamName || m.HomeTeamName) }}</span>
                    </template>
                  </v-avatar>
                  <div class="team-name">{{ m.homeTeamName || m.HomeTeamName }}</div>
                </div>
                <v-icon class="mx-2" icon="sports_martial_arts" />
                <div class="team-slot">
                  <v-avatar size="44" color="secondary" variant="tonal">
                    <template v-if="teamDetails[m.awayTeamId || m.AwayTeamId]?.logoUrl || teamDetails[m.awayTeamId || m.AwayTeamId]?.LogoUrl">
                      <v-img :src="teamDetails[m.awayTeamId || m.AwayTeamId].logoUrl || teamDetails[m.awayTeamId || m.AwayTeamId].LogoUrl" cover />
                    </template>
                    <template v-else>
                      <span class="text-subtitle-2">{{ getAvatarLetter(m.awayTeamName || m.AwayTeamName) }}</span>
                    </template>
                  </v-avatar>
                  <div class="team-name">{{ m.awayTeamName || m.AwayTeamName }}</div>
                </div>
              </div>
            </v-card>
              <template v-if="editMode && (drafts[group.round] || []).length">
                <v-card v-for="d in drafts[group.round]" :key="d.id" class="match-card" rounded="xl" elevation="1" variant="outlined">
                  <div class="match-top d-flex align-center">
                    <span class="text-body-2">未保存比赛</span>
                    <v-spacer />
                    <v-btn size="small" variant="text" prepend-icon="delete" color="error" @click="removeDraft(group.round, d.id)">移除</v-btn>
                  </div>
                  <div class="match-main d-flex align-center">
                    <div class="mr-2" style="min-width: 140px">
                      <v-select v-model="d.round" label="轮次" density="compact" :items="roundsItems" hide-details style="max-width: 140px" />
                    </div>
                    <div class="team-slot drop-zone" @dragover.prevent @drop="dropToSlot(group.round, d.id, 'home')">
                      <v-avatar size="44" color="primary" variant="tonal">
                        <template v-if="d.homeId && (teamDetails[d.homeId]?.logoUrl || teamDetails[d.homeId]?.LogoUrl)">
                          <v-img :src="teamDetails[d.homeId].logoUrl || teamDetails[d.homeId].LogoUrl" cover />
                        </template>
                      <template v-else>
                        <span class="text-subtitle-2">{{ d.homeId ? getAvatarLetter(teamNameById[d.homeId]) : '拖入主队' }}</span>
                      </template>
                    </v-avatar>
                    <div class="team-name">{{ d.homeId ? (teamNameById[d.homeId] || ('#'+d.homeId)) : '' }}</div>
                  </div>
                  <v-icon class="mx-2" icon="sports_martial_arts" />
                  <div class="team-slot drop-zone" @dragover.prevent @drop="dropToSlot(group.round, d.id, 'away')">
                    <v-avatar size="44" color="secondary" variant="tonal">
                      <template v-if="d.awayId && (teamDetails[d.awayId]?.logoUrl || teamDetails[d.awayId]?.LogoUrl)">
                        <v-img :src="teamDetails[d.awayId].logoUrl || teamDetails[d.awayId].LogoUrl" cover />
                      </template>
                      <template v-else>
                        <span class="text-subtitle-2">{{ d.awayId ? getAvatarLetter(teamNameById[d.awayId]) : '拖入客队' }}</span>
                      </template>
                    </v-avatar>
                    <div class="team-name">{{ d.awayId ? (teamNameById[d.awayId] || ('#'+d.awayId)) : '' }}</div>
                  </div>
                </div>
                <div class="d-flex align-center">
                  <v-spacer />
                  <v-btn color="primary" variant="tonal" prepend-icon="save" @click="saveDraft(group.round, d)">保存为比赛</v-btn>
                </div>
              </v-card>
              </template>
          </div>
        </div>
      </div>
      <div v-else class="group-grid">
        <div v-for="col in groupColumns" :key="col.label" class="group-column">
          <v-sheet class="group-header" color="primary" variant="tonal">
            <v-icon class="mr-2" icon="diversity_3" />
            <span>组 {{ col.label }}</span>
            <v-chip label color="default" variant="tonal" class="ml-2">{{ col.items.length }} 场</v-chip>
          </v-sheet>
          <div class="match-list">
            <v-card v-for="m in col.items" :key="m.id || m.Id" class="match-card" rounded="xl" elevation="2" variant="elevated">
              <div class="match-top d-flex align-center">
                <v-chip size="small" color="primary" variant="tonal">小组赛</v-chip>
                <v-spacer />
                <v-chip v-if="getWinnerName(m)" size="small" color="success" variant="tonal" prepend-icon="emoji_events">{{ getWinnerName(m) }} 获胜</v-chip>
              </div>
              <div class="match-main d-flex align-center">
                <div class="team-slot">
                  <v-avatar size="44" color="primary" variant="tonal">
                    <template v-if="teamDetails[m.homeTeamId || m.HomeTeamId]?.logoUrl || teamDetails[m.homeTeamId || m.HomeTeamId]?.LogoUrl">
                      <v-img :src="teamDetails[m.homeTeamId || m.HomeTeamId].logoUrl || teamDetails[m.homeTeamId || m.HomeTeamId].LogoUrl" cover />
                    </template>
                    <template v-else>
                      <span class="text-subtitle-2">{{ getAvatarLetter(m.homeTeamName || m.HomeTeamName) }}</span>
                    </template>
                  </v-avatar>
                  <div class="team-name">{{ m.homeTeamName || m.HomeTeamName }}</div>
                </div>
                <v-icon class="mx-2" icon="sports_martial_arts" />
                <div class="team-slot">
                  <v-avatar size="44" color="secondary" variant="tonal">
                    <template v-if="teamDetails[m.awayTeamId || m.AwayTeamId]?.logoUrl || teamDetails[m.awayTeamId || m.AwayTeamId]?.LogoUrl">
                      <v-img :src="teamDetails[m.awayTeamId || m.AwayTeamId].logoUrl || teamDetails[m.awayTeamId || m.AwayTeamId].LogoUrl" cover />
                    </template>
                    <template v-else>
                      <span class="text-subtitle-2">{{ getAvatarLetter(m.awayTeamName || m.AwayTeamName) }}</span>
                    </template>
                  </v-avatar>
                  <div class="team-name">{{ m.awayTeamName || m.AwayTeamName }}</div>
                </div>
              </div>
            </v-card>
          </div>
        </div>
      </div>

    </div>
  </v-container>
</template>

<style scoped>
.bracket-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px }
.round-column { border-radius: 16px; background: rgba(0,0,0,0.02); padding: 8px }
.round-header { display: flex; align-items: center; padding: 6px 10px; border-radius: 10px }
.match-list { display: flex; flex-direction: column; gap: 8px; padding: 6px }
.match-card { padding: 8px }
.match-top { padding: 0 4px 4px 4px }
.match-main { gap: 6px; padding: 4px 4px }
.team-slot { display: flex; align-items: center; gap: 6px }
.team-name { max-width: 140px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis }
.team-pool { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 8px }
.team-chip { display: flex; align-items: center; gap: 8px; padding: 8px 10px; border-radius: 12px; background: rgba(0,0,0,0.04) }
.team-chip.active { outline: 2px solid rgba(25,118,210,0.5) }
.drop-zone { border: 1px dashed rgba(0,0,0,0.2); border-radius: 12px; padding: 4px }
.canvas { position: relative; height: 520px; border-radius: 12px; background: linear-gradient(0deg, rgba(0,0,0,0.02), rgba(0,0,0,0.02)); outline: 1px dashed rgba(0,0,0,0.15) }
.canvas { overflow: hidden; height: 640px }
.canvas-content { position: absolute; inset: 0; width: 100%; height: 100% }
.edges { position: absolute; inset: 0; width: 100%; height: 100% }
.edge-path { fill: none; stroke: rgba(0,0,0,0.3); stroke-width: 2 }
.canvas-node { position: absolute; min-width: 160px; max-width: 220px; cursor: grab; border-radius: 10px; padding: 6px; background: white; box-shadow: 0 2px 8px rgba(0,0,0,0.08); border: 1px solid rgba(0,0,0,0.08) }
.canvas-node.active { border-color: rgba(25,118,210,0.6); box-shadow: 0 0 0 2px rgba(25,118,210,0.2) }
.node-title { display: flex; align-items: center }
.node-tools { display: flex; align-items: center; gap: 4px; margin-top: 6px }
.group-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px }
.group-column { border-radius: 16px; background: rgba(0,0,0,0.02); padding: 8px }
.group-header { display: flex; align-items: center; padding: 6px 10px; border-radius: 10px }
</style>

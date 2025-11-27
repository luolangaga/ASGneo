<script setup>
import { ref, onMounted, onUnmounted, computed, watch } from 'vue'
import { renderMarkdown } from '../utils/markdown'
import { getArticles } from '../services/articles'
import { useRoute, useRouter } from 'vue-router'
import { getEvent, getEventRegistrations, exportEventRegistrationsCsv, exportEventTeamLogosZip, registerTeamToEvent, uploadEventLogo, updateTeamRegistrationStatus, setEventChampion, getEventAdmins, addEventAdmin, removeEventAdmin, getRuleRevisions, publishRuleRevision, createRuleRevision, updateRegistrationFormSchema, getRegistrationFormSchema, submitRegistrationAnswers, getRegistrationAnswers } from '../services/events'
import { getTeam, uploadTeamLogo, generateInvite, setTeamDispute } from '../services/teams'
import { getMatches } from '../services/matches'
import { getUser } from '../services/user'
import { currentUser, isAuthenticated } from '../stores/auth'
import PageHero from '../components/PageHero.vue'
import ResultDialog from '../components/ResultDialog.vue'
import { extractErrorDetails } from '../services/api'

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
const matches = ref([])
const teamDetails = ref({}) // teamId -> TeamDto
const loadingTeamIds = ref(new Set())
const registering = ref(false)
const actionError = ref('')
const successMsg = ref('')
const showSuccess = ref(false)
const errorOpen = ref(false)
const errorDetails = ref([])
const shareDialog = ref(false)
const rulesDialogOpen = ref(false)
const rulesLoading = ref(false)
const ruleError = ref('')
const ruleRevisions = ref([])
const selectedRuleRevisionId = ref(null)
const creatingRuleRevision = ref(false)
const rulePublishLoading = ref(false)
const newRuleMarkdown = ref('')
const newRuleChangeNotes = ref('')
const regSchemaObj = ref(null)
const regSchemaLoading = ref(false)
const regFormDialogOpen = ref(false)
const regAnswersMap = ref({})
const regSubmitLoading = ref(false)
const regSchemaEditorOpen = ref(false)
const regSchemaEditorLoading = ref(false)
const regSchemaJsonText = ref('')
const regSchemaSaveLoading = ref(false)
const regFormPreviewMode = ref(false)
const regAnswersByTeam = ref({})
const regAnswersLoading = ref(false)
const visualEditorOpen = ref(false)
const visualEditorLoading = ref(false)
const visualEditorSaving = ref(false)
const visualFields = ref([])
const newFieldType = ref('text')
const uploadingLogo = ref(false)
const uploadLogoError = ref('')
const showTeamLogoDialog = ref(false)
const teamLogoFile = ref(null)
const teamLogoError = ref('')
const teamLogoUploading = ref(false)
const pendingTeamId = ref(null)
const inviteInfo = ref(null)
const generatingInvite = ref(false)
// 审批时是否邮件通知队伍拥有者
const notifyByEmail = ref(false)

  const disputeTogglingIds = ref(new Set())
  const disputeDialogOpen = ref(false)
  const disputeTeamId = ref(null)
  const disputeDetailText = ref('')
  const selectedDisputeArticleId = ref(null)
  const disputeArticleSearch = ref('')
  const disputeArticleOptions = ref([])
  const disputeArticleLoading = ref(false)
  function isGuid(s) { return /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(String(s || '').trim()) }
const disputeSwitchMap = ref({})
function toMd(s) { return renderMarkdown(s || '') }
 

async function fetchDisputeArticles(q) {
  try {
    disputeArticleLoading.value = true
    const res = await getArticles({ query: q, pageSize: 10, sortBy: 'createdAt', desc: true })
    const items = (res.items || res.Items || []).map(a => ({ id: a.id || a.Id, title: a.title || a.Title }))
    disputeArticleOptions.value = items
  } catch (e) {
    disputeArticleOptions.value = []
  } finally {
    disputeArticleLoading.value = false
  }
}

 

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
const eventQqGroup = computed(() => ev.value?.qqGroup || ev.value?.QqGroup || '')
const eventRulesMarkdown = computed(() => ev.value?.rulesMarkdown || ev.value?.RulesMarkdown || '')
const eventRulesHtml = computed(() => renderMarkdown(eventRulesMarkdown.value || ''))
const selectedRuleRevision = computed(() => {
  const id = selectedRuleRevisionId.value
  if (!id) return null
  const list = ruleRevisions.value || []
  return list.find(r => (r.id || r.Id) === id) || null
})
const selectedRulesHtml = computed(() => {
  const rev = selectedRuleRevision.value
  if (rev) {
    const md = rev.contentMarkdown || rev.ContentMarkdown || ''
    return renderMarkdown(md || '')
  }
  return eventRulesHtml.value
})
const regSchemaFields = computed(() => {
  const obj = regSchemaObj.value || {}
  const arr = obj.fields || obj.Fields || []
  return Array.isArray(arr) ? arr : []
})
const playerTypeRequirements = computed(() => {
  try {
    const cd = ev.value?.customData || ev.value?.CustomData || ''
    if (!cd) return { regulator: { min: null, max: null }, survivor: { min: null, max: null } }
    const obj = JSON.parse(cd)
    let req = obj.playerTypeRequirements || obj.PlayerTypeRequirements || {}
    if (typeof req === 'string') { try { req = JSON.parse(req) } catch {} }
    const reg = req.regulator || req.Regulator || {}
    const sur = req.survivor || req.Survivor || {}
    const toNum = (v) => {
      if (v == null || v === '') return null
      const n = Number(v)
      return Number.isFinite(n) ? n : null
    }
    return {
      regulator: { min: toNum(reg.min ?? reg.Min), max: toNum(reg.max ?? reg.Max) },
      survivor: { min: toNum(sur.min ?? sur.Min), max: toNum(sur.max ?? sur.Max) }
    }
  } catch { return { regulator: { min: null, max: null }, survivor: { min: null, max: null } } }
})
// 角色人数限制编辑态（可选，空为无限制）
const reqMinReg = ref(null)
const reqMaxReg = ref(null)
const reqMinSur = ref(null)
const reqMaxSur = ref(null)

watch(ev, () => {
  try {
    const req = playerTypeRequirements.value || { regulator: {}, survivor: {} }
    reqMinReg.value = req.regulator?.min ?? null
    reqMaxReg.value = req.regulator?.max ?? null
    reqMinSur.value = req.survivor?.min ?? null
    reqMaxSur.value = req.survivor?.max ?? null
  } catch {
    reqMinReg.value = null
    reqMaxReg.value = null
    reqMinSur.value = null
    reqMaxSur.value = null
  }
}, { immediate: true })

function normalizeOptInt(v) {
  if (v == null || v === '') return null
  const n = Number(v)
  return Number.isFinite(n) ? n : null
}

async function savePlayerTypeRequirements() {
  if (!canManageEvent.value) return
  try {
    const prev = regSchemaObj.value || {}
    const fields = Array.isArray(prev.fields || prev.Fields) ? (prev.fields || prev.Fields) : []
    const payload = {
      fields,
      playerTypeRequirements: {
        regulator: { min: normalizeOptInt(reqMinReg.value), max: normalizeOptInt(reqMaxReg.value) },
        survivor: { min: normalizeOptInt(reqMinSur.value), max: normalizeOptInt(reqMaxSur.value) }
      }
    }
    const schemaStr = JSON.stringify(payload)
    await updateRegistrationFormSchema(eventId, schemaStr)
    ev.value = await getEvent(eventId)
    await loadRegistrationFormSchema()
    successMsg.value = '角色人数限制已保存'
    showSuccess.value = true
  } catch (e) {
    actionError.value = e?.payload?.message || e?.message || '保存角色人数限制失败'
    errorDetails.value = extractErrorDetails(e?.payload)
    errorOpen.value = true
  }
}
const selectPlayersDialogOpen = ref(false)
const selectedPlayerIds = ref([])
const selectingPlayers = ref(false)
const selectionError = ref('')
const myTeamPlayers = ref([])
const selectedPlayerIdsSet = computed(() => new Set((selectedPlayerIds.value || []).map(id => String(id))))
function playerTypeName(pt) { const v = Number(pt || 2); return v === 1 ? '监管者' : '求生者' }
function countByType(list) {
  const reg = list.filter(p => Number(p.playerType || p.PlayerType || 2) === 1).length
  const sur = list.filter(p => Number(p.playerType || p.PlayerType || 2) === 2).length
  return { regulator: reg, survivor: sur }
}
function teamPlayersForEvent(teamId) {
  const t = teamDetails.value[teamId] || {}
  const all = t.players || t.Players || []
  const ans = (regAnswersByTeam.value || {})[teamId] || {}
  const ids = ans.selectedPlayerIds || ans.SelectedPlayerIds || []
  const idSet = new Set(Array.isArray(ids) ? ids.map(x => String(x)) : [])
  if (idSet.size > 0) return all.filter(p => idSet.has(String(p.id || p.Id)))
  return all
}
function toggleSelectPlayer(p) {
  const id = String(p.id || p.Id)
  const pt = Number(p.playerType || p.PlayerType || 2)
  const maxReg = playerTypeRequirements.value.regulator.max
  const maxSur = playerTypeRequirements.value.survivor.max
  const curReg = (selectedPlayerIds.value || []).filter(x => {
    const tp = myTeamPlayers.value.find(pp => String(pp.id || pp.Id) === String(x))?.playerType || myTeamPlayers.value.find(pp => String(pp.id || pp.Id) === String(x))?.PlayerType
    return Number(tp || 2) === 1
  }).length
  const curSur = (selectedPlayerIds.value || []).filter(x => {
    const tp = myTeamPlayers.value.find(pp => String(pp.id || pp.Id) === String(x))?.playerType || myTeamPlayers.value.find(pp => String(pp.id || pp.Id) === String(x))?.PlayerType
    return Number(tp || 2) === 2
  }).length
  const set = new Set(selectedPlayerIds.value.map(x => String(x)))
  if (set.has(id)) {
    set.delete(id)
  } else {
    if (pt === 1 && maxReg != null && curReg >= maxReg) return
    if (pt === 2 && maxSur != null && curSur >= maxSur) return
    set.add(id)
  }
  selectedPlayerIds.value = Array.from(set)
}
async function onConfirmSelectPlayersAndSubmit() {
  selectionError.value = ''
  const maxReg = playerTypeRequirements.value.regulator.max
  const maxSur = playerTypeRequirements.value.survivor.max
  const { regulator: selReg, survivor: selSur } = countByType(myTeamPlayers.value.filter(p => selectedPlayerIdsSet.value.has(String(p.id || p.Id))))
  if (maxReg != null && selReg > maxReg) { selectionError.value = `监管者最多${maxReg}人`; return }
  if (maxSur != null && selSur > maxSur) { selectionError.value = `求生者最多${maxSur}人`; return }
  selectingPlayers.value = true
  try {
    const me = currentUser.value
    const myTeamId = me?.teamId || me?.TeamId
    regAnswersMap.value['selectedPlayerIds'] = [...selectedPlayerIds.value]
    const payload = JSON.stringify(regAnswersMap.value || {})
    await submitRegistrationAnswers(eventId, myTeamId, payload)
    await registerTeamToEvent(eventId, { teamId: myTeamId })
    regFormDialogOpen.value = false
    selectPlayersDialogOpen.value = false
    successMsg.value = '报名提交成功，请等待审核'
    showSuccess.value = true
    registrations.value = await getEventRegistrations(eventId)
    await loadAllRegistrationAnswers()
  } catch (e) {
    actionError.value = e?.payload?.message || e?.message || '提交报名信息失败'
    errorDetails.value = extractErrorDetails(e?.payload)
    errorOpen.value = true
  } finally {
    selectingPlayers.value = false
    regSubmitLoading.value = false
  }
}

async function loadRuleRevisions() {
  ruleError.value = ''
  rulesLoading.value = true
  try {
    const list = await getRuleRevisions(eventId)
    ruleRevisions.value = Array.isArray(list) ? list : (list?.items || list?.Items || [])
    selectedRuleRevisionId.value = null
  } catch (e) {
    ruleError.value = e?.payload?.message || e?.message || '加载规则版本失败'
  } finally {
    rulesLoading.value = false
  }
}

async function onPublishSelectedRule() {
  if (!canManageEvent.value) return
  const id = selectedRuleRevisionId.value
  if (!id) return
  rulePublishLoading.value = true
  actionError.value = ''
  successMsg.value = ''
  try {
    await publishRuleRevision(eventId, id)
    ev.value = await getEvent(eventId)
    successMsg.value = '已发布该规则版本'
    showSuccess.value = true
    await loadRuleRevisions()
  } catch (e) {
    actionError.value = e?.payload?.message || e?.message || '发布规则版本失败'
    errorDetails.value = extractErrorDetails(e?.payload)
  } finally {
    rulePublishLoading.value = false
  }
}

async function onCreateRuleRevision() {
  if (!canManageEvent.value) return
  const contentMarkdown = (newRuleMarkdown.value || '').trim()
  if (!contentMarkdown) { actionError.value = '请输入规则内容'; errorOpen.value = true; return }
  creatingRuleRevision.value = true
  actionError.value = ''
  successMsg.value = ''
  try {
    await createRuleRevision(eventId, { contentMarkdown, changeNotes: (newRuleChangeNotes.value || '').trim() })
    newRuleMarkdown.value = ''
    newRuleChangeNotes.value = ''
    await loadRuleRevisions()
    successMsg.value = '规则版本已创建'
    showSuccess.value = true
  } catch (e) {
    actionError.value = e?.payload?.message || e?.message || '创建规则版本失败'
    errorDetails.value = extractErrorDetails(e?.payload)
  } finally {
    creatingRuleRevision.value = false
  }
}
async function loadRegistrationFormSchema() {
  regSchemaLoading.value = true
  try {
    const res = await getRegistrationFormSchema(eventId)
    let obj = {}
    try {
      if (typeof res === 'string') obj = JSON.parse(res)
      else obj = res || {}
    } catch { obj = {} }
    regSchemaObj.value = obj || {}
    regAnswersMap.value = {}
    for (const f of regSchemaFields.value) {
      const key = f.id || f.Id
      let d = f.default
      if (d == null && (f.type === 'checkbox' || f.Type === 'checkbox')) d = false
      regAnswersMap.value[key] = d ?? ''
    }
  } catch (e) {
    regSchemaObj.value = null
  } finally {
    regSchemaLoading.value = false
  }
}

async function loadRegistrationAnswersForTeam(teamId) {
  try {
    const res = await getRegistrationAnswers(eventId, teamId)
    let obj = {}
    try { obj = typeof res === 'string' ? JSON.parse(res) : (res || {}) } catch { obj = {} }
    regAnswersByTeam.value[teamId] = obj || {}
    regAnswersByTeam.value = { ...regAnswersByTeam.value }
  } catch {}
}

async function loadAllRegistrationAnswers() {
  if (!canManageEvent.value) return
  const list = Array.isArray(registrations.value) ? registrations.value : []
  const ids = list.map(r => r.teamId || r.TeamId).filter(Boolean)
  if (!ids.length) return
  regAnswersLoading.value = true
  try {
    const map = {}
    await Promise.all(ids.map(async id => {
      try {
        const res = await getRegistrationAnswers(eventId, id)
        let obj = {}
        try { obj = typeof res === 'string' ? JSON.parse(res) : (res || {}) } catch { obj = {} }
        map[id] = obj || {}
      } catch { map[id] = {} }
    }))
    regAnswersByTeam.value = map
  } finally {
    regAnswersLoading.value = false
  }
}

function formatAnswer(teamId, key, type) {
  const m = regAnswersByTeam.value || {}
  const o = m[teamId] || {}
  let v = o[key]
  if (v == null) return '—'
  const t = type || 'text'
  if ((t === 'checkbox')) {
    const yes = (v === true || v === 'true' || v === 1 || v === '1')
    return yes ? '是' : '否'
  }
  if (Array.isArray(v)) return v.map(x => (x == null ? '' : String(x))).join('、')
  if (typeof v === 'object') return JSON.stringify(v)
  return String(v)
}
async function openSchemaEditor() {
  if (!canManageEvent.value) return
  regSchemaEditorLoading.value = true
  try {
    const res = await getRegistrationFormSchema(eventId)
    let obj = {}
    try { obj = typeof res === 'string' ? JSON.parse(res) : (res || {}) } catch { obj = {} }
    regSchemaJsonText.value = JSON.stringify(obj || {}, null, 2)
    regSchemaEditorOpen.value = true
  } catch (e) {
    actionError.value = e?.payload?.message || e?.message || '加载报名表Schema失败'
    errorOpen.value = true
  } finally {
    regSchemaEditorLoading.value = false
  }
}

function genFieldId() {
  return 'field_' + Math.random().toString(36).slice(2, 8)
}

async function openVisualEditor() {
  if (!canManageEvent.value) return
  visualEditorLoading.value = true
  try {
    const obj = regSchemaObj.value || {}
    const arr = Array.isArray(obj.fields || obj.Fields || []) ? (obj.fields || obj.Fields || []) : []
    visualFields.value = arr.map(f => {
      const opts = Array.isArray(f.options || f.Options) ? (f.options || f.Options) : []
      return {
        id: f.id || f.Id || '',
        label: f.label || f.Label || '',
        type: f.type || f.Type || 'text',
        required: !!(f.required || f.Required),
        default: f.default,
        optionsText: opts.join('\n')
      }
    })
    if (!visualFields.value.length) visualFields.value = []
    visualEditorOpen.value = true
  } finally {
    visualEditorLoading.value = false
  }
}

function addField(type = null) {
  const t = type || newFieldType.value || 'text'
  visualFields.value.push({ id: genFieldId(), label: '', type: t, required: false, default: (t === 'checkbox' ? false : ''), optionsText: '' })
  visualFields.value = [...visualFields.value]
}

function removeField(index) {
  if (index < 0 || index >= visualFields.value.length) return
  visualFields.value.splice(index, 1)
  visualFields.value = [...visualFields.value]
}

function moveField(index, dir) {
  const i = index
  const j = i + dir
  if (i < 0 || j < 0 || i >= visualFields.value.length || j >= visualFields.value.length) return
  const arr = [...visualFields.value]
  const tmp = arr[i]
  arr[i] = arr[j]
  arr[j] = tmp
  visualFields.value = arr
}

async function saveVisualEditor() {
  if (!canManageEvent.value) return
  visualEditorSaving.value = true
  actionError.value = ''
  successMsg.value = ''
  try {
    const fields = visualFields.value.map(f => {
      const opts = String(f.optionsText || '').split('\n').map(s => s.trim()).filter(Boolean)
      const o = { id: (f.id || '').trim(), label: (f.label || '').trim(), type: (f.type || 'text'), required: !!f.required }
      if (f.type === 'select') o.options = opts
      if (f.type === 'checkbox') o.default = !!f.default
      else o.default = f.default
      return o
    })
    const schemaStr = JSON.stringify({ fields })
    await updateRegistrationFormSchema(eventId, schemaStr)
    visualEditorOpen.value = false
    await loadRegistrationFormSchema()
    successMsg.value = '报名表已保存'
    showSuccess.value = true
  } catch (e) {
    actionError.value = e?.payload?.message || e?.message || '保存失败'
    errorDetails.value = extractErrorDetails(e?.payload)
    errorOpen.value = true
  } finally {
    visualEditorSaving.value = false
  }
}
async function saveSchemaEditor() {
  if (!canManageEvent.value) return
  regSchemaSaveLoading.value = true
  actionError.value = ''
  successMsg.value = ''
  try {
    let obj
    try { obj = JSON.parse(regSchemaJsonText.value || '{}') } catch { throw new Error('JSON 解析失败') }
    const schemaStr = JSON.stringify(obj)
    await updateRegistrationFormSchema(eventId, schemaStr)
    regSchemaEditorOpen.value = false
    await loadRegistrationFormSchema()
    successMsg.value = '报名表Schema已保存'
    showSuccess.value = true
  } catch (e) {
    actionError.value = e?.payload?.message || e?.message || '保存报名表Schema失败'
    errorDetails.value = extractErrorDetails(e?.payload)
  } finally {
    regSchemaSaveLoading.value = false
  }
}
async function onSubmitRegFormAndRegister() {
  if (!isAuthenticated.value) { router.push('/login'); return }
  const me = currentUser.value
  const myTeamId = me?.teamId || me?.TeamId
  regSubmitLoading.value = true
  actionError.value = ''
  successMsg.value = ''
  try {
    const missing = []
    for (const f of regSchemaFields.value) {
      const required = !!(f.required || f.Required)
      if (!required) continue
      const key = f.id || f.Id
      const type = f.type || f.Type
      const val = regAnswersMap.value[key]
      let empty = false
      if (type === 'checkbox') { empty = !(val === true || val === 'true') }
      else { empty = (val == null || String(val).trim() === '') }
      if (empty) missing.push(f.label || f.Label || key)
    }
    if (missing.length) {
      actionError.value = `请填写必填项：${missing.join('、')}`
      errorDetails.value = missing.map(m => ({ key: m, message: '未填写' }))
      errorOpen.value = true
      return
    }
    let myTeam
    try { myTeam = await getTeam(myTeamId) } catch { myTeam = null }
    const players = myTeam ? (myTeam.players || myTeam.Players || []) : []
    const { regulator: regCount, survivor: surCount } = countByType(players)
    const minReg = playerTypeRequirements.value.regulator.min
    const minSur = playerTypeRequirements.value.survivor.min
    if (minReg != null && regCount < minReg) { actionError.value = `不满足报名要求：监管者至少${minReg}名`; errorOpen.value = true; return }
    if (minSur != null && surCount < minSur) { actionError.value = `不满足报名要求：求生者至少${minSur}名`; errorOpen.value = true; return }
    const maxReg = playerTypeRequirements.value.regulator.max
    const maxSur = playerTypeRequirements.value.survivor.max
    if ((maxReg != null && regCount > maxReg) || (maxSur != null && surCount > maxSur)) {
      myTeamPlayers.value = players
      selectedPlayerIds.value = []
      selectPlayersDialogOpen.value = true
      return
    }
    const payload = JSON.stringify(regAnswersMap.value || {})
    await submitRegistrationAnswers(eventId, myTeamId, payload)
    await registerTeamToEvent(eventId, { teamId: myTeamId })
    regFormDialogOpen.value = false
    successMsg.value = '报名提交成功，请等待审核'
    showSuccess.value = true
    registrations.value = await getEventRegistrations(eventId)
    await loadAllRegistrationAnswers()
  } catch (e) {
    actionError.value = e?.payload?.message || e?.message || '提交报名信息失败'
    errorDetails.value = extractErrorDetails(e?.payload)
    errorOpen.value = true
  } finally {
    regSubmitLoading.value = false
  }
}
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
  const origin = (typeof window !== 'undefined' && window.location) ? window.location.origin : ''
  const id = eventId || ev.value?.id || ev.value?.Id
  return `${origin}/events/${id}`
})

const heroTitle = computed(() => ev.value?.name || '赛事详情')

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

const canManageAdmins = computed(() => {
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

const adminList = ref([])
const loadingAdmins = ref(false)
const adminError = ref('')
const addUserId = ref('')
const addingAdmin = ref(false)
const removingAdminIds = ref(new Set())
const loadingMatches = ref(false)

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

function getRegisteredByUserId(r) {
  return r?.registeredByUserId || r?.RegisteredByUserId || ''
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
        const name = u?.fullName || u?.FullName || u?.email || ''
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
    await loadAllRegistrationAnswers()
    // 初始化纠纷状态开关映射
    const map = {}
    for (const r of registrations.value) {
      const tid = r.teamId || r.TeamId
      const hd = !!(r.teamHasDispute || r.TeamHasDispute)
      map[tid] = hd
    }
    disputeSwitchMap.value = map
    await loadAllRegistrationAnswers()
  } catch (err2) {
    regsError.value = err2?.payload?.message || err2?.message || '加载报名队伍失败'
  } finally {
    loadingRegs.value = false
  }
    try {
      loadingAdmins.value = true
      adminList.value = await getEventAdmins(eventId)
    } catch (e) {
      adminError.value = e?.payload?.message || e?.message || '加载赛事管理员失败'
    } finally {
      loadingAdmins.value = false
    }
    try {
      loadingMatches.value = true
      matches.value = await getMatches({ eventId, pageSize: 1000 })
    } catch (e) {
    } finally {
      loadingMatches.value = false
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
onMounted(() => { loadRegistrationFormSchema() })

onUnmounted(() => {
  if (regTimer) { clearInterval(regTimer); regTimer = null }
})

watch(disputeArticleSearch, (q) => {
  if (!disputeDialogOpen.value) return
  fetchDisputeArticles(q)
})
watch(disputeDialogOpen, (open) => {
  if (open) fetchDisputeArticles(disputeArticleSearch.value || '')
})
watch(rulesDialogOpen, (open) => {
  if (open) loadRuleRevisions()
})

function backToEvents() {
  router.push('/events')
}

function joinQqGroup() {
  const raw = eventQqGroup.value || ''
  if (!raw) return
  const s = String(raw).trim()
  let url = s
  const isUrl = /^https?:\/\//i.test(s) || /^mqq/i.test(s) || /^qq:\/\//i.test(s)
  if (!isUrl) {
    const digits = s.replace(/\D/g, '')
    if (digits) {
      url = `mqqapi://card/show_pslcard?src_type=internal&version=1&uin=${digits}&card_type=group&source=qrcode`
    }
  }
  try { window.open(url, '_blank') } catch {}
}

async function onAddAdmin() {
  adminError.value = ''
  successMsg.value = ''
  if (!canManageAdmins.value) return
  const uid = (addUserId.value || '').trim()
  if (!uid) { adminError.value = '请输入用户ID'; return }
  addingAdmin.value = true
  try {
    await addEventAdmin(eventId, uid)
    addUserId.value = ''
    adminList.value = await getEventAdmins(eventId)
    successMsg.value = '已添加赛事管理员'
    showSuccess.value = true
  } catch (e) {
    adminError.value = e?.payload?.message || e?.message || '添加赛事管理员失败'
    errorDetails.value = extractErrorDetails(e?.payload)
  } finally {
    addingAdmin.value = false
  }
}

async function onRemoveAdmin(userId) {
  if (!canManageAdmins.value) return
  const id = userId
  removingAdminIds.value.add(id)
  try {
    await removeEventAdmin(eventId, id)
    adminList.value = await getEventAdmins(eventId)
    successMsg.value = '已移除赛事管理员'
    showSuccess.value = true
  } catch (e) {
    adminError.value = e?.payload?.message || e?.message || '移除赛事管理员失败'
    errorDetails.value = extractErrorDetails(e?.payload)
  } finally {
    removingAdminIds.value.delete(id)
  }
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
    await loadRegistrationAnswersForTeam(teamId)
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
    const shouldNotify = notifyByEmail.value || normalizeStatus(status) === 'Approved'
    const updated = await updateTeamRegistrationStatus(eventId, teamId, { status, notifyByEmail: shouldNotify })
    // 用返回的数据更新本地列表项
    const idx = registrations.value.findIndex(r => (r.teamId || r.TeamId) === teamId)
    if (idx >= 0) {
      registrations.value[idx] = { ...registrations.value[idx], ...updated }
      registrations.value = [...registrations.value]
      await loadRegistrationAnswersForTeam(teamId)
    } else {
      // 找不到则刷新列表
      registrations.value = await getEventRegistrations(eventId)
      await loadAllRegistrationAnswers()
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


function getTeamHasDispute(r) {
  return !!(r.teamHasDispute || r.TeamHasDispute)
}

async function onToggleDispute(teamId) {
  if (!canManageEvent.value) return
  if (disputeTogglingIds.value.has(teamId)) return
  actionError.value = ''
  successMsg.value = ''
  try {
    const has = !!disputeSwitchMap.value[teamId]
    if (has) {
      disputeTeamId.value = teamId
      disputeDetailText.value = ''
      selectedDisputeArticleId.value = null
      disputeArticleSearch.value = ''
      disputeArticleOptions.value = []
      fetchDisputeArticles('')
      disputeDialogOpen.value = true
      return
    } else {
      disputeTogglingIds.value.add(teamId)
      await setTeamDispute(teamId, { hasDispute: false })
    }
    // 刷新报名列表以同步统计与展示
    registrations.value = await getEventRegistrations(eventId)
    await loadAllRegistrationAnswers()
    // 同步开关映射
    const map = { ...disputeSwitchMap.value }
    for (const r of registrations.value) {
      const tid = r.teamId || r.TeamId
      map[tid] = !!(r.teamHasDispute || r.TeamHasDispute)
    }
    disputeSwitchMap.value = map
    successMsg.value = has ? '已标记为存在纠纷' : '已取消纠纷标记'
    showSuccess.value = true
  } catch (err) {
    actionError.value = err?.payload?.message || err?.message || '设置纠纷状态失败'
    // 回滚开关状态
    disputeSwitchMap.value[teamId] = !disputeSwitchMap.value[teamId]
    errorDetails.value = extractErrorDetails(err?.payload)
  } finally {
    disputeTogglingIds.value.delete(teamId)
  }
}

async function confirmSetDispute() {
  if (!canManageEvent.value) return
  const teamId = disputeTeamId.value
  if (!teamId) { disputeDialogOpen.value = false; return }
  const postId = selectedDisputeArticleId.value
  if (!postId) { actionError.value = '必须选择关联的社区帖子'; errorOpen.value = true; return }
  try {
    await setTeamDispute(teamId, { hasDispute: true, disputeDetail: (disputeDetailText.value || '').trim(), communityPostId: postId })
    disputeDialogOpen.value = false
    successMsg.value = '已标记为存在纠纷'
    showSuccess.value = true
    registrations.value = await getEventRegistrations(eventId)
    await loadAllRegistrationAnswers()
    const map = { ...disputeSwitchMap.value }
    for (const r of registrations.value) {
      const tid = r.teamId || r.TeamId
      map[tid] = !!(r.teamHasDispute || r.TeamHasDispute)
    }
    disputeSwitchMap.value = map
  } catch (err) {
    actionError.value = err?.payload?.message || err?.message || '设置纠纷状态失败'
    errorDetails.value = extractErrorDetails(err?.payload)
  }
}

function openRatingDialog(teamId) { }

async function submitTeamRating() { }

async function doExportCsv() {
  try {
    const blob = await exportEventRegistrationsCsv(eventId, { loading: '正在导出报名CSV...' })
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

async function doExportLogosZip() {
  try {
    const blob = await exportEventTeamLogosZip(eventId, { loading: '正在打包战队Logo...' })
    const url = URL.createObjectURL(blob)
    const nameSafe = (ev.value?.name || 'event').replace(/[^a-zA-Z0-9_\-]/g, '_')
    const a = document.createElement('a')
    a.href = url
    a.download = `${nameSafe}-team-logos.zip`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '导出徽标ZIP失败'
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
  await loadRegistrationFormSchema()
  // 若队员人数超过上限，立即弹出“选择参赛队员”
  let exceedLimit = false
  let myTeamPlayersList = []
  try {
    const myTeam = await getTeam(myTeamId)
    const players = myTeam ? (myTeam.players || myTeam.Players || []) : []
    myTeamPlayersList = players
    const { regulator: regCount, survivor: surCount } = countByType(players)
    const maxReg = playerTypeRequirements.value.regulator.max
    const maxSur = playerTypeRequirements.value.survivor.max
    exceedLimit = (maxReg != null && regCount > maxReg) || (maxSur != null && surCount > maxSur)
  } catch {}
  if (exceedLimit) {
    myTeamPlayers.value = myTeamPlayersList
    selectedPlayerIds.value = []
    selectPlayersDialogOpen.value = true
  }
  if (regSchemaFields.value && regSchemaFields.value.length > 0) {
    regFormPreviewMode.value = false
    regFormDialogOpen.value = true
    return
  }
  if (exceedLimit) return
  registering.value = true
  try {
    await registerTeamToEvent(eventId, { teamId: myTeamId })
    successMsg.value = '报名提交成功，请等待审核'
    showSuccess.value = true
    registrations.value = await getEventRegistrations(eventId)
    await loadAllRegistrationAnswers()
  } catch (err) {
    actionError.value = err?.payload?.message || err?.message || '报名失败'
    errorDetails.value = extractErrorDetails(err?.payload)
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
    errorDetails.value = extractErrorDetails(err?.payload)
  } finally {
    uploadingLogo.value = false
  }
}

watch([actionError, uploadLogoError, teamLogoError], (vals) => {
  const m = vals[0] || vals[1] || vals[2]
  if (m) errorOpen.value = true
})

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
    await loadAllRegistrationAnswers()
  } catch (err) {
    teamLogoError.value = err?.payload?.message || err?.message || '上传队伍Logo失败'
    errorDetails.value = extractErrorDetails(err?.payload)
  } finally {
    teamLogoUploading.value = false
    registering.value = false
  }
}

async function onGenerateTeamInvite() {
  if (!isAuthenticated.value) { router.push('/login'); return }
  const me = currentUser.value
  const myTeamId = me?.teamId || me?.TeamId
  if (!myTeamId) { actionError.value = '您还没有战队，请先创建战队'; router.push('/teams/create'); return }
  generatingInvite.value = true
  actionError.value = ''
  successMsg.value = ''
  try {
    const info = await generateInvite(myTeamId, 7)
    inviteInfo.value = info
    successMsg.value = '邀请链接已生成'
    showSuccess.value = true
  } catch (err) {
    actionError.value = err?.payload?.message || err?.message || '生成邀请失败'
    errorDetails.value = extractErrorDetails(err?.payload)
  } finally {
    generatingInvite.value = false
  }
}

async function copyInviteLink() {
  if (!inviteInfo.value) return
  const token = inviteInfo.value.token || inviteInfo.value.Token
  const origin = (typeof window !== 'undefined' && window.location) ? window.location.origin : ''
  const url = `${origin}/join/${token}`
  try {
    await navigator.clipboard.writeText(url)
    successMsg.value = '邀请链接已复制'
    showSuccess.value = true
  } catch {
    actionError.value = '复制失败，请手动复制'
  }
}
const analytics = computed(() => {
  const regs = Array.isArray(registrations.value) ? registrations.value : []
  const total = regs.length
  const by = {
    approved: regs.filter(r => normalizeStatus(r.status || r.Status) === 'Approved').length,
    pending: regs.filter(r => normalizeStatus(r.status || r.Status) === 'Pending').length,
    registered: regs.filter(r => normalizeStatus(r.status || r.Status) === 'Registered').length,
    confirmed: regs.filter(r => normalizeStatus(r.status || r.Status) === 'Confirmed').length,
    cancelled: regs.filter(r => normalizeStatus(r.status || r.Status) === 'Cancelled').length,
    rejected: regs.filter(r => normalizeStatus(r.status || r.Status) === 'Rejected').length,
  }
  const approvedRate = total > 0 ? by.approved / total : 0
  const maxTeams = ev.value?.maxTeams || ev.value?.MaxTeams || null
  const utilization = maxTeams ? Math.min(1, (by.approved || 0) / maxTeams) : null
  const ms = Array.isArray(matches.value) ? matches.value : []
  const now = Date.now()
  const matchTotal = ms.length
  const upcoming = ms.filter(m => {
    const t = m.matchTime || m.MatchTime
    return t ? new Date(t).getTime() > now : false
  }).length
  const past = matchTotal - upcoming
  let nextTime = null
  if (ms.length) {
    const sorted = [...ms].sort((a, b) => new Date(a.matchTime || a.MatchTime).getTime() - new Date(b.matchTime || b.MatchTime).getTime())
    const nxt = sorted.find(m => {
      const t = m.matchTime || m.MatchTime
      return t ? new Date(t).getTime() > now : false
    })
    nextTime = nxt ? (nxt.matchTime || nxt.MatchTime) : null
  }
  const byDay = {}
  for (const r of regs) {
    const dt = r.registrationTime || r.RegistrationTime
    if (!dt) continue
    const d = new Date(dt)
    const key = `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`
    byDay[key] = (byDay[key] || 0) + 1
  }
  const dayDist = Object.entries(byDay).sort((a,b) => a[0] < b[0] ? -1 : 1).slice(-7)
  return { total, by, approvedRate, maxTeams, utilization, matchTotal, upcoming, past, nextTime, dayDist }
})
</script>

<template>
  <PageHero :title="heroTitle" subtitle="查看赛事信息与报名队伍" icon="emoji_events">
    <template #actions>
      <v-btn color="white" variant="text" prepend-icon="chevron_left" class="mr-3 mb-3" @click="backToEvents">返回赛事列表</v-btn>
      <v-btn
        v-if="ev"
        color="white" variant="text"
        class="mr-3 mb-3"
        :to="'/events/' + (ev.id || ev.Id) + '/schedule'"
        prepend-icon="calendar_month"
      >赛程</v-btn>
      <v-btn
        v-if="ev"
        color="white" variant="text"
        class="mr-3 mb-3"
        :to="'/events/' + (ev.id || ev.Id) + '/bracket'"
        prepend-icon="account_tree"
      >赛程图</v-btn>
      <v-chip :color="registrationStatusColor" class="mb-3 mr-2" size="small">报名状态：{{ registrationStatusLabel }}</v-chip>
      <template v-if="registrationOpen()">
        <v-btn
          color="white"
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
        <v-btn
          color="white"
          prepend-icon="link"
          :loading="generatingInvite"
          class="mb-3"
          @click="onGenerateTeamInvite"
        >邀请队员加入我的战队</v-btn>
      </template>
      <template v-if="eventQqGroup">
        <v-btn color="white" variant="tonal" class="mb-3 ml-2" prepend-icon="group" @click="joinQqGroup">加入QQ群</v-btn>
      </template>
      <template v-if="eventRulesMarkdown">
        <v-btn color="white" variant="tonal" class="mb-3 ml-2" prepend-icon="rule" @click="rulesDialogOpen = true">查看赛事规则</v-btn>
      </template>
      </template>
    <template #media>
      <lottie-player src="/animations/Champion.json" autoplay loop style="width:220px;height:220px"></lottie-player>
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
            <v-img v-if="ev.logoUrl || ev.LogoUrl" :src="ev.logoUrl || ev.LogoUrl" alt="event logo" cover>
              <template #placeholder>
                <div class="img-skeleton"></div>
              </template>
            </v-img>
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
              导出报名CSV（每队空行分隔）
            </v-btn>
            <v-btn v-if="canManageEvent" color="primary" variant="tonal" prepend-icon="download" class="mr-3" @click="doExportLogosZip">
              导出战队Logo ZIP
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
      <v-card v-if="canManageEvent" class="mt-6">
        <v-card-title class="d-flex align-center">
          <v-icon icon="assignment" class="mr-2" />
          <span class="text-subtitle-1">报名自定义字段</span>
          <v-spacer />
        </v-card-title>
        <v-card-text>
          <div class="text-body-2">当前字段数：{{ regSchemaFields.length }}</div>
          <v-divider class="my-4" />
          <div class="text-subtitle-2 mb-2">角色人数限制（可选，空为无限制）</div>
          <v-row dense>
            <v-col cols="12" md="3">
              <v-text-field v-model="reqMinReg" type="number" label="监管者最少" />
            </v-col>
            <v-col cols="12" md="3">
              <v-text-field v-model="reqMaxReg" type="number" label="监管者最多" />
            </v-col>
            <v-col cols="12" md="3">
              <v-text-field v-model="reqMinSur" type="number" label="求生者最少" />
            </v-col>
            <v-col cols="12" md="3">
              <v-text-field v-model="reqMaxSur" type="number" label="求生者最多" />
            </v-col>
          </v-row>
          <div class="d-flex">
            <v-spacer />
            <v-btn color="primary" prepend-icon="save" @click="savePlayerTypeRequirements">保存角色限制</v-btn>
          </div>
        </v-card-text>
        <v-card-actions>
          <v-btn :loading="regSchemaEditorLoading" color="primary" prepend-icon="edit" @click="openSchemaEditor">编辑报名表单（JSON）</v-btn>
          <v-btn :loading="visualEditorLoading" color="secondary" class="ml-2" prepend-icon="view_quilt" @click="openVisualEditor">可视化编辑器</v-btn>
          <v-spacer />
          <v-btn v-if="regSchemaFields.length" variant="tonal" prepend-icon="assignment" @click="(async()=>{ await loadRegistrationFormSchema(); regFormPreviewMode = true; regFormDialogOpen = true })()">预览填写</v-btn>
        </v-card-actions>
      </v-card>
      <v-card v-if="canManageEvent" class="mt-6">
        <v-card-title class="d-flex align-center">
          <v-icon icon="insights" class="mr-2" />
          <span class="text-subtitle-1">数据分析</span>
          <v-spacer />
        </v-card-title>
        <v-card-text>
          <v-row>
            <v-col cols="12" sm="6" md="3">
              <v-card class="pa-3" variant="tonal">
                <div class="text-caption text-medium-emphasis">报名总数</div>
                <div class="text-h5">{{ analytics.total }}</div>
              </v-card>
            </v-col>
            <v-col cols="12" sm="6" md="3">
              <v-card class="pa-3" variant="tonal">
                <div class="text-caption text-medium-emphasis">已批准</div>
                <div class="text-h6">{{ analytics.by.approved }}</div>
                <v-progress-linear :model-value="(analytics.total ? (analytics.by.approved/analytics.total*100) : 0)" height="6" rounded color="green" class="mt-2" />
              </v-card>
            </v-col>
            <v-col cols="12" sm="6" md="3">
              <v-card class="pa-3" variant="tonal">
                <div class="text-caption text-medium-emphasis">待审核</div>
                <div class="text-h6">{{ analytics.by.pending }}</div>
                <v-progress-linear :model-value="(analytics.total ? (analytics.by.pending/analytics.total*100) : 0)" height="6" rounded color="orange" class="mt-2" />
              </v-card>
            </v-col>
            <v-col cols="12" sm="6" md="3">
              <v-card class="pa-3" variant="tonal">
                <div class="text-caption text-medium-emphasis">已拒绝</div>
                <div class="text-h6">{{ analytics.by.rejected }}</div>
                <v-progress-linear :model-value="(analytics.total ? (analytics.by.rejected/analytics.total*100) : 0)" height="6" rounded color="red" class="mt-2" />
              </v-card>
            </v-col>
          </v-row>
          <v-row class="mt-2">
            <v-col cols="12" sm="6" md="4">
              <div class="text-caption mb-1">审核通过率</div>
              <v-progress-linear :model-value="analytics.approvedRate*100" height="10" rounded color="green" />
              <div class="text-caption mt-1">{{ Math.round(analytics.approvedRate*1000)/10 }}%</div>
            </v-col>
            <v-col cols="12" sm="6" md="4">
              <div class="text-caption mb-1">席位利用率</div>
              <v-progress-linear :model-value="(analytics.utilization != null ? analytics.utilization*100 : 0)" height="10" rounded color="blue" />
              <div class="text-caption mt-1">{{ analytics.utilization != null ? (Math.round(analytics.utilization*1000)/10 + '%') : '未设置最大队伍数' }}</div>
            </v-col>
            <v-col cols="12" sm="6" md="4">
              <div class="text-caption mb-1">赛程</div>
              <div class="text-body-2">总数：{{ analytics.matchTotal }}，未开赛：{{ analytics.upcoming }}，已结束：{{ analytics.past }}</div>
              <div class="text-caption">下场比赛：{{ analytics.nextTime ? new Date(analytics.nextTime).toLocaleString() : '—' }}</div>
            </v-col>
          </v-row>
          <v-divider class="my-4" />
          <div class="text-subtitle-2 mb-2">报名时间分布（最近 7 天）</div>
          <v-row>
            <v-col v-for="([day,count]) in analytics.dayDist" :key="day" cols="12" sm="6" md="3">
              <v-card class="pa-3" variant="tonal">
                <div class="text-caption text-medium-emphasis">{{ day }}</div>
                <div class="text-h6">{{ count }}</div>
              </v-card>
            </v-col>
          </v-row>
        </v-card-text>
      </v-card>
      <template v-if="inviteInfo">
        <v-alert type="success" class="mt-4">
          <div class="d-flex align-center">
            <div class="flex-grow-1">邀请有效期至：{{ new Date(inviteInfo.expiresAt || inviteInfo.ExpiresAt).toLocaleString() }}</div>
            <v-btn variant="text" prepend-icon="content_copy" @click="copyInviteLink">复制邀请链接</v-btn>
          </div>
          <div class="mt-2 text-caption">{{ `${(typeof window !== 'undefined' && window.location) ? window.location.origin : ''}/join/${inviteInfo.token || inviteInfo.Token}` }}</div>
        </v-alert>
      </template>

      <v-card class="mt-6">
        <v-card-title class="d-flex align-center">
          <v-icon icon="shield_person" class="mr-2" />
          <span class="text-subtitle-1">赛事管理员</span>
          <v-spacer />
        </v-card-title>
        <v-card-text>
          <v-alert v-if="adminError" type="error" :text="adminError" class="mb-3" />
          <div class="text-body-2 mb-2">创建者：{{ creatorDisplay }}</div>
          <div class="text-body-2 text-medium-emphasis mb-2">已添加的管理员：</div>
          <v-progress-linear v-if="loadingAdmins" indeterminate color="primary" />
          <template v-else>
            <div v-if="!adminList || adminList.length === 0" class="text-body-2">暂无赛事管理员</div>
            <v-list v-else density="comfortable">
              <v-list-item v-for="u in adminList" :key="u.id || u.Id">
                <template #prepend>
                  <v-icon icon="person" class="mr-2" />
                </template>
                <v-list-item-title>{{ u.fullName || [u.firstName, u.lastName].filter(Boolean).join(' ') || u.email || (u.id || u.Id) }}</v-list-item-title>
                <v-list-item-subtitle>
                  <span v-if="u.email">{{ u.email }}</span>
                </v-list-item-subtitle>
                <template #append>
                  <v-btn
                    v-if="canManageAdmins && (u.id || u.Id) !== creatorUserId"
                    size="small"
                    color="error"
                    variant="text"
                    prepend-icon="delete"
                    :loading="removingAdminIds.has(u.id || u.Id)"
                    @click="onRemoveAdmin(u.id || u.Id)"
                  >移除</v-btn>
                </template>
              </v-list-item>
            </v-list>
          </template>
          <div v-if="canManageAdmins" class="mt-4 d-flex align-center">
            <v-text-field
              v-model="addUserId"
              class="mr-3"
              density="comfortable"
              label="用户ID"
              placeholder="输入要添加为管理员的用户ID"
              hide-details
            />
            <v-btn :loading="addingAdmin" color="primary" prepend-icon="person_add" @click="onAddAdmin">添加管理员</v-btn>
          </div>
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
                  <v-img v-if="championTeam.logoUrl || championTeam.LogoUrl" :src="championTeam.logoUrl || championTeam.LogoUrl" alt="champion logo" cover>
                    <template #placeholder>
                      <div class="img-skeleton"></div>
                    </template>
                  </v-img>
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
                          <span v-if="(p.playerType ?? p.PlayerType) != null" class="ml-2">角色类型：{{ playerTypeName(p.playerType ?? p.PlayerType) }}</span>
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
            <template v-if="canManageEvent">
              <v-switch
                v-model="notifyByEmail"
                color="primary"
                inset
                class="mb-4"
                :label="notifyByEmail ? '审批时将通过邮件通知队伍拥有者（每次扣减1积分）' : '审批时通过邮件通知队伍拥有者'"
              />
            </template>
            <div v-if="!registrations || registrations.length === 0" class="text-body-2">暂无报名队伍</div>
            <v-row v-else>
              <v-col v-for="r in registrations" :key="r.teamId" cols="12" sm="6" md="4" lg="3">
                <v-card>
                  <v-card-title class="d-flex align-center">
                    <router-link :to="{ name: 'team-detail', params: { id: r.teamId }, query: { eventId } }" class="d-inline-flex align-center text-decoration-none">
                      <v-avatar size="28" class="mr-2">
                        <v-img v-if="teamDetails[r.teamId]?.logoUrl || teamDetails[r.teamId]?.LogoUrl" :src="teamDetails[r.teamId]?.logoUrl || teamDetails[r.teamId]?.LogoUrl" alt="team logo" cover>
                          <template #placeholder>
                            <div class="img-skeleton"></div>
                          </template>
                        </v-img>
                        <v-icon v-else icon="group" size="22" />
                      </v-avatar>
                      <span class="text-truncate">{{ r.teamName }}</span>
                    </router-link>
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
              <v-btn
                v-if="canManageEvent && getRegisteredByUserId(r)"
                size="small"
                variant="text"
                color="secondary"
                :to="`/messages/${getRegisteredByUserId(r)}`"
                class="ml-1"
                prepend-icon="chat"
              >联系队长</v-btn>
              <template v-if="canManageEvent">
                <v-btn size="small" color="success" class="ml-2" prepend-icon="check_circle" :loading="updatingStatusIds.has(r.teamId)" :disabled="normalizeStatus(r.status) === 'Approved'" @click="approve(r.teamId)">
                  通过
                </v-btn>
                <v-btn size="small" color="error" class="ml-1" prepend-icon="cancel" :loading="updatingStatusIds.has(r.teamId)" :disabled="normalizeStatus(r.status) === 'Rejected'" @click="reject(r.teamId)">
                  拒绝
                </v-btn>
                <v-switch
                  v-model="disputeSwitchMap[r.teamId]"
                  class="ml-2"
                  color="error"
                  density="compact"
                  :loading="disputeTogglingIds.has(r.teamId)"
                  hide-details
                  :label="disputeSwitchMap[r.teamId] ? '纠纷：是' : '纠纷：否'"
                  @update:modelValue="() => onToggleDispute(r.teamId)"
                />
                
              </template>
            </div>
                    <div class="mt-2 d-flex align-center flex-wrap gap-2">
                      <v-chip v-if="r.teamHasDispute || r.TeamHasDispute" color="error" variant="tonal" size="small" prepend-icon="report">存在纠纷</v-chip>
                      
                    </div>
                    <div v-if="r.teamHasDispute || r.TeamHasDispute" class="mt-2">
                      <div class="text-caption text-medium-emphasis mb-1">纠纷已标记</div>
                      <div class="text-caption mt-1" v-if="r.teamCommunityPostId || r.TeamCommunityPostId">
                        <router-link :to="'/articles/' + (r.teamCommunityPostId || r.TeamCommunityPostId)" class="text-decoration-none">
                          关联帖子：{{ r.teamCommunityPostId || r.TeamCommunityPostId }}
                        </router-link>
                      </div>
                    </div>
                    <div v-if="teamDetails[r.teamId]" class="mt-2">
                      <v-divider class="mb-2" />
                      <div v-if="canManageEvent && regSchemaFields.length" class="mb-2">
                        <div class="text-caption mb-2">报名信息</div>
                        <v-list density="compact">
                          <v-list-item v-for="f in regSchemaFields" :key="f.id || f.Id">
                            <v-list-item-title>{{ f.label || f.Label }}</v-list-item-title>
                            <v-list-item-subtitle>{{ formatAnswer(r.teamId, f.id || f.Id, f.type || f.Type) }}</v-list-item-subtitle>
                          </v-list-item>
                        </v-list>
                      </div>
                      <div class="text-caption mb-2">队员列表</div>
                      <v-list density="compact">
                        <v-list-item v-for="p in teamPlayersForEvent(r.teamId)" :key="p.id || p.name">
                          <v-list-item-title>{{ p.name }}</v-list-item-title>
                          <v-list-item-subtitle>
                            类型：{{ playerTypeName(p.playerType || p.PlayerType) }} | GameID: {{ p.gameId || p.GameId || '—' }} | Rank: {{ p.gameRank || p.GameRank || '—' }}
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

      <v-dialog v-model="disputeDialogOpen" max-width="540">
        <v-card>
          <v-card-title>设置纠纷详情</v-card-title>
          <v-card-text>
            <div class="text-caption mb-2">必须绑定一个社区帖子才能标记为纠纷。</div>
            <v-textarea v-model="disputeDetailText" label="纠纷说明（主办方阐述理由，支持Markdown）" rows="5" auto-grow />
            <v-autocomplete
              v-model="selectedDisputeArticleId"
              v-model:search="disputeArticleSearch"
              :items="disputeArticleOptions"
              :loading="disputeArticleLoading"
              item-title="title"
              item-value="id"
              label="关联社区帖子（搜索后选择）"
              :no-filter="true"
              clearable
            />
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="disputeDialogOpen = false">取消</v-btn>
            <v-btn color="error" prepend-icon="report" @click="confirmSetDispute">确认标记纠纷</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

      <v-dialog v-model="visualEditorOpen" max-width="900">
        <v-card>
          <v-card-title>报名表可视化编辑器</v-card-title>
          <v-card-text>
            <v-progress-linear v-if="visualEditorLoading" indeterminate color="primary" class="mb-3" />
            <div>
              <v-row v-for="(f,i) in visualFields" :key="f.id || i" class="mb-2" dense>
                <v-col cols="12">
                  <v-card variant="tonal" class="pa-3">
                    <div class="d-flex align-center mb-2">
                      <div class="text-caption text-medium-emphasis">字段 {{ i+1 }}</div>
                      <v-spacer />
                      <v-btn icon="arrow_upward" variant="text" size="small" :disabled="i===0" @click="moveField(i,-1)" />
                      <v-btn icon="arrow_downward" variant="text" size="small" :disabled="i===visualFields.length-1" @click="moveField(i,1)" />
                      <v-btn icon="delete" variant="text" color="error" size="small" class="ml-1" @click="removeField(i)" />
                    </div>
                    <v-row dense>
                      <v-col cols="12" md="3">
                        <v-select v-model="f.type" :items="['text','textarea','select','checkbox','number','date','datetime']" label="类型" />
                      </v-col>
                      <v-col cols="12" md="4">
                        <v-text-field v-model="f.id" label="字段ID" />
                      </v-col>
                      <v-col cols="12" md="5">
                        <v-text-field v-model="f.label" label="显示名" />
                      </v-col>
                      <v-col cols="12" md="3">
                        <v-switch v-model="f.required" inset label="必填" />
                      </v-col>
                      <template v-if="f.type==='select'">
                        <v-col cols="12" md="6">
                          <v-textarea v-model="f.optionsText" label="选项（每行一个）" rows="4" auto-grow />
                        </v-col>
                        <v-col cols="12" md="6">
                          <v-select :items="String(f.optionsText||'').split('\n').map(s=>s.trim()).filter(Boolean)" v-model="f.default" label="默认选项（可选）" />
                        </v-col>
                      </template>
                      <template v-else-if="f.type==='checkbox'">
                        <v-col cols="12" md="6">
                          <v-switch v-model="f.default" inset label="默认是" />
                        </v-col>
                      </template>
                      <template v-else>
                        <v-col cols="12" md="6">
                          <v-text-field v-model="f.default" :type="(f.type==='number'?'number':(f.type==='date'?'date':(f.type==='datetime'?'datetime-local':'text')))" label="默认值（可选）" />
                        </v-col>
                      </template>
                    </v-row>
                  </v-card>
                </v-col>
              </v-row>
            </div>
            <div class="d-flex align-center mt-2">
              <v-select v-model="newFieldType" :items="['text','textarea','select','checkbox','number','date','datetime']" label="字段类型" class="mr-3" style="max-width:240px" />
              <v-btn color="secondary" prepend-icon="add" @click="addField()">添加字段</v-btn>
            </div>
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="visualEditorOpen=false">取消</v-btn>
            <v-btn :loading="visualEditorSaving" color="primary" @click="saveVisualEditor">保存</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

      <v-dialog v-model="rulesDialogOpen" max-width="760">
        <v-card>
          <v-card-title>赛事规则</v-card-title>
          <v-card-text>
            <v-alert v-if="ruleError" type="error" :text="ruleError" class="mb-3" />
            <v-progress-linear v-if="rulesLoading" indeterminate color="primary" class="mb-3" />
            <v-row class="mb-3" dense>
              <v-col cols="12" md="6">
                <v-select
                  v-model="selectedRuleRevisionId"
                  :items="(ruleRevisions || []).map(r => ({ title: `版本 V${r.version || r.Version}${(r.isPublished || r.IsPublished) ? '（已发布）' : ''}`, value: r.id || r.Id }))"
                  label="选择规则版本"
                  clearable
                  prepend-inner-icon="rule"
                />
              </v-col>
              <v-col cols="12" md="6" v-if="canManageEvent && selectedRuleRevisionId">
                <v-btn :loading="rulePublishLoading" color="primary" prepend-icon="publish" class="mt-1" @click="onPublishSelectedRule">发布为当前规则</v-btn>
              </v-col>
            </v-row>
            <div class="text-body-2 md-content" v-html="selectedRulesHtml"></div>
            <template v-if="canManageEvent">
              <v-divider class="my-4" />
              <div class="text-subtitle-2 mb-2">创建规则新版本</div>
              <v-textarea v-model="newRuleMarkdown" label="规则内容（Markdown）" rows="6" auto-grow />
              <v-text-field v-model="newRuleChangeNotes" class="mt-2" label="变更说明（可选）" />
              <div class="mt-2 d-flex">
                <v-spacer />
                <v-btn :loading="creatingRuleRevision" color="secondary" prepend-icon="add" @click="onCreateRuleRevision">创建新版本</v-btn>
              </div>
            </template>
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="rulesDialogOpen = false">关闭</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

      <v-dialog v-model="regSchemaEditorOpen" max-width="720">
        <v-card>
          <v-card-title>编辑报名表单</v-card-title>
          <v-card-text>
            <v-progress-linear v-if="regSchemaEditorLoading" indeterminate color="primary" class="mb-3" />
            <v-textarea v-model="regSchemaJsonText" rows="12" auto-grow label="Schema JSON" />
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="regSchemaEditorOpen = false">取消</v-btn>
            <v-btn :loading="regSchemaSaveLoading" color="primary" @click="saveSchemaEditor">保存</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

      <v-dialog v-model="regFormDialogOpen" max-width="760">
        <v-card>
          <v-card-title>填写报名信息</v-card-title>
          <v-card-text>
            <v-progress-linear v-if="regSchemaLoading" indeterminate color="primary" class="mb-3" />
            <v-row v-else dense>
              <v-col v-for="f in regSchemaFields" :key="f.id || f.Id" cols="12" md="6">
                <template v-if="(f.type || f.Type) === 'textarea'">
                  <v-textarea v-model="regAnswersMap[f.id || f.Id]" :label="f.label || f.Label" auto-grow />
                </template>
                <template v-else-if="(f.type || f.Type) === 'select'">
                  <v-select v-model="regAnswersMap[f.id || f.Id]" :label="f.label || f.Label" :items="(f.options || f.Options || [])" />
                </template>
                <template v-else-if="(f.type || f.Type) === 'checkbox'">
                  <v-checkbox v-model="regAnswersMap[f.id || f.Id]" :label="f.label || f.Label" />
                </template>
                <template v-else>
                  <v-text-field v-model="regAnswersMap[f.id || f.Id]" :label="f.label || f.Label" :type="((f.type || f.Type) === 'number' ? 'number' : ((f.type || f.Type) === 'date' ? 'date' : ((f.type || f.Type) === 'datetime' ? 'datetime-local' : 'text')))" />
                </template>
              </v-col>
            </v-row>
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="regFormDialogOpen = false; regFormPreviewMode = false">关闭</v-btn>
            <v-btn v-if="!regFormPreviewMode" :loading="regSubmitLoading" color="primary" @click="onSubmitRegFormAndRegister">提交并报名</v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>

      <v-dialog v-model="selectPlayersDialogOpen" max-width="760">
        <v-card>
          <v-card-title>选择参赛队员</v-card-title>
          <v-card-text>
            <v-alert v-if="selectionError" type="error" :text="selectionError" class="mb-3" />
            <div class="text-caption mb-2">监管者最多：{{ playerTypeRequirements.regulator.max ?? '无限制' }}；求生者最多：{{ playerTypeRequirements.survivor.max ?? '无限制' }}</div>
            <v-list density="comfortable">
              <v-list-item v-for="p in myTeamPlayers" :key="p.id || p.Id">
                <template #prepend>
                  <v-checkbox :model-value="selectedPlayerIdsSet.has(String(p.id || p.Id))" @update:modelValue="() => toggleSelectPlayer(p)" />
                </template>
                <v-list-item-title>{{ p.name || p.Name }}</v-list-item-title>
                <v-list-item-subtitle>类型：{{ playerTypeName(p.playerType || p.PlayerType) }} | ID：{{ p.gameId || p.GameId || '—' }} | 段位：{{ p.gameRank || p.GameRank || '—' }}</v-list-item-subtitle>
              </v-list-item>
            </v-list>
          </v-card-text>
          <v-card-actions>
            <v-btn variant="text" @click="selectPlayersDialogOpen=false">取消</v-btn>
            <v-spacer />
            <v-btn :loading="selectingPlayers" color="primary" @click="onConfirmSelectPlayersAndSubmit">确认</v-btn>
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

      <ResultDialog v-model="showSuccess" :type="'success'" :message="successMsg" />
      <ResultDialog v-model="errorOpen" :type="'error'" :message="actionError || uploadLogoError || teamLogoError" :details="errorDetails" />
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

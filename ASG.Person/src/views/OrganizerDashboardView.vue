<script setup>
import { ref, onMounted } from 'vue'
import { getRecruitments, createRecruitment, deleteRecruitment } from '../services/recruitments'
import { getMyEvents, getAllEvents } from '../services/events'
import { getMatches } from '../services/matches'
import { getApplicationsByRecruitment, approveApplication, rejectApplication, syncApplicationMatches } from '../services/applications'
import { notify } from '../stores/notify'
import { currentUser } from '../stores/auth'

const items = ref([])
const loading = ref(false)
const creating = ref(false)
const title = ref('')
const eventId = ref(null)
const positionType = ref('Commentator')
const payPerMatch = ref(100)
const slots = ref(1)
const description = ref('')
const matchPickerOpen = ref(false)
const matches = ref([])
const selectedMatchIds = ref([])
const loadingMatches = ref(false)
const applicationDialogOpen = ref(false)
const applications = ref([])
const loadingApplications = ref(false)
const currentRecruitment = ref(null)
const approveDialogOpen = ref(false)
const approvingApplication = ref(null)
const approvalMatchIds = ref([])
const candidateMatches = ref([])
const loadingCandidateMatches = ref(false)
const busyDialogOpen = ref(false)
const busyText = ref('')

async function load() {
  loading.value = true
  try { items.value = await getRecruitments({ includeClosed: true }) } finally { loading.value = false }
  try {
    eventsLoadError.value = ''
    myEvents.value = await getMyEvents()
    const roleName = currentUser.value?.roleName || currentUser.value?.RoleName
    if ((!myEvents.value || !myEvents.value.length) && (roleName === 'Admin' || roleName === 'SuperAdmin')) {
      myEvents.value = await getAllEvents()
    }
  } catch (e) {
    eventsLoadError.value = e?.payload?.message || e?.message || '加载所属赛事失败'
  }
}

async function onCreate() {
  creating.value = true
  try {
    const dto = { title: title.value, eventId: eventId.value, positionType: positionType.value, payPerMatch: Number(payPerMatch.value), slots: Number(slots.value), description: description.value, matchIds: selectedMatchIds.value }
    await createRecruitment(dto)
    notify({ text: '已创建招募任务', color: 'success' })
    title.value = ''
    eventId.value = null
    description.value = ''
    selectedMatchIds.value = []
    await load()
  } catch (e) {
    notify({ text: e?.payload?.message || e?.message || '创建失败', color: 'error' })
  } finally { creating.value = false }
}

async function onDelete(id) {
  try { await deleteRecruitment(id); await load() } catch (e) { notify({ text: e?.payload?.message || e?.message || '删除失败', color: 'error' }) }
}

const myEvents = ref([])
const eventsLoadError = ref('')
onMounted(load)

async function openMatchPicker() {
  if (!eventId.value) { notify({ text: '请先选择所属赛事', color: 'warning' }); return }
  matchPickerOpen.value = true
  loadingMatches.value = true
  try { matches.value = await getMatches({ eventId: eventId.value, page: 1, pageSize: 100 }) } catch (e) {} finally { loadingMatches.value = false }
}

function toggleSelectMatch(id) {
  const idx = selectedMatchIds.value.indexOf(id)
  if (idx >= 0) selectedMatchIds.value.splice(idx, 1)
  else selectedMatchIds.value.push(id)
}

async function openApplications(rec) {
  currentRecruitment.value = rec
  applicationDialogOpen.value = true
  loadingApplications.value = true
  try { applications.value = await getApplicationsByRecruitment(rec.id || rec.Id) } catch (e) { applications.value = []; notify({ text: e?.payload?.message || e?.message || '加载申请失败', color: 'error' }) } finally { loadingApplications.value = false }
}

async function openApprove(app) {
  approvingApplication.value = app
  const preset = currentRecruitment.value?.matchIds || currentRecruitment.value?.MatchIds || []
  if (preset && preset.length) {
    busyText.value = '正在审批并同步赛程...'
    busyDialogOpen.value = true
    try { await approveApplication(app.id || app.Id, {}); notify({ text: '已同意申请并自动同步赛程', color: 'success' }); await openApplications(currentRecruitment.value) } catch (e) { notify({ text: e?.payload?.message || e?.message || '审批失败', color: 'error' }) } finally { busyDialogOpen.value = false }
    return
  }
  approveDialogOpen.value = true
  approvalMatchIds.value = []
  loadingCandidateMatches.value = true
  try { candidateMatches.value = await getMatches({ eventId: currentRecruitment.value?.eventId || currentRecruitment.value?.EventId, page: 1, pageSize: 100 }) } catch (e) { candidateMatches.value = [] } finally { loadingCandidateMatches.value = false }
}

function toggleApprovalMatch(id) {
  const idx = approvalMatchIds.value.indexOf(id)
  if (idx >= 0) approvalMatchIds.value.splice(idx, 1)
  else approvalMatchIds.value.push(id)
}

async function confirmApprove() {
  if (!approvingApplication.value) return
  busyText.value = '正在审批并同步赛程...'
  busyDialogOpen.value = true
  try {
    await approveApplication(approvingApplication.value.id || approvingApplication.value.Id, {})
    if (approvalMatchIds.value && approvalMatchIds.value.length) {
      await syncApplicationMatches(approvingApplication.value.id || approvingApplication.value.Id, { matchIds: approvalMatchIds.value })
    }
    notify({ text: '已同意申请并同步赛程', color: 'success' })
    approveDialogOpen.value = false
    approvingApplication.value = null
    await openApplications(currentRecruitment.value)
  } catch (e) {
    notify({ text: e?.payload?.message || e?.message || '审批失败', color: 'error' })
  } finally {
    busyDialogOpen.value = false
  }
}

async function onReject(app) {
  busyText.value = '正在拒绝申请...'
  busyDialogOpen.value = true
  try {
    await rejectApplication(app.id || app.Id, {})
    notify({ text: '已拒绝申请', color: 'success' })
    await openApplications(currentRecruitment.value)
  } catch (e) {
    notify({ text: e?.payload?.message || e?.message || '拒绝失败', color: 'error' })
  } finally {
    busyDialogOpen.value = false
  }
}
</script>

<template>
  <v-container class="py-8 page-container">
    <div class="text-h5 mb-3">主办方任务管理</div>
    <v-card class="mb-6">
      <v-card-title>创建招募任务</v-card-title>
      <v-card-text>
        <v-row dense>
          <v-col cols="12" v-if="eventsLoadError">
            <v-alert type="warning" :text="eventsLoadError" />
          </v-col>
          <v-col cols="12" v-else-if="!myEvents.length">
            <v-alert type="info" text="尚未加载到所属赛事，请登录或在赛事系统中创建赛事。管理员可选择全部赛事。" />
          </v-col>
          <v-col cols="12" md="6"><v-text-field v-model="title" label="标题" /></v-col>
          <v-col cols="12" md="6">
            <v-select v-model="eventId" :items="myEvents.map(e => ({ title: e.name || e.Name, value: e.id || e.Id }))" label="所属赛事" prepend-inner-icon="emoji_events" />
          </v-col>
          <v-col cols="12" md="3"><v-select v-model="positionType" :items="['Commentator','Director','Referee']" label="职位" /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model.number="payPerMatch" type="number" min="0" label="每场单价" /></v-col>
          <v-col cols="12" md="3"><v-text-field v-model.number="slots" type="number" min="1" label="名额" /></v-col>
          <v-col cols="12"><v-textarea v-model="description" label="说明" rows="4" /></v-col>
          <v-col cols="12" class="d-flex align-center">
            <v-btn variant="tonal" prepend-icon="calendar_month" @click="openMatchPicker">选择赛程</v-btn>
            <v-chip class="ml-2" v-if="selectedMatchIds.length" label color="default" variant="tonal">已选 {{ selectedMatchIds.length }} 场</v-chip>
          </v-col>
        </v-row>
      </v-card-text>
      <v-card-actions>
        <v-btn :disabled="!eventId" :loading="creating" color="primary" prepend-icon="save" @click="onCreate">创建</v-btn>
      </v-card-actions>
    </v-card>

    <div class="text-subtitle-1 mb-2">已创建任务</div>
    <v-row dense>
      <v-col cols="12" v-for="it in items" :key="it.id">
        <v-card>
          <v-card-title>{{ it.title }}</v-card-title>
          <v-card-text>
            <div class="text-caption">{{ it.positionType }} • 每场 {{ it.payPerMatch }} 元 • 名额 {{ it.slots }}</div>
          </v-card-text>
        <v-card-actions>
          <router-link :to="{ name: 'recruitment-detail', params: { id: it.id } }"><v-btn variant="text" prepend-icon="visibility">查看</v-btn></router-link>
          <v-btn variant="text" prepend-icon="assignment" @click="openApplications(it)">查看申请</v-btn>
          <v-spacer />
          <v-btn color="error" variant="tonal" prepend-icon="delete" @click="onDelete(it.id)">删除</v-btn>
        </v-card-actions>
      </v-card>
    </v-col>
    </v-row>
  </v-container>

  <v-dialog v-model="matchPickerOpen" max-width="860">
    <v-card>
      <v-card-title>选择赛程</v-card-title>
      <v-card-text>
        <v-row dense v-if="loadingMatches"><v-col cols="12"><v-skeleton-loader type="table" /></v-col></v-row>
        <v-table v-else>
          <thead>
            <tr><th style="width:40px"></th><th>时间</th><th>主队</th><th>客队</th></tr>
          </thead>
          <tbody>
            <tr v-for="m in matches" :key="m.id || m.Id">
              <td><v-checkbox density="compact" :model-value="selectedMatchIds.includes(m.id || m.Id)" @update:modelValue="() => toggleSelectMatch(m.id || m.Id)" /></td>
              <td>{{ new Date(m.matchTime || m.MatchTime).toLocaleString() }}</td>
              <td>{{ m.homeTeamName || m.HomeTeamName }}</td>
              <td>{{ m.awayTeamName || m.AwayTeamName }}</td>
            </tr>
          </tbody>
        </v-table>
      </v-card-text>
      <v-card-actions>
        <v-btn variant="text" @click="matchPickerOpen = false">关闭</v-btn>
        <v-spacer />
        <v-chip v-if="selectedMatchIds.length" label color="default" variant="tonal">已选 {{ selectedMatchIds.length }} 场</v-chip>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-dialog v-model="applicationDialogOpen" max-width="860">
    <v-card>
      <v-card-title>申请列表</v-card-title>
      <v-card-text>
        <v-row dense v-if="loadingApplications"><v-col cols="12"><v-skeleton-loader type="table" /></v-col></v-row>
        <v-table v-else>
          <thead>
            <tr><th>申请人</th><th>备注</th><th>状态</th><th style="width:220px">操作</th></tr>
          </thead>
          <tbody>
            <tr v-for="a in applications" :key="a.id || a.Id">
              <td>{{ a.applicantUserName || a.ApplicantUserName || a.applicantUserId || a.ApplicantUserId }}</td>
              <td>{{ a.note || a.Note }}</td>
              <td>{{ a.status || a.Status }}</td>
              <td>
                <v-btn size="small" color="primary" variant="tonal" prepend-icon="check" class="mr-2" @click="openApprove(a)">同意</v-btn>
                <v-btn size="small" color="error" variant="tonal" prepend-icon="close" @click="onReject(a)">拒绝</v-btn>
              </td>
            </tr>
          </tbody>
        </v-table>
      </v-card-text>
      <v-card-actions>
        <v-btn variant="text" @click="applicationDialogOpen = false">关闭</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-dialog v-model="approveDialogOpen" max-width="860">
    <v-card>
      <v-card-title>选择同步的赛程</v-card-title>
      <v-card-text>
        <v-row dense v-if="loadingCandidateMatches"><v-col cols="12"><v-skeleton-loader type="table" /></v-col></v-row>
        <v-table v-else>
          <thead>
            <tr><th style="width:40px"></th><th>时间</th><th>主队</th><th>客队</th></tr>
          </thead>
          <tbody>
            <tr v-for="m in candidateMatches" :key="m.id || m.Id">
              <td><v-checkbox density="compact" :model-value="approvalMatchIds.includes(m.id || m.Id)" @update:modelValue="() => toggleApprovalMatch(m.id || m.Id)" /></td>
              <td>{{ new Date(m.matchTime || m.MatchTime).toLocaleString() }}</td>
              <td>{{ m.homeTeamName || m.HomeTeamName }}</td>
              <td>{{ m.awayTeamName || m.AwayTeamName }}</td>
            </tr>
          </tbody>
        </v-table>
      </v-card-text>
      <v-card-actions>
        <v-btn variant="text" @click="approveDialogOpen = false">取消</v-btn>
        <v-spacer />
        <v-btn color="primary" prepend-icon="done_all" @click="confirmApprove">确认同意并同步</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-dialog v-model="busyDialogOpen" persistent max-width="420">
    <v-card>
      <v-card-title>{{ busyText || '请稍候...' }}</v-card-title>
      <v-card-text>
        <div class="d-flex align-center">
          <v-progress-circular indeterminate color="primary" />
          <div class="ml-3">正在处理，请稍候...</div>
        </div>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

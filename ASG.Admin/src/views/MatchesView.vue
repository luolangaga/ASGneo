<script setup>
import { ref, reactive, onMounted, watch } from 'vue'
import { getMatches, getMatch, createMatch, updateMatch, deleteMatch } from '../services/matches'
import { getEvents, getEventRegistrations } from '../services/events'
import { notifySuccess, notifyError } from '../notify'

const loading = ref(false)
const errorMsg = ref('')
const items = ref([])
const page = ref(1)
const pageSize = ref(10)
const hasNext = ref(true)

const events = ref([])
const eventFilter = ref(null)

const createDialog = ref(false)
const editDialog = ref(false)
const editing = ref(null)
const teamOptions = ref([])

const form = reactive({
  eventId: null,
  homeTeamId: null,
  awayTeamId: null,
  matchTime: '',
  liveLink: '',
  commentator: '',
  director: '',
  referee: '',
})

function toLocalInput(dt) {
  if (!dt) return ''
  const d = new Date(dt)
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}
function fromLocalInput(str) {
  if (!str) return null
  const d = new Date(str)
  return d.toISOString()
}

function fmtDate(v) {
  if (!v) return ''
  const d = new Date(v)
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

onMounted(async () => {
  try { events.value = await getEvents() } catch {}
  await fetchData()
})

watch([eventFilter, page, pageSize], () => fetchData())

async function fetchTeamsForEvent(eventId) {
  teamOptions.value = []
  if (!eventId) return
  try {
    const regs = await getEventRegistrations(eventId)
    teamOptions.value = (regs || []).map(r => ({ text: r.TeamName || r.teamName, value: r.TeamId || r.teamId }))
  } catch (err) {
    console.warn('加载报名队伍失败', err)
  }
}

async function fetchData() {
  loading.value = true
  errorMsg.value = ''
  try {
    const res = await getMatches(eventFilter.value || null, page.value, pageSize.value)
    items.value = res || []
    hasNext.value = (res || []).length >= pageSize.value
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载失败'
  } finally { loading.value = false }
}

function openCreate() {
  Object.assign(form, { eventId: eventFilter.value || null, homeTeamId: null, awayTeamId: null, matchTime: '', liveLink: '', commentator: '', director: '', referee: '' })
  if (form.eventId) fetchTeamsForEvent(form.eventId)
  createDialog.value = true
}

function onEventChange(val) { fetchTeamsForEvent(val) }

async function submitCreate() {
  try {
    const dto = {
      EventId: form.eventId,
      HomeTeamId: form.homeTeamId,
      AwayTeamId: form.awayTeamId,
      MatchTime: fromLocalInput(form.matchTime),
      LiveLink: form.liveLink || null,
      CustomData: '{}',
      Commentator: form.commentator || null,
      Director: form.director || null,
      Referee: form.referee || null,
    }
    await createMatch(dto)
    notifySuccess('赛程创建成功')
    createDialog.value = false
    await fetchData()
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '创建失败')
  }
}

async function openEdit(item) {
  try {
    const id = item.Id || item.id
    const detail = await getMatch(id)
    editing.value = detail
    Object.assign(form, {
      eventId: detail.EventId || detail.eventId,
      homeTeamId: detail.HomeTeamId || detail.homeTeamId,
      awayTeamId: detail.AwayTeamId || detail.awayTeamId,
      matchTime: toLocalInput(detail.MatchTime || detail.matchTime),
      liveLink: detail.LiveLink || detail.liveLink || '',
      commentator: detail.Commentator || detail.commentator || '',
      director: detail.Director || detail.director || '',
      referee: detail.Referee || detail.referee || '',
    })
    await fetchTeamsForEvent(form.eventId)
    editDialog.value = true
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '加载详情失败')
  }
}

async function submitEdit() {
  try {
    const id = editing.value?.Id || editing.value?.id
    if (!id) throw new Error('缺少赛程ID')
    const dto = {
      MatchTime: form.matchTime ? fromLocalInput(form.matchTime) : null,
      LiveLink: form.liveLink || null,
      CustomData: '{}',
      Commentator: form.commentator || null,
      Director: form.director || null,
      Referee: form.referee || null,
    }
    await updateMatch(id, dto)
    notifySuccess('赛程更新成功')
    editDialog.value = false
    await fetchData()
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '更新失败')
  }
}

async function onDelete(item) {
  if (!confirm(`删除赛程 ${item.HomeTeamName || item.homeTeamName} vs ${item.AwayTeamName || item.awayTeamName}?`)) return
  try {
    const id = item.Id || item.id
    await deleteMatch(id)
    notifySuccess('删除成功')
    await fetchData()
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '删除失败')
  }
}
</script>

<template>
  <v-container class="py-6" style="max-width: 1200px">
    <div class="d-flex align-center mb-4">
      <div class="text-h5">比赛管理</div>
      <v-spacer></v-spacer>
      <v-select class="mr-2" density="compact" style="max-width: 280px" :items="events.map(e => ({ text: e.Name || e.name, value: e.Id || e.id }))" v-model="eventFilter" label="筛选赛事" clearable />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">创建赛程</v-btn>
    </div>

    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />

    <v-table class="elevation-1">
      <thead>
        <tr>
          <th>赛事</th>
          <th>对阵</th>
          <th>时间</th>
          <th>直播链接</th>
          <th>操作</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="m in items" :key="m.Id || m.id">
          <td>{{ m.EventName || m.eventName }}</td>
          <td>{{ m.HomeTeamName || m.homeTeamName }} vs {{ m.AwayTeamName || m.awayTeamName }}</td>
          <td>{{ fmtDate(m.MatchTime || m.matchTime) }}</td>
          <td><a v-if="m.LiveLink || m.liveLink" :href="m.LiveLink || m.liveLink" target="_blank">{{ m.LiveLink || m.liveLink }}</a><span v-else class="text-medium-emphasis">—</span></td>
          <td>
            <v-btn variant="text" prepend-icon="mdi-pencil" @click="openEdit(m)">编辑</v-btn>
            <v-btn color="error" variant="text" prepend-icon="mdi-delete" @click="onDelete(m)">删除</v-btn>
          </td>
        </tr>
      </tbody>
    </v-table>

    <div class="d-flex align-center mt-2">
      <v-btn class="mr-2" :disabled="page<=1" @click="page=page-1" prepend-icon="mdi-chevron-left">上一页</v-btn>
      <div>第 {{ page }} 页</div>
      <v-spacer></v-spacer>
      <v-select :items="[5,10,20,50]" v-model="pageSize" density="compact" style="max-width: 120px" />
      <v-btn class="ml-2" :disabled="!hasNext" @click="page=page+1" append-icon="mdi-chevron-right">下一页</v-btn>
    </div>

    <!-- 创建赛程 -->
    <v-dialog v-model="createDialog" max-width="720">
      <v-card>
        <v-card-title>创建赛程</v-card-title>
        <v-card-text>
          <v-select :items="events.map(e => ({ text: e.Name || e.name, value: e.Id || e.id }))" v-model="form.eventId" label="所属赛事" @update:model-value="onEventChange" />
          <div class="d-flex flex-wrap gap-2">
            <v-select :items="teamOptions" v-model="form.homeTeamId" label="主队" style="max-width: 300px" />
            <v-select :items="teamOptions" v-model="form.awayTeamId" label="客队" style="max-width: 300px" />
          </div>
          <v-text-field v-model="form.matchTime" type="datetime-local" label="比赛时间" />
          <v-text-field v-model="form.liveLink" label="直播链接(可选)" />
          <div class="d-flex flex-wrap gap-2">
            <v-text-field v-model="form.commentator" label="解说(可选)" style="max-width: 220px" />
            <v-text-field v-model="form.director" label="导播(可选)" style="max-width: 220px" />
            <v-text-field v-model="form.referee" label="裁判(可选)" style="max-width: 220px" />
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn variant="text" @click="createDialog=false">取消</v-btn>
          <v-btn color="primary" @click="submitCreate">保存</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- 编辑赛程 -->
    <v-dialog v-model="editDialog" max-width="720">
      <v-card>
        <v-card-title>编辑赛程</v-card-title>
        <v-card-text>
          <v-select :items="events.map(e => ({ text: e.Name || e.name, value: e.Id || e.id }))" v-model="form.eventId" label="所属赛事" disabled />
          <div class="d-flex flex-wrap gap-2">
            <v-select :items="teamOptions" v-model="form.homeTeamId" label="主队" style="max-width: 300px" disabled />
            <v-select :items="teamOptions" v-model="form.awayTeamId" label="客队" style="max-width: 300px" disabled />
          </div>
          <v-text-field v-model="form.matchTime" type="datetime-local" label="比赛时间" />
          <v-text-field v-model="form.liveLink" label="直播链接(可选)" />
          <div class="d-flex flex-wrap gap-2">
            <v-text-field v-model="form.commentator" label="解说(可选)" style="max-width: 220px" />
            <v-text-field v-model="form.director" label="导播(可选)" style="max-width: 220px" />
            <v-text-field v-model="form.referee" label="裁判(可选)" style="max-width: 220px" />
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn variant="text" @click="editDialog=false">取消</v-btn>
          <v-btn color="primary" @click="submitEdit">保存</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>



<style scoped>
.gap-2 { gap: 8px; }
</style>
<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { getMyEvents, updateEvent, deleteEvent } from '../services/events'
import { renderMarkdown } from '../utils/markdown'

const router = useRouter()

const loading = ref(false)
const savingId = ref(null)
const deletingId = ref(null)
const errorMsg = ref('')
const myEvents = ref([])
const expandedMap = ref({})

const stats = computed(() => {
  const list = Array.isArray(myEvents.value) ? myEvents.value : []
  const totalEvents = list.length
  const activeRegistrationEvents = list.filter(e => (e.status ?? e.Status) === 1).length
  const ongoingEvents = list.filter(e => (e.status ?? e.Status) === 3).length
  const completedEvents = list.filter(e => (e.status ?? e.Status) === 4).length
  const nowTs = Date.now()
  const upcomingEvents = list.filter(e => {
    const t = e.competitionStartTime ?? e.CompetitionStartTime
    return t ? new Date(t).getTime() > nowTs : false
  }).length
  let totalRegistrations = 0
  let approvedRegistrations = 0
  let pendingRegistrations = 0
  let rejectedRegistrations = 0
  let cancelledRegistrations = 0
  const perEvents = list.map(e => {
    const regs = Array.isArray(e.registeredTeams) ? e.registeredTeams : Array.isArray(e.RegisteredTeams) ? e.RegisteredTeams : []
    const registeredCount = e.registeredTeamsCount ?? e.RegisteredTeamsCount ?? regs.length
    totalRegistrations += registeredCount
    const approved = regs.filter(r => (r.status ?? r.Status) === 3).length
    const pending = regs.filter(r => (r.status ?? r.Status) === 0).length
    const rejected = regs.filter(r => (r.status ?? r.Status) === 5).length
    const cancelled = regs.filter(r => (r.status ?? r.Status) === 4).length
    approvedRegistrations += approved
    pendingRegistrations += pending
    rejectedRegistrations += rejected
    cancelledRegistrations += cancelled
    const max = e.maxTeams ?? e.MaxTeams ?? null
    const util = max ? Math.min(1, (approved || 0) / max) : null
    return {
      id: e.id ?? e.Id,
      name: e.name ?? e.Name,
      status: e.status ?? e.Status,
      registrationStartTime: e.registrationStartTime ?? e.RegistrationStartTime,
      registrationEndTime: e.registrationEndTime ?? e.RegistrationEndTime,
      competitionStartTime: e.competitionStartTime ?? e.CompetitionStartTime,
      competitionEndTime: e.competitionEndTime ?? e.CompetitionEndTime,
      registeredCount,
      approved,
      pending,
      rejected,
      cancelled,
      maxTeams: max,
      utilization: util,
    }
  })
  const approvalRate = totalRegistrations > 0 ? approvedRegistrations / totalRegistrations : 0
  return { totalEvents, activeRegistrationEvents, upcomingEvents, ongoingEvents, completedEvents, totalRegistrations, approvedRegistrations, pendingRegistrations, rejectedRegistrations, cancelledRegistrations, approvalRate, perEvents }
})

function fmtPercent(p) {
  const v = Number(p || 0)
  return `${Math.round(v * 1000) / 10}%`
}

const statusMeta = {
  0: { label: '草稿', color: 'grey' },
  1: { label: '报名中', color: 'green' },
  2: { label: '报名结束', color: 'orange' },
  3: { label: '进行中', color: 'blue' },
  4: { label: '已结束', color: 'purple' },
  5: { label: '已取消', color: 'red' },
}

function statusLabel(s) { return statusMeta[s]?.label || String(s) }
function statusColor(s) { return statusMeta[s]?.color || 'default' }

function toIso(dt) {
  if (!dt) return null
  const d = typeof dt === 'string' ? new Date(dt) : dt
  return new Date(d).toISOString()
}

async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    myEvents.value = await getMyEvents()
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载我的赛事失败'
  } finally {
    loading.value = false
  }
}

onMounted(load)

function goCreateEvent() {
  router.push('/events/create')
}

function goEditEvent(id) {
  router.push(`/events/${id}/edit`)
}

function toMd(s) {
  return renderMarkdown(s || '')
}

function isExpanded(id) {
  return !!expandedMap.value[id]
}
function toggleExpanded(id) {
  expandedMap.value[id] = !expandedMap.value[id]
}

async function setStatus(ev, newStatus) {
  savingId.value = ev.id
  errorMsg.value = ''
  try {
    const dto = {
      name: ev.name,
      description: ev.description,
      registrationStartTime: toIso(ev.registrationStartTime),
      registrationEndTime: toIso(ev.registrationEndTime),
      competitionStartTime: toIso(ev.competitionStartTime),
      competitionEndTime: ev.competitionEndTime ? toIso(ev.competitionEndTime) : null,
      maxTeams: ev.maxTeams ?? null,
      status: newStatus,
    }
    await updateEvent(ev.id, dto)
    await load()
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '更新赛事状态失败'
  } finally {
    savingId.value = null
  }
}

async function onDeleteEvent(ev) {
  if (!ev?.id) return
  const ok = window.confirm(`确定要删除赛事“${ev.name}”吗？此操作不可恢复。`)
  if (!ok) return
  deletingId.value = ev.id
  errorMsg.value = ''
  try {
    await deleteEvent(ev.id)
    await load()
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '删除赛事失败'
  } finally {
    deletingId.value = null
  }
}
</script>

<template>
  <v-container class="py-8">
    <div class="d-flex align-center mb-4">
      <v-card-title class="px-0">我的赛事</v-card-title>
      <v-spacer />
      <v-btn color="primary" @click="goCreateEvent" prepend-icon="add">创建赛事</v-btn>
    </div>

    <v-card class="mb-6">
      <v-card-title>数据概览</v-card-title>
      <v-card-text>
        <v-row>
          <v-col cols="12" sm="6" md="3">
            <v-card class="pa-3" variant="tonal">
              <div class="text-caption text-medium-emphasis">赛事数量</div>
              <div class="text-h5">{{ stats.totalEvents }}</div>
            </v-card>
          </v-col>
          <v-col cols="12" sm="6" md="3">
            <v-card class="pa-3" variant="tonal">
              <div class="text-caption text-medium-emphasis">报名中的赛事</div>
              <div class="text-h5">{{ stats.activeRegistrationEvents }}</div>
            </v-card>
          </v-col>
          <v-col cols="12" sm="6" md="3">
            <v-card class="pa-3" variant="tonal">
              <div class="text-caption text-medium-emphasis">总报名数</div>
              <div class="text-h5">{{ stats.totalRegistrations }}</div>
            </v-card>
          </v-col>
          <v-col cols="12" sm="6" md="3">
            <v-card class="pa-3" variant="tonal">
              <div class="text-caption text-medium-emphasis">审核通过率</div>
              <div class="text-h5">{{ fmtPercent(stats.approvalRate) }}</div>
            </v-card>
          </v-col>
        </v-row>
        <v-row class="mt-2">
          <v-col cols="12" sm="6" md="3">
            <v-chip color="blue" variant="tonal">进行中：{{ stats.ongoingEvents }}</v-chip>
          </v-col>
          <v-col cols="12" sm="6" md="3">
            <v-chip color="purple" variant="tonal">已结束：{{ stats.completedEvents }}</v-chip>
          </v-col>
          <v-col cols="12" sm="6" md="3">
            <v-chip color="teal" variant="tonal">即将开始：{{ stats.upcomingEvents }}</v-chip>
          </v-col>
          <v-col cols="12" sm="6" md="3">
            <v-chip color="grey" variant="tonal">待审核报名：{{ stats.pendingRegistrations }}</v-chip>
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>

    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />

    <v-progress-linear v-if="loading" indeterminate color="primary" />

    <v-card v-if="!loading && myEvents && myEvents.length > 0" class="mb-6">
      <v-card-title>赛事报名进度</v-card-title>
      <v-card-text>
        <v-list density="comfortable">
          <v-list-item v-for="it in stats.perEvents" :key="it.id">
            <template #prepend>
              <v-icon icon="event" class="mr-2" />
            </template>
            <v-list-item-title>{{ it.name }}</v-list-item-title>
            <v-list-item-subtitle>
              <span v-if="it.maxTeams != null">席位利用率：{{ fmtPercent(it.utilization || 0) }}</span>
              <span v-else>通过/报名：{{ it.approved }} / {{ it.registeredCount }}</span>
            </v-list-item-subtitle>
            <template #append>
              <div style="min-width: 240px">
                <v-progress-linear :model-value="(it.maxTeams != null ? (it.utilization || 0) * 100 : (it.registeredCount ? (it.approved / it.registeredCount) * 100 : 0))" height="8" rounded color="green" />
              </div>
            </template>
          </v-list-item>
        </v-list>
      </v-card-text>
    </v-card>

    <v-row v-if="!loading && myEvents && myEvents.length > 0">
      <v-col v-for="ev in myEvents" :key="ev.id" cols="12" md="6">
        <v-card>
          <v-card-title class="d-flex align-center">
            <span class="me-2">{{ ev.name }}</span>
            <v-chip :color="statusColor(ev.status)" size="small" class="ms-1">{{ statusLabel(ev.status) }}</v-chip>
          </v-card-title>
          <v-card-subtitle>
            报名：{{ new Date(ev.registrationStartTime).toLocaleString() }} - {{ new Date(ev.registrationEndTime).toLocaleString() }}
          </v-card-subtitle>
          <v-card-subtitle>
            比赛：{{ new Date(ev.competitionStartTime).toLocaleString() }}<span v-if="ev.competitionEndTime"> - {{ new Date(ev.competitionEndTime).toLocaleString() }}</span>
          </v-card-subtitle>
          <v-card-text>
            <div v-if="ev.description" :class="['md-content', { 'md-truncate': !isExpanded(ev.id) }]" v-html="toMd(ev.description)"></div>
            <div v-else class="text-medium-emphasis">暂无赛事描述</div>
            <div v-if="(ev.description?.length || 0) > 220" class="mt-1">
              <v-btn size="x-small" variant="text" @click="toggleExpanded(ev.id)">{{ isExpanded(ev.id) ? '收起' : '展开' }}</v-btn>
            </div>
          </v-card-text>
          <v-card-actions>
            <v-btn variant="text" :to="`/events/${ev.id}`" prepend-icon="visibility">查看详情</v-btn>
            <v-btn variant="text" @click="goEditEvent(ev.id)" prepend-icon="edit">编辑</v-btn>
            <v-spacer />
            <v-btn
              :loading="deletingId === ev.id"
              color="error"
              variant="text"
              @click="onDeleteEvent(ev)"
              prepend-icon="delete"
            >删除</v-btn>
            <v-btn
              v-if="ev.status !== 1"
              :loading="savingId === ev.id"
              color="green"
              @click="setStatus(ev, 1)"
              prepend-icon="lock_open"
            >开启报名</v-btn>
            <v-btn
              v-else
              :loading="savingId === ev.id"
              color="orange"
              @click="setStatus(ev, 2)"
              prepend-icon="lock"
            >关闭报名</v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>

<style scoped>
</style>

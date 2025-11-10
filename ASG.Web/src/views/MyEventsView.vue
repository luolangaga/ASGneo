<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getMyEvents, updateEvent } from '../services/events'
import { renderMarkdown } from '../utils/markdown'

const router = useRouter()

const loading = ref(false)
const savingId = ref(null)
const errorMsg = ref('')
const myEvents = ref([])
const expandedMap = ref({})

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
</script>

<template>
  <v-container class="py-8">
    <div class="d-flex align-center mb-4">
      <v-card-title class="px-0">我的赛事</v-card-title>
      <v-spacer />
      <v-btn color="primary" @click="goCreateEvent" prepend-icon="add">创建赛事</v-btn>
    </div>

    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />

    <v-progress-linear v-if="loading" indeterminate color="primary" />

    <v-row v-else>
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
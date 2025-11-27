<script setup>
import { ref, onMounted } from 'vue'
import { apiFetch } from '../services/api'
import { getEvent } from '../services/events'

const items = ref([])
const loading = ref(false)
const eventMap = ref({}) // eventId -> EventDto

function formatDateTime(dt) {
  if (!dt) return ''
  try { return new Date(dt).toLocaleString([], { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' }) } catch { return String(dt) }
}

async function ensureEvent(id) {
  if (!id || eventMap.value[id]) return
  try { eventMap.value[id] = await getEvent(id) } catch {}
}

async function load() {
  loading.value = true
  try {
    const list = await apiFetch('/Applications/my')
    items.value = Array.isArray(list) ? list : []
    const ids = [...new Set(items.value.map(x => x.eventId || x.EventId).filter(Boolean))]
    await Promise.all(ids.map(ensureEvent))
  } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <v-container class="py-8 page-container">
    <div class="text-h5 mb-3">我的申请</div>
    <v-row dense>
      <v-col cols="12" v-for="it in items" :key="it.id">
        <v-card>
          <v-card-item>
            <div class="d-flex align-center">
              <v-avatar size="36" class="mr-3" v-if="eventMap[it.eventId]?.logoUrl">
                <v-img :src="eventMap[it.eventId].logoUrl" alt="event logo" />
              </v-avatar>
              <v-avatar size="36" class="mr-3" v-else>
                <v-icon icon="emoji_events" />
              </v-avatar>
              <div>
                <div class="text-subtitle-1">{{ it.taskTitle }}</div>
                <div class="text-caption">{{ it.eventName }}</div>
              </div>
              <v-spacer />
              <div class="text-caption">状态 {{ it.status }}</div>
            </div>
          </v-card-item>
          <v-divider />
          <v-card-text>
            <div>
              <v-icon size="18" icon="schedule" class="mr-1" />
              <span>{{ formatDateTime(it.nextMatchTime || it.NextMatchTime) || '未关联比赛' }}</span>
            </div>
            <div class="mt-1">
              <v-icon size="18" icon="place" class="mr-1" />
              <span>{{ it.venue || it.Venue || '场地待定' }}</span>
            </div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>
    <v-row v-if="!items.length && !loading"><v-col cols="12"><v-alert type="info" text="暂无申请记录" /></v-col></v-row>
  </v-container>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { listNotifications, markRead, markAllRead } from '../services/notifications'
import { refreshUnreadCount } from '../stores/notifications'
import { notify } from '../stores/notify'

const items = ref([])
const loading = ref(false)

async function load() {
  loading.value = true
  try { items.value = await listNotifications() } finally { loading.value = false }
}

async function read(id) {
  try { await markRead(id); await load(); await refreshUnreadCount() } catch (e) { notify({ text: e?.payload?.message || e?.message || '操作失败', color: 'error' }) }
}

async function readAll() {
  try { await markAllRead(); await load(); await refreshUnreadCount() } catch (e) { notify({ text: e?.payload?.message || e?.message || '操作失败', color: 'error' }) }
}

onMounted(load)
</script>

<template>
  <v-container class="py-8 page-container">
    <div class="d-flex align-center mb-3">
      <div class="text-h5">通知中心</div>
      <v-spacer />
      <v-btn variant="text" prepend-icon="done_all" @click="readAll">全部已读</v-btn>
    </div>
    <v-card>
      <v-list>
        <v-list-item v-for="it in items" :key="it.id || it.Id">
          <template #prepend>
            <v-icon :icon="(it.type || it.Type) === 'recruitment.application' ? 'how_to_reg' : (it.type || it.Type) === 'article.comment' ? 'chat' : 'notifications'" />
          </template>
          <v-list-item-title>{{ it.type || it.Type }}</v-list-item-title>
          <v-list-item-subtitle class="text-caption">{{ new Date(it.createdAt || it.CreatedAt).toLocaleString() }}</v-list-item-subtitle>
          <template #append>
            <v-btn v-if="!(it.isRead || it.IsRead)" size="small" variant="text" @click="read(it.id || it.Id)">标为已读</v-btn>
          </template>
        </v-list-item>
      </v-list>
    </v-card>
    <v-row v-if="!items.length && !loading"><v-col cols="12"><v-alert type="info" text="暂无通知" /></v-col></v-row>
  </v-container>
</template>
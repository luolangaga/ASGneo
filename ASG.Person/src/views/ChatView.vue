<script setup>
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { getConversations, getConversationMessages } from '../services/messages'
import { sendDirectMessage } from '../services/realtime'
import { currentUser } from '../stores/auth'
import { notify } from '../stores/notify'

const route = useRoute()
const targetUserId = route.params.userId || ''
const conversations = ref([])
const currentConversationId = ref(null)
const messages = ref([])
const input = ref('')
const loading = ref(false)

async function load() {
  loading.value = true
  try {
    conversations.value = await getConversations()
    if (targetUserId) {
      const byPeer = conversations.value.find(c => (c.peerUserId || c.PeerUserId) === targetUserId)
      currentConversationId.value = byPeer?.id || byPeer?.Id || null
    }
    if (!currentConversationId.value && conversations.value.length) currentConversationId.value = conversations.value[0].id || conversations.value[0].Id
    if (currentConversationId.value) messages.value = await getConversationMessages(currentConversationId.value)
  } finally { loading.value = false }
}

async function send() {
  const text = String(input.value || '').trim()
  if (!text) return
  try {
    if (targetUserId) await sendDirectMessage(targetUserId, text)
    else notify({ text: '请选择聊天对象', color: 'warning' })
    input.value = ''
  } catch (e) { notify({ text: e?.payload?.message || e?.message || '发送失败', color: 'error' }) }
}

onMounted(load)
</script>

<template>
  <v-container class="py-8 page-container">
    <div class="text-h5 mb-3">聊天</div>
    <v-row>
      <v-col cols="12" md="3">
        <v-list>
          <v-list-item v-for="c in conversations" :key="c.id || c.Id" @click="currentConversationId = c.id || c.Id; load()">
            <v-list-item-title>{{ c.peerName || c.PeerName || '对话' }}</v-list-item-title>
            <v-list-item-subtitle>{{ c.lastMessage || c.LastMessage }}</v-list-item-subtitle>
          </v-list-item>
        </v-list>
      </v-col>
      <v-col cols="12" md="9">
        <v-card>
          <v-card-text style="height: 380px; overflow: auto;">
            <div v-for="m in messages" :key="m.id || m.Id" class="mb-2" :class="(m.senderUserId || m.SenderUserId) === currentUser?.id ? 'text-right' : ''">
              <div class="text-caption">{{ new Date(m.createdAt || m.CreatedAt).toLocaleString() }}</div>
              <div class="pa-2 rounded" :style="{ background: (m.senderUserId || m.SenderUserId) === currentUser?.id ? '#E3F2FD' : '#F5F5F5' }">{{ m.content || m.Content }}</div>
            </div>
          </v-card-text>
          <v-divider />
          <v-card-actions>
            <v-text-field v-model="input" class="flex-grow-1" label="输入消息" @keyup.enter="send" />
            <v-btn color="primary" prepend-icon="send" @click="send">发送</v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>
<script setup>
import { ref, onMounted, onUnmounted, watch, computed, nextTick } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getConversations, getConversationMessages, sendMessageToUser, blockUser, unblockUser, checkBlock, clearConversation } from '../services/messages'
import realtime, { enableRealtime, disableRealtimeGlobal } from '../services/realtime'
import { searchUsersByName, getUser } from '../services/user'
import { currentUser } from '../stores/auth'
import { notify } from '../stores/notify'
import { refreshUnreadCount } from '../stores/notifications'
import { uploadImage } from '../services/files'
import NotificationsView from './NotificationsView.vue'
import { useDisplay } from 'vuetify'

const route = useRoute()
const router = useRouter()
const meId = computed(() => currentUser.value?.id || currentUser.value?.Id || '')
const targetUserId = ref(route.params.userId || '')
const conversations = ref([])
const currentConversationId = ref(null)
const messages = ref([])
const input = ref('')
const loading = ref(false)
const loadingConversations = ref(false)
const searchText = ref('')
const searchResults = ref([])
const blockState = ref({ youBlocked: false, blockedYou: false })
const peerProfile = ref(null)
const messagesContainer = ref(null)
const imageUploading = ref(false)
const imageInputRef = ref(null)
const emojiOpen = ref(false)
const { smAndDown } = useDisplay()
const leftDrawerOpen = ref(true)
const profileDrawerOpen = ref(true)
const imagePreviewOpen = ref(false)
const imagePreviewSrc = ref('')

const emojis = ['😀','😁','😂','🤣','😊','😍','😘','😎','🤔','🙃','😉','😇','🥳','😢','😭','😡','👍','👎','🙏','👏','💪','🎉','🔥','✨','🌟','💯']

async function loadConversations() {
  loadingConversations.value = true
  try {
    conversations.value = await getConversations()
  } finally {
    loadingConversations.value = false
  }
}

async function selectConversation(id) {
  currentConversationId.value = id
  const peerId = getCurrentPeerId()
  if (peerId) router.replace({ name: 'messages', params: { userId: peerId } })
  await refreshMessages()
  if (id && String(id).toLowerCase() === '00000000-0000-0000-0000-000000000000') {
    await refreshUnreadCount()
    try { window.dispatchEvent(new CustomEvent('asg:conversations-changed')) } catch {}
  } else {
    try { await realtime.markConversationRead(id) } catch {}
    await loadConversations()
    try { window.dispatchEvent(new CustomEvent('asg:conversations-changed')) } catch {}
  }
}

async function refreshMessages() {
  loading.value = true
  try {
    if (!currentConversationId.value) return
    const peer = getCurrentPeerId()
    if (peer === 'system') { messages.value = []; return }
    messages.value = await getConversationMessages(currentConversationId.value)
    scrollToBottom()
  } finally { loading.value = false }
}

function scrollToBottom() {
  nextTick(() => {
    if (messagesContainer.value) {
      messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight
    }
  })
}

async function refreshBlockState() {
  const peerId = getCurrentPeerId()
  if (peerId && peerId !== 'system') {
    blockState.value = await checkBlock(peerId)
  } else {
    blockState.value = { youBlocked: false, blockedYou: false }
  }
}

async function refreshPeerProfile() {
  const peerId = getCurrentPeerId()
  if (peerId && peerId !== 'system') {
    try {
      peerProfile.value = await getUser(peerId)
    } catch {
      peerProfile.value = null
    }
  } else {
    peerProfile.value = null
  }
}

function getCurrentPeerId() {
  const c = conversations.value.find(x => (x.id || x.Id) === currentConversationId.value)
  return c ? (c.peerUserId || c.PeerUserId) : targetUserId.value
}

function getCurrentPeerName() {
  const c = conversations.value.find(x => (x.id || x.Id) === currentConversationId.value)
  return c ? (c.peerName || c.PeerName || '对方') : '对方'
}

const isSystemSelected = computed(() => getCurrentPeerId() === 'system')
watch(smAndDown, (v) => {
  leftDrawerOpen.value = !v
  profileDrawerOpen.value = !v
}, { immediate: true })

function toggleConversations() { leftDrawerOpen.value = !leftDrawerOpen.value }
function toggleProfile() { profileDrawerOpen.value = !profileDrawerOpen.value }

async function send() {
  const text = String(input.value || '').trim()
  if (!text) return
  await sendContent(text)
}

async function sendContent(content) {
  const peerId = getCurrentPeerId()
  if (!peerId || peerId === 'system') { notify({ text: '请选择聊天对象', color: 'warning' }); return }
  try {
    if (blockState.value?.youBlocked || blockState.value?.blockedYou) { notify({ text: '已被拉黑或已拉黑对方', color: 'error' }); return }
    const text = String(content || '').trim()
    if (!text) return
    if (realtime.isConnected()) {
      await realtime.sendDirectMessage(peerId, text)
    } else {
      await sendMessageToUser(peerId, text)
    }
    input.value = ''
    await afterSend(peerId)
  } catch (e) { notify({ text: e?.payload?.message || e?.message || '发送失败', color: 'error' }) }
}

async function afterSend(peerId) {
  await loadConversations()
  const byPeer = conversations.value.find(c => (c.peerUserId || c.PeerUserId) === peerId)
  const newConvId = byPeer?.id || byPeer?.Id || null
  if (newConvId) currentConversationId.value = newConvId
  await refreshMessages()
}

async function doSearch() {
  const q = String(searchText.value || '').trim()
  if (!q) { searchResults.value = []; return }
  searchResults.value = await searchUsersByName(q, 10)
}

async function chooseUser(u) {
  const chosenId = u.id || u.Id
  router.push({ name: 'messages', params: { userId: chosenId } })
  searchText.value = ''
  searchResults.value = []
}

async function doBlock() {
  const peerId = getCurrentPeerId()
  if (!peerId || peerId === 'system') return
  await blockUser(peerId)
  await refreshBlockState()
  notify({ text: '已拉黑该用户', color: 'success' })
}

async function doUnblock() {
  const peerId = getCurrentPeerId()
  if (!peerId || peerId === 'system') return
  await unblockUser(peerId)
  await refreshBlockState()
  notify({ text: '已取消拉黑', color: 'success' })
}

async function doClear() {
  const id = currentConversationId.value
  const peerId = getCurrentPeerId()
  if (!id || peerId === 'system') return
  try {
    await clearConversation(id)
    messages.value = []
    await loadConversations()
    if (conversations.value.length) {
      const firstConv = conversations.value[0]
      await selectConversation(firstConv.id || firstConv.Id)
    } else {
      currentConversationId.value = null
    }
    notify({ text: '已清除当前会话记录', color: 'success' })
  } catch (e) {
    notify({ text: e?.payload?.message || e?.message || '清除失败', color: 'error' })
  }
}

async function initialize() {
  await loadConversations()
  if (targetUserId.value) {
    const byPeer = conversations.value.find(c => (c.peerUserId || c.PeerUserId) === targetUserId.value)
    currentConversationId.value = byPeer?.id || byPeer?.Id || null
  }
  if (!currentConversationId.value && conversations.value.length && !targetUserId.value) {
    const firstConv = conversations.value[0]
    currentConversationId.value = firstConv.id || firstConv.Id
  }
  if (currentConversationId.value) {
    await selectConversation(currentConversationId.value)
  }
  await refreshBlockState()
  await refreshPeerProfile()
}

function handleMessage(e) {
  const msg = e.detail
  if (msg.conversationId === currentConversationId.value) {
    messages.value.push(msg)
    scrollToBottom()
  }
  loadConversations()
}

function triggerImageUpload() {
  try { imageInputRef.value?.click?.() } catch {}
}

async function onUploadImage(e) {
  const file = e?.target?.files?.[0]
  if (!file) return
  const peerId = getCurrentPeerId()
  if (!peerId || peerId === 'system') { notify({ text: '请选择聊天对象', color: 'warning' }); return }
  imageUploading.value = true
  try {
    const res = await uploadImage(file, 'chat')
    const url = res?.url || res?.imageUrl || res?.Url || res?.URL
    if (!url) throw new Error('上传成功，但未返回图片URL')
    await sendContent(url)
  } catch (err) {
    notify({ text: err?.payload?.message || err?.message || '图片上传失败', color: 'error' })
  } finally {
    imageUploading.value = false
    try { e.target.value = '' } catch {}
  }
}

function insertEmoji(emoji) {
  const s = String(emoji || '')
  const el = document.activeElement
  try {
    const textarea = document.querySelector('.chat-input-area textarea')
    if (textarea && typeof textarea.selectionStart === 'number' && typeof textarea.selectionEnd === 'number') {
      const start = textarea.selectionStart
      const end = textarea.selectionEnd
      const v = input.value || ''
      input.value = v.slice(0, start) + s + v.slice(end)
      nextTick(() => { try { textarea.focus(); const pos = start + s.length; textarea.setSelectionRange(pos, pos) } catch {} })
      return
    }
  } catch {}
  input.value = (input.value || '') + s
}

function isImageContent(m) {
  const c = String((m?.content ?? m?.Content) || '')
  const mdMatch = c.match(/!\[[^\]]*\]\(([^\)]+)\)/)
  const url = mdMatch ? mdMatch[1] : c
  return /(https?:\/\/|\/)[^\s]+\.(png|jpg|jpeg|gif|webp)$/i.test(url)
}

function extractImageUrl(m) {
  const c = String((m?.content ?? m?.Content) || '')
  const mdMatch = c.match(/!\[[^\]]*\]\(([^\)]+)\)/)
  return mdMatch ? mdMatch[1] : c
}

function openImage(m) {
  imagePreviewSrc.value = extractImageUrl(m)
  imagePreviewOpen.value = true
}

function closeImage() {
  imagePreviewOpen.value = false
  imagePreviewSrc.value = ''
}

onMounted(async () => {
  try { enableRealtime(); await realtime.startRealtime() } catch {}
  await initialize()
  window.addEventListener('asg:direct-message', handleMessage)
  window.addEventListener('asg:notification', loadConversations)
})

onUnmounted(() => {
  window.removeEventListener('asg:direct-message', handleMessage)
  window.removeEventListener('asg:notification', loadConversations)
  try { realtime.stopRealtime(); disableRealtimeGlobal('消息页已关闭，停止实时连接') } catch {}
})

watch(currentConversationId, async () => {
  await refreshBlockState()
  await refreshPeerProfile()
})

watch(() => route.params.userId, async (n) => {
  targetUserId.value = n || ''
  const byPeer = conversations.value.find(c => (c.peerUserId || c.PeerUserId) === targetUserId.value)
  const newConvId = byPeer?.id || byPeer?.Id || null
  if (newConvId !== currentConversationId.value) {
    currentConversationId.value = newConvId
    if (newConvId) await selectConversation(newConvId)
    else messages.value = []
  }
  await refreshBlockState()
  await refreshPeerProfile()
})

const formatTime = (t) => new Date(t).toLocaleString()
</script>

<template>
  <v-layout class="messages-layout">
    <!-- Left Sidebar: Conversations List -->
    <v-navigation-drawer
      class="conversations-drawer"
      :permanent="!smAndDown"
      :temporary="smAndDown"
      :model-value="leftDrawerOpen"
      @update:modelValue="leftDrawerOpen = $event"
      :width="smAndDown ? 320 : 360"
    >
      <div class="pa-4">
        <v-text-field
          v-model="searchText"
          label="搜索用户..."
          prepend-inner-icon="search"
          variant="solo-filled"
          density="compact"
          hide-details
          @input="doSearch"
        />
        <v-list v-if="searchResults.length" class="search-results-list">
          <v-list-item
            v-for="u in searchResults"
            :key="u.id || u.Id"
            :title="u.fullName || u.FullName"
            :prepend-avatar="u.avatarUrl || u.AvatarUrl"
            @click="chooseUser(u)"
          />
        </v-list>
      </div>
      <v-divider />
      <v-progress-linear v-if="loadingConversations" indeterminate color="primary" />
      <v-list class="conversations-list" mandatory :model-value="currentConversationId">
        <v-list-item
          v-for="c in conversations"
          :key="c.id || c.Id"
          :value="c.id || c.Id"
          @click="selectConversation(c.id || c.Id)"
          :class="{ 'v-list-item--active': (c.id || c.Id) === currentConversationId }"
        >
          <template #prepend>
            <v-badge :model-value="c.unreadCount > 0" color="error" :content="c.unreadCount" dot>
              <v-avatar :image="c.peerAvatarUrl || c.PeerAvatarUrl" size="40" />
            </v-badge>
          </template>
          <v-list-item-title class="font-weight-medium">{{ c.peerName || c.PeerName }}</v-list-item-title>
          <v-list-item-subtitle class="text-truncate">{{ c.lastMessage || c.LastMessage }}</v-list-item-subtitle>
          <template #append>
            <div class="text-caption text-disabled">{{ formatTime(c.updatedAt || c.UpdatedAt) }}</div>
          </template>
        </v-list-item>
      </v-list>
    </v-navigation-drawer>

    <!-- Main Content: Chat Window -->
  <v-main class="chat-main">
      <template v-if="currentConversationId || targetUserId">
        <v-app-bar flat density="compact" class="chat-header">
          <v-btn v-if="smAndDown" icon="menu" class="mr-1" @click="toggleConversations" />
          <v-toolbar-title class="font-weight-bold">{{ isSystemSelected ? '系统消息' : getCurrentPeerName() }}</v-toolbar-title>
          <v-spacer />
          <v-btn v-if="smAndDown && !isSystemSelected" icon="person" class="mr-1" @click="toggleProfile" />
          <v-menu v-if="!isSystemSelected">
            <template #activator="{ props }">
              <v-btn icon="more_vert" v-bind="props" />
            </template>
            <v-list>
              <v-list-item v-if="!blockState.youBlocked" @click="doBlock">拉黑</v-list-item>
              <v-list-item v-else @click="doUnblock">取消拉黑</v-list-item>
              <v-list-item @click="doClear">清空记录</v-list-item>
            </v-list>
          </v-menu>
        </v-app-bar>

        <div v-if="!isSystemSelected" ref="messagesContainer" class="messages-container">
          <v-progress-circular v-if="loading" indeterminate color="primary" class="loading-spinner" />
          <div v-for="m in messages" :key="m.id || m.Id" class="message-item" :class="{ 'my-message': (m.senderUserId || m.SenderUserId) === meId }">
            <v-avatar :image="(m.senderUserId || m.SenderUserId) === meId ? (currentUser.avatarUrl || currentUser.AvatarUrl) : (peerProfile?.avatarUrl || peerProfile?.AvatarUrl)" size="36" class="message-avatar" />
            <div class="message-content">
              <template v-if="isImageContent(m)">
                <v-img :src="extractImageUrl(m)" class="message-image clickable" cover @click="openImage(m)" />
              </template>
              <template v-else>
                <div class="message-bubble">{{ m.content || m.Content }}</div>
              </template>
              <div class="message-time">{{ formatTime(m.createdAt || m.CreatedAt) }}</div>
            </div>
          </div>
        </div>
        <div v-else class="messages-container">
          <NotificationsView />
        </div>

        <div v-if="!isSystemSelected" class="chat-input-area">
          <v-alert v-if="blockState.youBlocked" type="error" variant="tonal" density="compact" class="mb-2">你已拉黑该用户</v-alert>
          <v-alert v-if="blockState.blockedYou" type="error" variant="tonal" density="compact" class="mb-2">你已被该用户拉黑</v-alert>
          <v-textarea
            v-model="input"
            placeholder="输入消息..."
            variant="solo-filled"
            rows="2"
            no-resize
            hide-details
            auto-grow
            :disabled="blockState.youBlocked || blockState.blockedYou"
            @keydown.enter.prevent="send"
          />
          <div class="input-actions">
            <v-btn :loading="imageUploading" icon="add_photo_alternate" variant="text" @click="triggerImageUpload" />
            <input ref="imageInputRef" type="file" accept="image/png,image/jpeg,image/jpg,image/webp,image/gif" @change="onUploadImage" style="display:none" />
            <v-menu v-model="emojiOpen" :close-on-content-click="false">
              <template #activator="{ props }">
                <v-btn icon="mood" variant="text" v-bind="props" />
              </template>
              <v-sheet class="pa-2" rounded="lg" style="max-width: 280px">
                <div class="emoji-grid">
                  <v-btn v-for="e in emojis" :key="e" variant="text" class="emoji-btn" @click="insertEmoji(e)">{{ e }}</v-btn>
                </div>
              </v-sheet>
            </v-menu>
            <v-spacer />
            <v-btn color="primary" @click="send" :disabled="!input.trim() || blockState.youBlocked || blockState.blockedYou">发送</v-btn>
          </div>
        </div>
      </template>
      <template v-else>
        <div class="empty-state">
          <v-icon icon="chat" size="80" class="text-disabled mb-4" />
          <div class="text-h6 text-disabled">选择一个会话开始聊天</div>
        </div>
      </template>
    </v-main>

    <!-- Right Sidebar: User Profile -->
    <v-navigation-drawer
      v-if="!isSystemSelected"
      location="right"
      class="profile-drawer"
      :permanent="!smAndDown"
      :temporary="smAndDown"
      :model-value="profileDrawerOpen"
      @update:modelValue="profileDrawerOpen = $event"
      :width="smAndDown ? 320 : 360"
    >
      <div v-if="peerProfile" class="text-center pa-6">
        <v-avatar :image="peerProfile.avatarUrl || peerProfile.AvatarUrl" size="96" class="mb-4" />
        <div class="text-h6">{{ peerProfile.fullName || peerProfile.FullName }}</div>
        <div class="text-body-2 text-disabled">{{ peerProfile.email || peerProfile.Email }}</div>
        <v-btn block color="primary" variant="tonal" class="mt-6" :to="`/users/${peerProfile.id || peerProfile.Id}`">查看主页</v-btn>
      </div>
      <div v-else class="empty-state">
        <v-icon icon="person_off" size="80" class="text-disabled mb-4" />
        <div class="text-h6 text-disabled">未选择用户</div>
      </div>
    </v-navigation-drawer>
    <v-dialog v-model="imagePreviewOpen" :fullscreen="smAndDown" :max-width="860">
      <v-card>
        <v-toolbar flat density="compact">
          <v-toolbar-title>图片预览</v-toolbar-title>
          <v-spacer />
          <v-btn :href="imagePreviewSrc" target="_blank" variant="text" prepend-icon="open_in_new">新窗口打开</v-btn>
          <v-btn icon="close" variant="text" @click="closeImage" />
        </v-toolbar>
        <v-card-text class="d-flex justify-center">
          <v-img :src="imagePreviewSrc" class="image-preview-img" contain />
        </v-card-text>
      </v-card>
    </v-dialog>
  </v-layout>
</template>

<style scoped>
.messages-layout {
  height: calc(100vh - 64px); /* Adjust based on your app bar height */
}

.conversations-drawer {
  border-right: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.conversations-list .v-list-item {
  transition: background-color 0.2s;
}

.search-results-list {
  position: absolute;
  z-index: 10;
  width: calc(100% - 32px);
  background: rgb(var(--v-theme-surface));
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0,0,0,0.1);
}

.chat-main {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.chat-header {
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.messages-container {
  flex-grow: 1;
  overflow-y: auto;
  padding: 1.5rem;
  position: relative;
}

.loading-spinner {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
}

.message-item {
  display: flex;
  align-items: flex-end;
  margin-bottom: 1.5rem;
  gap: 12px;
}

.message-item.my-message {
  flex-direction: row-reverse;
}

.message-bubble {
  padding: 10px 16px;
  border-radius: 18px;
  background: rgb(var(--v-theme-surface-variant));
  max-width: 450px;
  word-wrap: break-word;
}

.message-image {
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.12);
  max-width: min(320px, 82vw);
}
.message-image.clickable { cursor: zoom-in; }

.my-message .message-bubble {
  background: rgb(var(--v-theme-primary));
  color: rgb(var(--v-theme-on-primary));
}

.message-time {
  font-size: 0.75rem;
  color: rgb(var(--v-theme-on-surface), 0.6);
  margin-top: 4px;
}

.my-message .message-time {
  text-align: right;
}

.chat-input-area {
  padding: 1rem;
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.input-actions {
  display: flex;
  align-items: center;
  margin-top: 8px;
}

.emoji-grid {
  display: grid;
  grid-template-columns: repeat(6, 1fr);
  gap: 4px;
}

.emoji-btn {
  min-width: 36px;
}

.empty-state {
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  height: 100%;
}

.profile-drawer {
  border-left: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.image-preview-img { max-width: 92vw; max-height: 82vh }

@media (max-width: 599px) {
  .messages-container { padding: 0.75rem; }
  .message-item { gap: 8px; }
  .message-bubble { max-width: 82vw; }
  .chat-input-area { padding: 0.75rem; }
  .emoji-btn { min-width: 30px; }
}
</style>

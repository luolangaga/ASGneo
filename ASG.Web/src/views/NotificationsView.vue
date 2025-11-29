<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { listNotifications, markRead, markAllRead } from '../services/notifications'
import { refreshUnreadCount } from '../stores/notifications'
import { notify } from '../stores/notify'
import realtime, { enableRealtime, disableRealtimeGlobal } from '../services/realtime'

const items = ref([])
const loading = ref(false)
const router = useRouter()

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

onMounted(() => {
  load()
  try { enableRealtime(); realtime.startRealtime() } catch {}
  try { window.addEventListener('asg:notification', load) } catch {}
})

onUnmounted(() => {
  try { window.removeEventListener('asg:notification', load) } catch {}
  try { realtime.stopRealtime(); disableRealtimeGlobal('已离开通知页，停止实时通知连接') } catch {}
})

function typeOf(it) { return it?.type || it?.Type }
function payloadOf(it) {
  const p = it?.payload || it?.Payload
  if (typeof p === 'string') { try { return JSON.parse(p) } catch { return {} } }
  return p || {}
}
function typeLabel(it) {
  const t = typeOf(it)
  if (t === 'article.comment') return '文章有新评论'
  if (t === 'comment.reply') return '评论有新回复'
  if (t === 'recruitment.application') return '收到新的报名申请'
  if (t === 'team.created') return '战队创建成功'
  if (t === 'team.invite.submitted') return '有人通过邀请加入战队'
  if (t === 'event.registration.approved') return '赛事报名审批通过'
  return '系统通知'
}
function typeIcon(it) {
  const t = typeOf(it)
  if (t === 'article.comment') return 'chat'
  if (t === 'comment.reply') return 'chat'
  if (t === 'recruitment.application') return 'how_to_reg'
  if (t === 'team.created') return 'group'
  if (t === 'team.invite.submitted') return 'person_add'
  if (t === 'event.registration.approved') return 'check_circle'
  return 'notifications'
}
function details(it) {
  const t = typeOf(it)
  const p = payloadOf(it)
  if (t === 'article.comment') return [p?.content ? `评论：${p.content}` : ''].filter(Boolean).join(' ')
  if (t === 'comment.reply') return [p?.content ? `回复：${p.content}` : ''].filter(Boolean).join(' ')
  if (t === 'team.created') return [p?.teamName].filter(Boolean).join(' ')
  if (t === 'team.invite.submitted') return [p?.teamName, p?.playerName].filter(Boolean).join(' ')
  if (t === 'event.registration.approved') return [p?.eventName, p?.teamName].filter(Boolean).join(' ')
  if (t === 'recruitment.application') return [p?.applicantName].filter(Boolean).join(' ')
  return ''
}
function formatTime(it) { return new Date(it?.createdAt || it?.CreatedAt).toLocaleString() }
function routeOf(it) {
  const t = typeOf(it)
  const p = payloadOf(it)
  if (t === 'article.comment') return p?.articleId ? { name: 'article-detail', params: { id: p.articleId }, query: { hlCommentId: p?.commentId } } : null
  if (t === 'comment.reply') return p?.articleId ? { name: 'article-detail', params: { id: p.articleId }, query: { hlCommentId: p?.commentId } } : null
  if (t === 'team.created') return p?.teamId ? { name: 'team-detail', params: { id: p.teamId } } : null
  if (t === 'team.invite.submitted') return p?.teamId ? { name: 'team-detail', params: { id: p.teamId } } : null
  if (t === 'event.registration.approved') return p?.eventId ? { name: 'event-detail', params: { id: p.eventId } } : null
  if (t === 'recruitment.application') return p?.eventId ? { name: 'event-detail', params: { id: p.eventId } } : { name: 'my-events-manage' }
  return null
}
async function open(it) {
  const id = it?.id || it?.Id
  const r = routeOf(it)
  if (id) await read(id)
  if (r) router.push(r)
}
</script>

<template>
  <v-container class="py-4 py-sm-6 page-container">
    <div class="d-flex flex-column flex-sm-row align-sm-center mb-6">
      <h1 class="text-h5 text-sm-h4 font-weight-bold align-self-start align-self-sm-auto">通知中心</h1>
      <v-spacer />
      <v-btn class="align-self-end align-self-sm-auto mt-2 mt-sm-0" variant="text" prepend-icon="done_all" @click="readAll">全部已读</v-btn>
    </div>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <div v-if="items.length">
      <v-list class="notification-list">
        <v-list-item
          v-for="it in items"
          :key="it.id || it.Id"
          @click="open(it)"
          class="notification-item"
          :class="{ 'unread': !(it.isRead || it.IsRead) }"
          lines="two"
        >
          <template #prepend>
            <div class="prepend-icon">
              <lottie-player
                v-if="typeOf(it) === 'team.created' || typeOf(it) === 'event.registration.approved'"
                src="\animations\Notification.json"
                autoplay
                loop
                class="lottie-player"
              ></lottie-player>
              <v-icon v-else :icon="typeIcon(it)" color="primary" />
            </div>
          </template>

          <v-list-item-title class="item-title">{{ typeLabel(it) }}</v-list-item-title>
          <v-list-item-subtitle class="text-caption item-subtitle">{{ details(it) }} · {{ formatTime(it) }}</v-list-item-subtitle>

          <template #append>
            <v-btn
              v-if="!(it.isRead || it.IsRead)"
              size="small"
              variant="text"
              @click.stop="read(it.id || it.Id)"
              class="d-none d-sm-flex"
            >标为已读</v-btn>
            <v-btn
              v-if="!(it.isRead || it.IsRead)"
              size="small"
              variant="icon"
              icon="check"
              @click.stop="read(it.id || it.Id)"
              class="d-sm-none"
            ></v-btn>
            <v-icon v-else color="success">check_circle</v-icon>
          </template>
        </v-list-item>
      </v-list>
    </div>

    <v-row v-if="!items.length && !loading">
      <v-col cols="12">
        <v-sheet rounded="lg" class="text-center pa-6 pa-sm-12">
          <v-icon icon="notifications_off" size="64" class="mb-4 text-disabled"></v-icon>
          <div class="text-h6 text-disabled">暂无通知</div>
          <div class="text-body-2 text-disabled mt-2">所有消息都会显示在这里</div>
        </v-sheet>
      </v-col>
    </v-row>
  </v-container>
</template>

<style scoped>
.page-container {
  max-width: 900px;
}

.notification-list {
  background: transparent !important;
}

.notification-item {
  border-radius: 12px !important;
  margin-bottom: 1rem;
  background: rgb(var(--v-theme-surface));
  box-shadow: 0 2px 8px rgba(0,0,0,0.05);
  transition: all 0.2s ease-out;
  border: 1px solid transparent;
  cursor: pointer;
}

.notification-item:hover {
  transform: translateY(-3px);
  box-shadow: 0 6px 20px rgba(0,0,0,0.08);
  border-color: rgb(var(--v-theme-primary));
}

.notification-item.unread {
  background: rgba(var(--v-theme-primary-rgb), 0.05);
}

.notification-item.unread .v-list-item-title {
  font-weight: 600;
}

.notification-item .v-list-item-subtitle {
  opacity: 0.8;
}

.lottie-player {
  width: 128px !important;
  height: 128px !important;
}

.prepend-icon {
  width: 48px;
  height: 48px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(var(--v-theme-primary-rgb), 0.1);
  border-radius: 50%;
  margin-right: 16px;
}
</style>

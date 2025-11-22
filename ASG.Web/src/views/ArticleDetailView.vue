<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRoute } from 'vue-router'
import PageHero from '../components/PageHero.vue'
import { renderMarkdown } from '../utils/markdown'
import { getArticle, getComments, addComment, likeArticle, addArticleView } from '../services/articles'
import { getUser } from '../services/user'
import { isAuthenticated } from '../stores/auth'

const route = useRoute()
const id = route.params.id

const loading = ref(false)
const errorMsg = ref('')
const article = ref(null)
const author = ref(null)

const commentsLoading = ref(false)
const commentsError = ref('')
const comments = ref([])
const commentsTotal = ref(0)
const commentsPage = ref(1)
const commentsPageSize = ref(20)
const replyOpen = ref({})
const replyText = ref({})

const newComment = ref('')
const posting = ref(false)

const loggedIn = computed(() => isAuthenticated.value)
const heroTitle = computed(() => (article.value?.title || article.value?.Title || '帖子详情'))
const subtitleText = computed(() => {
  const d = article.value?.createdAt || article.value?.CreatedAt
  const ds = d ? new Date(d).toLocaleString() : ''
  const author = article.value?.authorName || article.value?.AuthorName || '未知作者'
  return `${author} · ${ds}`
})

const authorName = computed(() => {
  return (
    article.value?.authorName ||
    article.value?.AuthorName ||
    author.value?.fullName ||
    author.value?.FullName ||
    '未知作者'
  )
})

const authorInitials = computed(() => {
  const name = authorName.value || ''
  const parts = name.split(/\s+/).filter(Boolean)
  const initials = parts.slice(0, 2).map(p => p[0]).join('')
  return initials || 'NA'
})

const authorTeamName = computed(() => article.value?.authorTeamName || article.value?.AuthorTeamName || '无')
const createdAtStr = computed(() => {
  const d = article.value?.createdAt || article.value?.CreatedAt
  return d ? new Date(d).toLocaleString() : ''
})

const likeCount = computed(() => article.value?.likes ?? article.value?.Likes ?? 0)
const viewCount = computed(() => article.value?.views ?? article.value?.Views ?? 0)
const liking = ref(false)
async function onLike() {
  const aid = article.value?.id || article.value?.Id
  if (!aid || liking.value) return
  liking.value = true
  try {
    const newLikes = await likeArticle(aid)
    if (article.value) {
      if ('likes' in article.value) article.value.likes = newLikes
      else if ('Likes' in article.value) article.value.Likes = newLikes
      else article.value.likes = newLikes
    }
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '点赞失败'
  } finally {
    liking.value = false
  }
}

function toMd(s) { return renderMarkdown(s || '') }

async function loadArticle() {
  loading.value = true
  errorMsg.value = ''
  try {
    article.value = await getArticle(id)
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载帖子失败'
  } finally {
    loading.value = false
  }
}

async function loadAuthor() {
  try {
    const uid = article.value?.authorUserId || article.value?.AuthorUserId
    if (!uid) {
      author.value = null
      return
    }
    author.value = await getUser(uid)
  } catch {
    author.value = null
  }
}

function mapComment(c) {
  const item = {
    id: c.id || c.Id,
    authorName: c.authorName || c.AuthorName || '匿名',
    authorEmail: c.authorEmail || c.AuthorEmail || '',
    authorAvatarUrl: c.authorAvatarUrl || c.AuthorAvatarUrl || null,
    content: c.content || c.Content,
    createdAt: c.createdAt || c.CreatedAt,
    parentId: c.parentId || c.ParentId || null,
    replies: Array.isArray(c.replies || c.Replies) ? (c.replies || c.Replies).map(mapComment) : []
  }
  return item
}

async function loadComments() {
  commentsLoading.value = true
  commentsError.value = ''
  try {
    const res = await getComments(id, { page: commentsPage.value, pageSize: commentsPageSize.value })
    comments.value = (res.items || res.Items || []).map(mapComment)
    commentsTotal.value = res.totalCount || res.TotalCount || comments.value.length
  } catch (err) {
    commentsError.value = err?.payload?.message || err?.message || '加载评论失败'
  } finally {
    commentsLoading.value = false
  }
}

async function onPostComment() {
  if (!newComment.value?.trim()) return
  posting.value = true
  try {
    await addComment(id, { content: newComment.value.trim() })
    newComment.value = ''
    await loadComments()
  } catch (err) {
    commentsError.value = err?.payload?.message || err?.message || '发表评论失败'
  } finally {
    posting.value = false
  }
}

function toggleReply(cid) {
  replyOpen.value[cid] = !replyOpen.value[cid]
}

async function submitReply(cid) {
  const text = (replyText.value[cid] || '').trim()
  if (!text) return
  try {
    await addComment(id, { content: text, parentId: cid })
    replyText.value[cid] = ''
    replyOpen.value[cid] = false
    await loadComments()
  } catch (err) {
    commentsError.value = err?.payload?.message || err?.message || '回复失败'
  }
}

onMounted(async () => {
  await loadArticle()
  await loadAuthor()
  await loadComments()
  // 进入详情页即计一次浏览
  try {
    const views = await addArticleView(id)
    if (article.value) {
      if ('views' in article.value) article.value.views = views
      else if ('Views' in article.value) article.value.Views = views
      else article.value.views = views
    }
  } catch (e) {
    // 静默失败，不影响详情展示
  }
})
</script>

<template>
  <PageHero :title="heroTitle" :subtitle="subtitleText" icon="article" />

  <v-container class="py-6">
    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />

    <v-card v-if="article && !errorMsg" class="mb-6">
      <v-card-item class="author-header">
        <div class="d-flex align-center">
          <v-avatar size="48" class="mr-3 elevation-1">
            <template v-if="author?.avatarUrl || author?.AvatarUrl">
              <v-img :src="author?.avatarUrl || author?.AvatarUrl" alt="作者头像" cover />
            </template>
            <template v-else>
              <span class="text-body-2">{{ authorInitials }}</span>
            </template>
          </v-avatar>
          <div class="flex-grow-1">
            <div class="text-subtitle-1 font-weight-medium">
              <router-link
                v-if="article?.authorUserId || article?.AuthorUserId"
                :to="`/users/${article?.authorUserId || article?.AuthorUserId}`"
                class="text-decoration-none"
              >{{ authorName }}</router-link>
              <template v-else>{{ authorName }}</template>
            </div>
            <div class="text-caption text-medium-emphasis">
              战队：
              <template v-if="authorTeamName && authorTeamName !== '无'">
                <router-link :to="{ name: 'team-search', query: { q: authorTeamName } }" class="text-decoration-none">{{ authorTeamName }}</router-link>
              </template>
              <template v-else>无</template>
              <span class="mx-1">·</span>
              发布于：{{ createdAtStr }}
            </div>
          </div>
          <div class="d-flex align-center">
            <template v-if="(article.honors || article.Honors)?.length">
              <v-chip size="small" color="primary" variant="tonal" class="mr-2">冠军荣誉 {{ (article.honors || article.Honors).length }}</v-chip>
            </template>
            <v-chip size="small" color="default" variant="tonal" class="mr-2">
              <v-icon start icon="visibility" /> 浏览 {{ viewCount }}
            </v-chip>
            <v-btn size="small" :loading="liking" color="primary" variant="tonal" @click="onLike" prepend-icon="favorite">
              点赞 {{ likeCount }}
            </v-btn>
          </div>
        </div>
      </v-card-item>

      <v-divider class="mx-4" />

      <v-card-text>
        <div v-if="(article.honors || article.Honors)?.length" class="mb-3">
          <div class="text-subtitle-2 text-medium-emphasis">荣誉（冠军赛事）</div>
          <div class="d-flex flex-wrap mt-1">
            <v-chip v-for="ev in (article.honors || article.Honors)" :key="ev.id || ev.Id" class="mr-2 mb-2" size="small" color="primary" variant="tonal">
              {{ ev.name || ev.Name }}
            </v-chip>
          </div>
        </div>
        <div v-if="article.contentMarkdown || article.ContentMarkdown" class="md-content" v-html="toMd(article.contentMarkdown || article.ContentMarkdown)" />
        <div v-else class="text-medium-emphasis">暂无正文内容</div>
      </v-card-text>
    </v-card>

    <v-card>
      <v-card-title>评论</v-card-title>
      <v-card-text>
        <v-alert v-if="commentsError" type="error" :text="commentsError" class="mb-3" />
        <v-progress-linear v-if="commentsLoading" indeterminate color="primary" class="mb-3" />

        <div v-if="loggedIn" class="d-flex align-center mb-3">
          <v-textarea v-model="newComment" rows="3" label="发布你的评论" auto-grow hide-details class="mr-3" />
          <v-btn :loading="posting" color="primary" @click="onPostComment" prepend-icon="send">发表</v-btn>
        </div>
        <v-alert v-else type="info" text="登录后可发表评论" class="mb-3" />

        <v-list density="comfortable">
          <template v-if="comments.length">
            <div v-for="c in comments" :key="c.id" class="mb-4">
              <div class="d-flex">
                <v-avatar size="36" class="mr-3">
                  <template v-if="c.authorAvatarUrl">
                    <v-img :src="c.authorAvatarUrl" alt="头像" cover />
                  </template>
                  <template v-else>
                    <span class="text-body-2">{{ (c.authorName || 'NA').slice(0,2) }}</span>
                  </template>
                </v-avatar>
                <div class="flex-grow-1">
                  <div class="text-subtitle-2">{{ c.authorName }} <span v-if="c.authorEmail" class="text-caption text-medium-emphasis">· {{ c.authorEmail }}</span></div>
                  <div class="text-caption text-medium-emphasis">{{ new Date(c.createdAt).toLocaleString() }}</div>
                  <div class="mt-2">{{ c.content }}</div>
                  <div class="mt-2">
                    <v-btn size="x-small" variant="text" color="primary" @click="toggleReply(c.id)">回复</v-btn>
                  </div>
                  <div v-if="replyOpen[c.id]" class="mt-2">
                    <v-textarea v-model="replyText[c.id]" rows="2" auto-grow hide-details label="回复内容" />
                    <div class="mt-2">
                      <v-btn size="small" color="primary" @click="submitReply(c.id)" prepend-icon="send">发表回复</v-btn>
                      <v-btn size="small" variant="text" class="ml-2" @click="toggleReply(c.id)">取消</v-btn>
                    </div>
                  </div>

                  <div v-if="c.replies && c.replies.length" class="mt-3 pl-6 border-left">
                    <div v-for="r in c.replies" :key="r.id" class="mb-3">
                      <div class="d-flex">
                        <v-avatar size="30" class="mr-3">
                          <template v-if="r.authorAvatarUrl">
                            <v-img :src="r.authorAvatarUrl" alt="头像" cover />
                          </template>
                          <template v-else>
                            <span class="text-body-2">{{ (r.authorName || 'NA').slice(0,2) }}</span>
                          </template>
                        </v-avatar>
                        <div class="flex-grow-1">
                          <div class="text-subtitle-2">{{ r.authorName }} <span v-if="r.authorEmail" class="text-caption text-medium-emphasis">· {{ r.authorEmail }}</span></div>
                          <div class="text-caption text-medium-emphasis">{{ new Date(r.createdAt).toLocaleString() }}</div>
                          <div class="mt-2">{{ r.content }}</div>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </template>
          <template v-else-if="!commentsLoading && !commentsError">
            <v-alert type="info" text="暂时没有评论" />
          </template>
        </v-list>

        <div class="d-flex justify-center mt-3" v-if="commentsTotal > commentsPageSize">
          <v-pagination v-model="commentsPage" :length="Math.ceil(commentsTotal / commentsPageSize)" total-visible="7" @update:modelValue="loadComments" />
        </div>
      </v-card-text>
    </v-card>
  </v-container>
</template>

<style scoped>
.md-content {
  max-width: 960px;
}
.author-header {
  padding-top: 16px;
  padding-bottom: 8px;
}
</style>
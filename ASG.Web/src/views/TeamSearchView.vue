<template>
  <PageHero title="综合搜索" subtitle="按关键词搜索战队、赛事与文章" icon="search">
    <template #actions>
      <v-btn variant="text" class="mb-3" to="/teams/create" prepend-icon="person_add">创建战队</v-btn>
    </template>
    <template #media>
      <lottie-player src="/animations/Untitled_file.json" autoplay loop style="width:220px;height:220px"></lottie-player>
    </template>
  </PageHero>
  <v-container class="py-8 page-container">
    <div class="mb-8">
      <div class="d-flex align-center flex-wrap gap-2">
        <v-text-field
          v-model="query"
          class="flex-grow-1"
          style="min-width: 220px; max-width: 600px;"
          label="搜索站内内容（战队 / 赛事 / 文章）"
          prepend-inner-icon="search"
          variant="outlined"
          density="comfortable"
          clearable
          hide-details
          @keyup.enter="doSearch"
        />
        <v-select
          v-model="searchType"
          :items="typeOptions"
          label="搜索类型"
          variant="outlined"
          density="comfortable"
          hide-details
          style="width: 150px"
        />
        <v-btn
          color="primary"
          height="48"
          class="px-6"
          :loading="loading"
          :disabled="!queryTrimmed"
          @click="doSearch"
          prepend-icon="search"
        >搜索</v-btn>
      </div>
      
      <div class="d-flex flex-wrap gap-4 text-medium-emphasis mt-4 text-caption">
        <template v-if="searchType === 'all'">
          <v-chip size="small" variant="tonal">战队：{{ totalCount }}</v-chip>
          <v-chip size="small" variant="tonal">赛事：{{ eventsCount }}</v-chip>
          <v-chip size="small" variant="tonal">文章：{{ articlesCount }}</v-chip>
        </template>
        <template v-else-if="searchType === 'teams'">
          <v-chip size="small" variant="tonal">战队：{{ totalCount }}</v-chip>
        </template>
        <template v-else-if="searchType === 'events'">
          <v-chip size="small" variant="tonal">赛事：{{ eventsCount }}</v-chip>
        </template>
        <template v-else-if="searchType === 'articles'">
          <v-chip size="small" variant="tonal">文章：{{ articlesCount }}</v-chip>
        </template>
      </div>
    </div>

    <v-alert v-if="error" type="error" class="mb-4" :text="error" variant="tonal" border="start" />

    <v-row v-if="loading" class="mb-4">
      <v-col v-for="n in 6" :key="n" cols="12" sm="6" md="4" lg="3">
        <v-skeleton-loader type="card" rounded="xl" />
      </v-col>
    </v-row>

    <!-- 战队结果 -->
    <div class="mb-8" v-if="!loading && (searchType === 'all' || searchType === 'teams')">
      <div class="d-flex align-center mb-4">
        <v-icon icon="group" color="primary" class="mr-2" />
        <h3 class="text-h6 font-weight-bold">战队结果</h3>
      </div>
      
      <v-row v-if="results.length" dense>
        <v-col v-for="team in results" :key="team.id" cols="12" sm="6" md="4" lg="3">
          <v-card class="hover-elevate h-100 d-flex flex-column" variant="flat" border rounded="xl">
            <v-card-item>
              <div class="d-flex align-center">
                <v-avatar size="48" class="mr-3" color="primary-lighten-4" variant="flat">
                  <template v-if="team.logoUrl">
                    <v-img :src="team.logoUrl" cover>
                      <template #placeholder>
                        <div class="d-flex align-center justify-center" style="width:100%;height:100%">
                          <lottie-player src="/animations/loading.json" background="transparent" speed="1" loop autoplay style="width:28px;height:28px"></lottie-player>
                        </div>
                      </template>
                    </v-img>
                  </template>
                  <template v-else>
                    <span class="text-h6 font-weight-bold text-primary">{{ getAvatarLetter(team.name) }}</span>
                  </template>
                </v-avatar>
                <div style="min-width: 0">
                  <v-card-title class="pa-0 text-subtitle-1 font-weight-bold text-truncate">{{ team.name }}</v-card-title>
                  <v-card-subtitle class="pa-0 text-caption">
                    <v-icon icon="thumb_up" size="x-small" color="primary" class="mr-1"></v-icon>
                    {{ team.likes ?? 0 }}
                  </v-card-subtitle>
                  <v-chip
                    v-if="team.hidePlayers || team.HidePlayers"
                    size="x-small"
                    color="warning"
                    class="mt-1"
                    variant="tonal"
                  >队员已隐藏</v-chip>
                </div>
            </div>
          </v-card-item>
          
          <v-divider class="mx-4 opacity-20" />

          <v-card-text class="text-body-2 flex-grow-1">
            <div v-if="team.description" :class="['md-content', { 'md-truncate': !isExpandedTeam(team.id) }]" v-html="toMd(team.description)"></div>
            <div v-else class="text-caption text-medium-emphasis font-italic">暂无战队描述</div>
            <div v-if="(team.description?.length || 0) > 220" class="mt-1">
              <v-btn size="x-small" variant="text" density="compact" color="primary" @click="toggleExpandedTeam(team.id)">{{ isExpandedTeam(team.id) ? '收起' : '展开' }}</v-btn>
            </div>
          </v-card-text>

          <v-card-actions class="px-4 pb-4">
            <v-btn
              color="primary"
              variant="tonal"
              class="flex-grow-1"
              :to="{ name: 'team-detail', params: { id: team.id } }"
              prepend-icon="visibility"
            >查看详情</v-btn>
            <div class="like-anim-container ml-2">
              <v-btn color="secondary" variant="text" icon="thumb_up_off_alt" @click="doLikeList(team.id)" title="点赞"></v-btn>
              <lottie-player v-if="likeFxTeamId === team.id" class="like-anim" src="/animations/Love_Animation_with_Particle.json" autoplay style="width:100px;height:100px;top:-20px;left:-20px"></lottie-player>
            </div>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>
    <div v-else class="pa-8 text-center border rounded-xl border-dashed text-medium-emphasis">
      <v-icon size="48" color="medium-emphasis" icon="group_off" class="mb-2" />
      <div class="text-h6">没有找到匹配的战队</div>
      <div class="text-body-2">试试换个关键词或检查拼写</div>
    </div>
  </div>

    <!-- 赛事结果 -->
    <div class="mb-8" v-if="!loading && (searchType === 'all' || searchType === 'events')">
      <div class="d-flex align-center mb-4">
        <v-icon icon="grid_view" color="primary" class="mr-2" />
        <h3 class="text-h6 font-weight-bold">赛事结果</h3>
      </div>
    
    <v-row v-if="eventResults.length" dense>
      <v-col v-for="ev in eventResults" :key="ev.id" cols="12" sm="6" md="4" lg="3">
        <v-card class="hover-elevate h-100" variant="flat" border rounded="xl">
          <v-card-title class="d-flex align-center">
              <v-avatar size="40" class="mr-3" rounded="lg" color="surface-variant">
              <v-img v-if="ev.logoUrl" :src="ev.logoUrl" alt="event logo" cover>
                <template #placeholder>
                  <div class="d-flex align-center justify-center" style="width:100%;height:100%">
                    <lottie-player src="/animations/loading.json" background="transparent" speed="1" loop autoplay style="width:64px;height:64px"></lottie-player>
                  </div>
                </template>
              </v-img>
              <v-icon v-else icon="grid_view" color="primary" />
              </v-avatar>
            <div class="flex-grow-1" style="min-width: 0">
              <div class="text-subtitle-1 font-weight-bold text-truncate">{{ ev.name }}</div>
              <div class="text-caption text-medium-emphasis">报名队伍：{{ ev.registeredTeamsCount ?? 0 }}</div>
            </div>
          </v-card-title>
          
          <v-divider class="mx-4 opacity-20" />
          
          <v-card-text>
            <div v-if="ev.description" class="text-body-2 md-truncate text-medium-emphasis" v-html="toMd(ev.description)" />
            <div v-else class="text-caption text-medium-emphasis font-italic">暂无赛事简介</div>
          </v-card-text>
          <v-card-actions class="px-4 pb-4">
            <v-btn color="primary" variant="tonal" block :to="`/events/${ev.id}`" prepend-icon="visibility">查看详情</v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>
    <div v-else class="pa-8 text-center border rounded-xl border-dashed text-medium-emphasis">
      <v-icon size="48" color="medium-emphasis" icon="grid_off" class="mb-2" />
      <div class="text-body-1">没有找到匹配的赛事</div>
    </div>
  </div>

    <!-- 文章结果 -->
    <div class="mb-8" v-if="!loading && (searchType === 'all' || searchType === 'articles')">
      <div class="d-flex align-center mb-4">
        <v-icon icon="article" color="primary" class="mr-2" />
        <h3 class="text-h6 font-weight-bold">文章结果</h3>
      </div>

    <v-row v-if="articleResults.length" dense>
      <v-col v-for="a in articleResults" :key="a.id" cols="12" sm="6" md="4" lg="3">
        <v-card class="hover-elevate h-100" variant="flat" border rounded="xl">
          <v-card-title class="d-flex align-center">
            <v-avatar size="32" color="primary-lighten-4" class="mr-2">
              <v-icon icon="article" color="primary" size="small" />
            </v-avatar>
            <div class="text-subtitle-1 font-weight-bold text-truncate">{{ a.title }}</div>
          </v-card-title>
          <v-card-text>
            <div class="text-caption text-medium-emphasis mb-2">
              作者：
              <router-link v-if="a.authorUserId" :to="`/users/${a.authorUserId}`" class="text-decoration-none font-weight-medium text-primary">
                {{ a.authorName || '匿名' }}
              </router-link>
              <template v-else>{{ a.authorName || '匿名' }}</template>
            </div>
            <div v-if="a.contentMarkdown" class="text-body-2 md-truncate text-medium-emphasis" v-html="toMd(a.contentMarkdown)" />
          </v-card-text>
          <v-card-actions class="px-4 pb-4">
            <v-btn color="default" variant="text" :to="`/articles/${a.id}`" prepend-icon="visibility">查看详情</v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>
    <div v-else class="pa-8 text-center border rounded-xl border-dashed text-medium-emphasis">
      <v-icon size="48" color="medium-emphasis" icon="article" class="mb-2" />
      <div class="text-body-1">没有找到匹配的文章</div>
    </div>
  </div>

    <div class="d-flex justify-center mt-4" v-if="showPagination">
      <v-pagination v-model="page" :length="maxPage" total-visible="7" @update:modelValue="doSearch" rounded="circle" density="comfortable" />
    </div>
  </v-container>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { likeTeam } from '../services/teams'
import { search as searchApi } from '../services/search'
import PageHero from '../components/PageHero.vue'
import { renderMarkdown } from '../utils/markdown'

const route = useRoute()
const query = ref('')
const searchType = ref('all') // all | teams | events | articles
const typeOptions = [
  { title: '综合', value: 'all' },
  { title: '仅战队', value: 'teams' },
  { title: '仅赛事', value: 'events' },
  { title: '仅文章', value: 'articles' },
]
const page = ref(1)
const pageSize = ref(12)
const totalCount = ref(0)
const results = ref([]) // 战队结果
const likeFxTeamId = ref(null)
const eventResults = ref([])
const eventsCount = ref(0)
const articleResults = ref([])
const articlesCount = ref(0)
const loading = ref(false)
const error = ref('')

const expandedTeamMap = ref({})

const queryTrimmed = computed(() => query.value.trim())
const showPagination = computed(() => {
  if (searchType.value === 'teams') return totalCount.value > pageSize.value
  if (searchType.value === 'events') return eventsCount.value > pageSize.value
  if (searchType.value === 'articles') return articlesCount.value > pageSize.value
  return totalCount.value > pageSize.value
})
const maxPage = computed(() => {
  const size = pageSize.value
  let count = totalCount.value
  if (searchType.value === 'events') count = eventsCount.value
  else if (searchType.value === 'articles') count = articlesCount.value
  return Math.max(1, Math.ceil(count / size))
})

function getAvatarLetter(name) {
  if (!name || typeof name !== 'string') return '?'
  return name.trim().charAt(0).toUpperCase()
}

async function doSearch() {
  error.value = ''
  if (!queryTrimmed.value) return
  loading.value = true
  try {
    const type = searchType.value
    const res = await searchApi({ type, query: queryTrimmed.value, page: page.value, pageSize: pageSize.value })

    if (type === 'all') {
      const teamPaged = res.Teams || res.teams || { Items: [], TotalCount: 0 }
      const eventPaged = res.Events || res.events || { Items: [], TotalCount: 0 }
      const articlePaged = res.Articles || res.articles || { Items: [], TotalCount: 0 }

      results.value = teamPaged.Items || teamPaged.items || []
      totalCount.value = teamPaged.TotalCount ?? teamPaged.totalCount ?? results.value.length

      eventResults.value = (eventPaged.Items || eventPaged.items || []).map(ev => ({
        id: ev.id || ev.Id,
        name: ev.name || ev.Name,
        description: ev.description || ev.Description,
        logoUrl: ev.logoUrl || ev.LogoUrl || null,
        registeredTeamsCount: ev.registeredTeamsCount || ev.RegisteredTeamsCount || 0,
      }))
      eventsCount.value = eventPaged.TotalCount ?? eventPaged.totalCount ?? eventResults.value.length

      articleResults.value = (articlePaged.Items || articlePaged.items || []).map(a => ({
        id: a.id || a.Id,
        title: a.title || a.Title,
        contentMarkdown: a.contentMarkdown || a.ContentMarkdown,
        authorName: a.authorName || a.AuthorName || '',
        authorUserId: a.authorUserId || a.AuthorUserId || null,
      }))
      articlesCount.value = articlePaged.TotalCount ?? articlePaged.totalCount ?? articleResults.value.length
    } else if (type === 'teams') {
      const teamPaged = res || {}
      results.value = teamPaged.Items || teamPaged.items || []
      totalCount.value = teamPaged.TotalCount ?? teamPaged.totalCount ?? results.value.length
      eventResults.value = []
      eventsCount.value = 0
      articleResults.value = []
      articlesCount.value = 0
    } else if (type === 'events') {
      const eventPaged = res || {}
      eventResults.value = (eventPaged.Items || eventPaged.items || []).map(ev => ({
        id: ev.id || ev.Id,
        name: ev.name || ev.Name,
        description: ev.description || ev.Description,
        logoUrl: ev.logoUrl || ev.LogoUrl || null,
        registeredTeamsCount: ev.registeredTeamsCount || ev.RegisteredTeamsCount || 0,
      }))
      eventsCount.value = eventPaged.TotalCount ?? eventPaged.totalCount ?? eventResults.value.length
      results.value = []
      totalCount.value = 0
      articleResults.value = []
      articlesCount.value = 0
    } else if (type === 'articles') {
      const articlePaged = res || {}
      articleResults.value = (articlePaged.Items || articlePaged.items || []).map(a => ({
        id: a.id || a.Id,
        title: a.title || a.Title,
        contentMarkdown: a.contentMarkdown || a.ContentMarkdown,
        authorName: a.authorName || a.AuthorName || '',
        authorUserId: a.authorUserId || a.AuthorUserId || null,
      }))
      articlesCount.value = articlePaged.TotalCount ?? articlePaged.totalCount ?? articleResults.value.length
      results.value = []
      totalCount.value = 0
      eventResults.value = []
      eventsCount.value = 0
    }
  } catch (e) {
    error.value = e?.message || '搜索失败'
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  const initial = route.query?.q || route.query?.name || ''
  const initialType = route.query?.type ? String(route.query.type) : 'all'
  if ([ 'all', 'teams', 'events', 'articles' ].includes(initialType)) {
    searchType.value = initialType
  }
  if (initial) {
    query.value = String(initial)
    page.value = 1
    doSearch()
  }
})

async function doLikeList(teamId) {
  try {
    const res = await likeTeam(teamId)
    const newLikes = res.likes ?? res.Likes ?? null
    results.value = results.value.map(r => r.id === teamId ? { ...r, likes: newLikes ?? r.likes } : r)
    likeFxTeamId.value = teamId
    setTimeout(() => { if (likeFxTeamId.value === teamId) likeFxTeamId.value = null }, 3200)
  } catch (e) {
    error.value = e?.message || '点赞失败'
  }
}

function toMd(s) {
  return renderMarkdown(s || '')
}

function isExpandedTeam(id) {
  return !!expandedTeamMap.value[id]
}
function toggleExpandedTeam(id) {
  expandedTeamMap.value[id] = !expandedTeamMap.value[id]
}
</script>

<style scoped>
.like-anim-container { position: relative; display: inline-block }
.like-anim { position: absolute; left: 50%; transform: translateX(-50%); top: -120px; pointer-events: none; z-index: 2; will-change: opacity, transform; animation: hoverPulse 3.2s ease-out forwards }
@keyframes hoverPulse { 0% { opacity: 0; transform: translate(-50%, -10px) scale(0.92) } 12% { opacity: 1; transform: translate(-50%, 0) scale(1) } 35% { transform: translate(-50%, -6px) } 68% { transform: translate(-50%, -3px) } 92% { transform: translate(-50%, -5px) } 100% { opacity: 0; transform: translate(-50%, -5px) } }
</style>

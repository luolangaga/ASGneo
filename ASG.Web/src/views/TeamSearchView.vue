<template>
  <PageHero title="综合搜索" subtitle="按关键词搜索战队、赛事与文章" icon="search">
    <template #actions>
      <v-btn variant="text" class="mb-3" to="/teams/create" prepend-icon="person_add">创建战队</v-btn>
    </template>
  </PageHero>
  <v-container class="py-6">
    <v-card class="mb-4">
      <v-card-title class="d-flex align-center">
        <v-text-field
          v-model="query"
          class="flex-grow-1"
          label="搜索站内内容（战队 / 赛事 / 文章）"
          prepend-inner-icon="search"
          clearable
          hide-details
          @keyup.enter="doSearch"
        />
        <v-select
          v-model="searchType"
          :items="typeOptions"
          class="ml-2"
          label="搜索类型"
          density="comfortable"
          hide-details
        />
        <v-btn
          class="ml-2"
          color="primary"
          :loading="loading"
          :disabled="!queryTrimmed"
          @click="doSearch"
          prepend-icon="search"
        >搜索</v-btn>
      </v-card-title>
      <v-card-text class="d-flex flex-wrap gap-4 text-medium-emphasis">
        <template v-if="searchType === 'all'">
          <div>
            战队：{{ totalCount }} 条（第 {{ page }} 页 / 每页 {{ pageSize }} 条）
          </div>
          <div>赛事：{{ eventsCount }} 条</div>
          <div>文章：{{ articlesCount }} 条</div>
        </template>
        <template v-else-if="searchType === 'teams'">
          <div>
            战队：{{ totalCount }} 条（第 {{ page }} 页 / 每页 {{ pageSize }} 条）
          </div>
        </template>
        <template v-else-if="searchType === 'events'">
          <div>赛事：{{ eventsCount }} 条</div>
        </template>
        <template v-else-if="searchType === 'articles'">
          <div>文章：{{ articlesCount }} 条</div>
        </template>
      </v-card-text>
    </v-card>

    <v-alert v-if="error" type="error" class="mb-4" :text="error" />

    <v-row v-if="loading" class="mb-4" dense>
      <v-col v-for="n in 6" :key="n" cols="12" sm="6" md="4" lg="3">
        <v-skeleton-loader type="card" />
      </v-col>
    </v-row>
    <!-- 战队结果 -->
    <v-card class="mb-3" v-if="!loading && (searchType === 'all' || searchType === 'teams')">
      <v-card-title class="d-flex align-center">
        <v-icon class="mr-2" icon="group" /> 战队结果
      </v-card-title>
    </v-card>
    <v-row v-if="results.length && (searchType === 'all' || searchType === 'teams')" dense>
      <v-col v-for="team in results" :key="team.id" cols="12" sm="6" md="4" lg="3">
        <v-card>
          <v-card-item>
            <div class="d-flex align-center">
              <v-avatar size="40" class="mr-3" color="primary" variant="tonal">
                <template v-if="team.logoUrl">
                  <v-img :src="team.logoUrl" cover />
                </template>
                <template v-else>
                  <span class="text-subtitle-2">{{ getAvatarLetter(team.name) }}</span>
                </template>
              </v-avatar>
              <div>
                <v-card-title class="pa-0">{{ team.name }}</v-card-title>
                <v-card-subtitle class="pa-0">👍 {{ team.likes ?? 0 }}</v-card-subtitle>
              </div>
          </div>
        </v-card-item>
          <v-card-text class="text-body-2">
            <div v-if="team.description" :class="['md-content', { 'md-truncate': !isExpandedTeam(team.id) }]" v-html="toMd(team.description)"></div>
            <div v-else class="text-medium-emphasis">暂无战队描述</div>
            <div v-if="(team.description?.length || 0) > 220" class="mt-1">
              <v-btn size="x-small" variant="text" @click="toggleExpandedTeam(team.id)">{{ isExpandedTeam(team.id) ? '收起' : '展开' }}</v-btn>
            </div>
          </v-card-text>
          <v-card-actions>
            <v-btn
              color="default"
              variant="text"
              :to="{ name: 'team-detail', params: { id: team.id } }"
              prepend-icon="visibility"
            >查看详情</v-btn>
            <v-spacer />
            <v-btn color="secondary" variant="tonal" @click="doLikeList(team.id)" prepend-icon="thumb_up_off_alt">点赞</v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>
    <v-card v-else-if="searchType === 'all' || searchType === 'teams'" class="pa-8 text-center">
      <v-icon size="40" color="primary" icon="group" />
      <div class="text-h6 mt-3">没有找到匹配的战队</div>
      <div class="text-medium-emphasis">试试换个关键词或检查拼写</div>
    </v-card>

    <!-- 赛事结果 -->
    <v-card class="mb-3" v-if="!loading && (searchType === 'all' || searchType === 'events')">
      <v-card-title class="d-flex align-center">
        <v-icon class="mr-2" icon="grid_view" /> 赛事结果
      </v-card-title>
    </v-card>
    <v-row v-if="eventResults.length && (searchType === 'all' || searchType === 'events')" dense>
      <v-col v-for="ev in eventResults" :key="ev.id" cols="12" sm="6" md="4" lg="3">
        <v-card>
          <v-card-title class="d-flex align-center">
            <v-avatar size="36" class="mr-2">
              <v-img v-if="ev.logoUrl" :src="ev.logoUrl" alt="event logo" cover />
              <v-icon v-else icon="grid_view" />
            </v-avatar>
            <div class="flex-grow-1">
              <div class="text-subtitle-1">{{ ev.name }}</div>
              <div class="text-medium-emphasis">报名队伍：{{ ev.registeredTeamsCount ?? 0 }}</div>
            </div>
          </v-card-title>
          <v-card-text>
            <div v-if="ev.description" class="text-body-2 md-truncate" v-html="toMd(ev.description)" />
            <div v-else class="text-medium-emphasis">暂无赛事简介</div>
          </v-card-text>
          <v-card-actions>
            <v-btn color="default" variant="text" :to="`/events/${ev.id}`" prepend-icon="visibility">查看详情</v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>
    <v-card v-else-if="searchType === 'all' || searchType === 'events'" class="pa-6 text-center">
      <v-icon size="32" color="primary" icon="grid_view" />
      <div class="mt-2 text-medium-emphasis">没有找到匹配的赛事</div>
    </v-card>

    <!-- 文章结果 -->
    <v-card class="mb-3" v-if="!loading && (searchType === 'all' || searchType === 'articles')">
      <v-card-title class="d-flex align-center">
        <v-icon class="mr-2" icon="article" /> 文章结果
      </v-card-title>
    </v-card>
    <v-row v-if="articleResults.length && (searchType === 'all' || searchType === 'articles')" dense>
      <v-col v-for="a in articleResults" :key="a.id" cols="12" sm="6" md="4" lg="3">
        <v-card>
          <v-card-title class="d-flex align-center">
            <v-icon class="mr-2" icon="article" />
            <div class="text-subtitle-1">{{ a.title }}</div>
          </v-card-title>
          <v-card-text>
            <div class="text-medium-emphasis">作者：{{ a.authorName || '匿名' }}</div>
            <div v-if="a.contentMarkdown" class="text-body-2 md-truncate" v-html="toMd(a.contentMarkdown)" />
          </v-card-text>
          <v-card-actions>
            <v-btn color="default" variant="text" :to="`/articles/${a.id}`" prepend-icon="visibility">查看详情</v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>
    <v-card v-else-if="searchType === 'all' || searchType === 'articles'" class="pa-6 text-center">
      <v-icon size="32" color="primary" icon="article" />
      <div class="mt-2 text-medium-emphasis">没有找到匹配的文章</div>
    </v-card>

    <div class="d-flex justify-center mt-4" v-if="showPagination">
      <v-pagination v-model="page" :length="maxPage" total-visible="7" @update:modelValue="doSearch" />
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
</style>
<template>
  <PageHero title="搜索战队" subtitle="按名称搜索并查看战队详情" icon="search">
    <template #actions>
      <v-btn variant="text" class="mb-3" to="/teams/create" prepend-icon="person_add">创建战队</v-btn>
    </template>
  </PageHero>
  <v-container class="py-6">
    <v-card class="mb-4">
      <v-card-title class="d-flex align-center">
        <v-text-field
          v-model="query"
          label="搜索战队名称"
          prepend-inner-icon="search"
          clearable
          hide-details
          @keyup.enter="doSearch"
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
      <v-card-text class="d-flex justify-space-between text-medium-emphasis">
        <div>共 {{ totalCount }} 条结果</div>
        <div>第 {{ page }} 页 / 每页 {{ pageSize }} 条</div>
      </v-card-text>
    </v-card>

    <v-alert v-if="error" type="error" class="mb-4" :text="error" />

    <v-row v-if="loading" class="mb-4" dense>
      <v-col v-for="n in 6" :key="n" cols="12" sm="6" md="4" lg="3">
        <v-skeleton-loader type="card" />
      </v-col>
    </v-row>

    <v-row v-else-if="results.length" dense>
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
            <v-btn color="default" variant="text" @click="openDetails(team)" prepend-icon="visibility">查看详情</v-btn>
            <v-spacer />
            <v-btn color="secondary" variant="tonal" @click="doLikeList(team.id)" prepend-icon="thumb_up_off_alt">点赞</v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>

    <v-card v-else class="pa-8 text-center">
      <v-icon size="40" color="primary" icon="group" />
      <div class="text-h6 mt-3">没有找到匹配的战队</div>
      <div class="text-medium-emphasis">试试换个关键词或检查拼写</div>
    </v-card>

    <div class="d-flex justify-center mt-4" v-if="totalCount > pageSize">
      <v-pagination v-model="page" :length="maxPage" total-visible="7" @update:modelValue="doSearch" />
    </div>

    <!-- 详情弹窗 -->
    <v-dialog v-model="detailsOpen" max-width="760">
      <v-card>
        <v-card-title class="d-flex align-center">
          <v-avatar size="56" class="mr-3" color="primary" variant="tonal">
            <template v-if="selectedTeam?.logoUrl">
              <v-img :src="selectedTeam.logoUrl" cover />
            </template>
            <template v-else>
              <span class="text-subtitle-1">{{ getAvatarLetter(selectedTeam?.name) }}</span>
            </template>
          </v-avatar>
          <div class="flex-grow-1">
            <div class="text-h6">{{ selectedTeam?.name }}</div>
            <div class="text-medium-emphasis">👍 {{ selectedTeam?.likes ?? 0 }}</div>
          </div>
          <v-btn icon="close" variant="text" @click="closeDetails" />
        </v-card-title>
        <v-card-text>
          <div class="mb-2 md-content" v-if="selectedTeam?.description" v-html="toMd(selectedTeam.description)"></div>
          <v-divider class="my-4" />
          <div>
            <div class="text-subtitle-1 mb-2">队员</div>
            <v-list v-if="selectedTeam?.players?.length">
              <v-list-item v-for="p in selectedTeam.players" :key="p.id">
                <v-list-item-title>{{ p.name }}</v-list-item-title>
                <v-list-item-subtitle>
                  <span v-if="p.gameId">ID：{{ p.gameId }}</span>
                  <span v-if="p.gameRank" class="ml-2">段位：{{ p.gameRank }}</span>
                </v-list-item-subtitle>
              </v-list-item>
            </v-list>
            <v-alert v-else type="info" variant="tonal" text="暂无队员信息" />
          </div>
          <v-divider class="my-4" />
          <div>
            <div class="text-subtitle-1 mb-2">战队荣誉</div>
            <template v-if="honorsLoading">
              <v-progress-linear indeterminate color="primary" />
            </template>
            <v-alert v-if="honorsError" type="error" :text="honorsError" class="mb-2" />
            <template v-if="selectedHonors?.length">
              <v-list density="comfortable">
                <v-list-item v-for="e in selectedHonors" :key="e.id || e.Id">
                  <template #prepend>
                    <v-avatar size="34" v-if="e.logoUrl || e.LogoUrl">
                      <v-img :src="e.logoUrl || e.LogoUrl" cover />
                    </v-avatar>
                  </template>
                  <v-list-item-title>{{ e.name || e.Name }}</v-list-item-title>
                  <v-list-item-subtitle>冠军</v-list-item-subtitle>
                </v-list-item>
              </v-list>
            </template>
            <v-alert v-else-if="!honorsLoading && !honorsError" type="info" variant="tonal" text="暂无荣誉" />
          </div>
        </v-card-text>
        <v-card-actions>
          <v-btn :loading="likeLoading" color="primary" @click="doLike(selectedTeam?.id)" prepend-icon="thumb_up">点赞</v-btn>
          <v-spacer />
          <v-btn variant="text" @click="closeDetails">关闭</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<script setup>
import { ref, computed } from 'vue'
import { searchTeamsByName, getTeam, getTeamHonors, likeTeam } from '../services/teams'
import PageHero from '../components/PageHero.vue'
import { renderMarkdown } from '../utils/markdown'

const query = ref('')
const page = ref(1)
const pageSize = ref(12)
const totalCount = ref(0)
const results = ref([])
const loading = ref(false)
const error = ref('')

const detailsOpen = ref(false)
const selectedTeam = ref(null)
const selectedHonors = ref([])
const honorsLoading = ref(false)
const honorsError = ref('')
const likeLoading = ref(false)
const expandedTeamMap = ref({})

const queryTrimmed = computed(() => query.value.trim())
const maxPage = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize.value)))

function getAvatarLetter(name) {
  if (!name || typeof name !== 'string') return '?'
  return name.trim().charAt(0).toUpperCase()
}

async function doSearch() {
  error.value = ''
  if (!queryTrimmed.value) return
  loading.value = true
  try {
    const res = await searchTeamsByName(queryTrimmed.value, page.value, pageSize.value)
    const items = (res.Items || res.items || [])
    totalCount.value = res.TotalCount ?? res.totalCount ?? items.length
    results.value = items
  } catch (e) {
    error.value = e?.message || '搜索失败'
  } finally {
    loading.value = false
  }
}

function openDetails(team) {
  detailsOpen.value = true
  fetchTeamDetails(team.id)
}

function closeDetails() {
  detailsOpen.value = false
  selectedTeam.value = null
  selectedHonors.value = []
  honorsLoading.value = false
  honorsError.value = ''
}

async function fetchTeamDetails(id) {
  try {
    selectedTeam.value = await getTeam(id)
    honorsLoading.value = true
    honorsError.value = ''
    try {
      selectedHonors.value = await getTeamHonors(id)
    } catch (e2) {
      honorsError.value = e2?.message || '获取战队荣誉失败'
    } finally {
      honorsLoading.value = false
    }
  } catch (e) {
    error.value = e?.message || '获取战队详情失败'
  }
}

async function doLike(teamId) {
  if (!teamId) return
  likeLoading.value = true
  try {
    const res = await likeTeam(teamId)
    const newLikes = res.likes ?? res.Likes ?? null
    if (selectedTeam.value && typeof newLikes === 'number') {
      selectedTeam.value.likes = newLikes
    }
    results.value = results.value.map(r => r.id === teamId ? { ...r, likes: newLikes ?? r.likes } : r)
  } catch (e) {
    error.value = e?.message || '点赞失败'
  } finally {
    likeLoading.value = false
  }
}

async function doLikeList(teamId) {
  try {
    const res = await likeTeam(teamId)
    const newLikes = res.likes ?? res.Likes ?? null
    results.value = results.value.map(r => r.id === teamId ? { ...r, likes: newLikes ?? r.likes } : r)
    if (selectedTeam.value?.id === teamId && typeof newLikes === 'number') {
      selectedTeam.value.likes = newLikes
    }
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
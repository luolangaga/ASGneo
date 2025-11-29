<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import PageHero from '../components/PageHero.vue'
import { getArticles, likeArticle } from '../services/articles'
import { getUser } from '../services/user'
import { isAuthenticated } from '../stores/auth'

const router = useRouter()
const loading = ref(false)
const errorMsg = ref('')
const items = ref([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = ref(10)
const authorMap = ref({})
const keyword = ref('')

const heroTitle = computed(() => '社区论坛')
const loggedIn = computed(() => isAuthenticated.value)

async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    const q = keyword.value.trim()
    const res = await getArticles({ query: q, page: page.value, pageSize: pageSize.value })
    // 兼容后端返回的属性命名
    const itemsArr = res.items || res.Items || []
    items.value = itemsArr
    totalCount.value = res.totalCount || res.TotalCount || itemsArr.length

    const ids = Array.from(new Set(itemsArr
      .map(a => a.authorUserId || a.AuthorUserId)
      .filter(Boolean)))
    await Promise.all(ids.map(async (uid) => {
      if (!authorMap.value[uid]) {
        try {
          const u = await getUser(uid)
          authorMap.value[uid] = u
        } catch {
          authorMap.value[uid] = null
        }
      }
    }))
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载帖子失败'
  } finally {
    loading.value = false
  }
}

function onSearch() {
  page.value = 1
  load()
}

function goCreate() {
  router.push('/articles/create')
}

function goDetail(id) {
  router.push(`/articles/${id}`)
}

function toDateStr(s) {
  const d = s ? new Date(s) : null
  return d ? d.toLocaleString() : ''
}

function initials(name) {
  const parts = String(name || '').split(/\s+/).filter(Boolean)
  const ini = parts.slice(0, 2).map(p => p[0]).join('')
  return ini || 'NA'
}

onMounted(load)

const likingIds = ref(new Set())
async function onLike(a) {
  const id = a.id || a.Id
  if (!id || likingIds.value.has(id)) return
  likingIds.value.add(id)
  try {
    const newLikes = await likeArticle(id)
    if ('likes' in a) a.likes = newLikes
    else if ('Likes' in a) a.Likes = newLikes
    else a.likes = newLikes
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '点赞失败'
  } finally {
    likingIds.value.delete(id)
  }
}
</script>

<template>
  <PageHero :title="heroTitle" subtitle="浏览社区帖子，了解最新动态" icon="article">
    <template #actions>
      <v-btn v-if="loggedIn" class="mb-3" color="primary" variant="tonal" @click="goCreate" prepend-icon="edit">发布帖子</v-btn>
    </template>
  </PageHero>

  <v-container class="py-8 page-container">
    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" variant="tonal" />
    
    <div class="d-flex align-center mb-8">
      <v-text-field
        v-model="keyword"
        class="flex-grow-1"
        label="搜索帖子关键字"
        prepend-inner-icon="search"
        variant="outlined"
        density="comfortable"
        clearable
        hide-details
        @keyup.enter="onSearch"
        style="max-width: 600px"
      />
      <v-btn class="ml-3 px-6" color="primary" height="48" @click="onSearch" prepend-icon="search">搜索</v-btn>
    </div>

    <v-row v-if="loading" class="mb-4">
      <v-col v-for="n in 6" :key="n" cols="12" sm="6" md="4" lg="3">
        <v-skeleton-loader type="card" rounded="xl" />
      </v-col>
    </v-row>

    <v-row v-else-if="items.length" dense>
      <v-col v-for="a in items" :key="a.id || a.Id" cols="12" sm="6" md="4" lg="3">
        <v-card class="article-card h-100 hover-elevate" variant="flat" border rounded="xl">
          <v-card-item class="pa-4">
            <router-link :to="`/articles/${a.id || a.Id}`" class="text-subtitle-1 font-weight-bold mb-3 d-inline-block text-decoration-none text-high-emphasis article-title">
              {{ a.title || a.Title }}
            </router-link>
            
            <div class="d-flex align-center mb-3">
              <v-avatar size="32" class="mr-2" color="surface-variant">
                <template v-if="authorMap[a.authorUserId || a.AuthorUserId]?.avatarUrl || authorMap[a.authorUserId || a.AuthorUserId]?.AvatarUrl">
                  <v-img :src="authorMap[a.authorUserId || a.AuthorUserId]?.avatarUrl || authorMap[a.authorUserId || a.AuthorUserId]?.AvatarUrl" alt="作者头像" cover />
                </template>
                <template v-else>
                  <span class="text-caption font-weight-bold text-primary">{{ initials(a.authorName || a.AuthorName) }}</span>
                </template>
              </v-avatar>
              <div class="flex-grow-1 text-truncate">
                <div class="text-caption font-weight-medium">
                  <router-link
                    v-if="a.authorUserId || a.AuthorUserId"
                    :to="`/users/${a.authorUserId || a.AuthorUserId}`"
                    class="text-decoration-none text-primary"
                  >{{ a.authorName || a.AuthorName || '未知' }}</router-link>
                  <template v-else>
                    {{ a.authorName || a.AuthorName || '未知' }}
                  </template>
                </div>
                <div class="text-caption text-medium-emphasis text-truncate">
                  <template v-if="a.authorTeamName || a.AuthorTeamName">
                    <v-icon icon="groups" size="x-small" class="mr-1" />
                    <router-link :to="{ name: 'team-search', query: { q: a.authorTeamName || a.AuthorTeamName } }" class="text-decoration-none text-medium-emphasis">{{ a.authorTeamName || a.AuthorTeamName }}</router-link>
                  </template>
                </div>
              </div>
            </div>
            
            <div class="text-caption text-disabled d-flex align-center">
              <v-icon icon="schedule" size="x-small" class="mr-1" />
              {{ toDateStr(a.createdAt || a.CreatedAt) }}
            </div>
          </v-card-item>
          
          <v-divider class="mx-4 opacity-20" />
          
          <v-card-actions class="pa-4">
            <div class="d-flex align-center gap-2">
              <v-btn size="small" :loading="likingIds.has(a.id || a.Id)" color="pink" variant="text" @click="onLike(a)" prepend-icon="favorite" class="px-2" :active="false">
                {{ (a.likes ?? a.Likes ?? 0) }}
              </v-btn>
              <div class="text-caption text-medium-emphasis d-flex align-center">
                <v-icon icon="visibility" size="small" class="mr-1" />
                {{ (a.views ?? a.Views ?? 0) }}
              </div>
            </div>
            <v-spacer />
            <v-btn variant="tonal" color="primary" size="small" :to="`/articles/${a.id || a.Id}`" prepend-icon="arrow_forward">查看</v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>

    <div v-else class="pa-12 text-center border rounded-xl border-dashed text-medium-emphasis">
      <v-icon size="64" color="medium-emphasis" icon="forum" class="mb-4" />
      <div class="text-h6 font-weight-bold mb-2">暂无帖子</div>
      <div class="text-body-2">登录后可以发布属于你的首篇帖子，分享你的观点。</div>
      <v-btn v-if="loggedIn" color="primary" class="mt-4" @click="goCreate" prepend-icon="edit">立即发布</v-btn>
    </div>

    <div class="d-flex justify-center mt-8" v-if="totalCount > pageSize">
      <v-pagination v-model="page" :length="Math.ceil(totalCount / pageSize)" total-visible="7" @update:modelValue="load" rounded="circle" density="comfortable" />
    </div>
  </v-container>
</template>

<style scoped>
.article-card { display: flex; flex-direction: column; }
.article-card .v-card-actions { margin-top: auto; }
.article-title {
  line-height: 1.4;
  height: 2.8em;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical; 
}
</style>
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

const heroTitle = computed(() => '社区文章')
const loggedIn = computed(() => isAuthenticated.value)

async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    const res = await getArticles({ page: page.value, pageSize: pageSize.value })
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
    errorMsg.value = err?.payload?.message || err?.message || '加载文章失败'
  } finally {
    loading.value = false
  }
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
  <PageHero :title="heroTitle" subtitle="浏览社区文章，了解最新动态" icon="article">
    <template #actions>
      <v-btn v-if="loggedIn" class="mb-3" color="primary" variant="tonal" @click="goCreate" prepend-icon="edit">发布文章</v-btn>
    </template>
  </PageHero>

  <v-container class="py-6">
    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
    <v-row v-if="loading" class="mb-4" dense>
      <v-col v-for="n in 6" :key="n" cols="12" sm="6" md="4" lg="3">
        <v-skeleton-loader type="card" />
      </v-col>
    </v-row>

    <v-row v-else-if="items.length" dense>
      <v-col v-for="a in items" :key="a.id || a.Id" cols="12" sm="6" md="4" lg="3">
        <v-card>
          <v-card-item class="pa-3">
            <div class="text-h6 mb-2">{{ a.title || a.Title }}</div>
            <div class="d-flex align-center">
              <v-avatar size="28" class="mr-2">
                <template v-if="authorMap[a.authorUserId || a.AuthorUserId]?.avatarUrl || authorMap[a.authorUserId || a.AuthorUserId]?.AvatarUrl">
                  <v-img :src="authorMap[a.authorUserId || a.AuthorUserId]?.avatarUrl || authorMap[a.authorUserId || a.AuthorUserId]?.AvatarUrl" alt="作者头像" cover />
                </template>
                <template v-else>
                  <span class="text-caption">{{ initials(a.authorName || a.AuthorName) }}</span>
                </template>
              </v-avatar>
              <div class="flex-grow-1">
                <div class="text-caption">{{ a.authorName || a.AuthorName || '未知' }}</div>
                <div class="text-caption text-medium-emphasis">
                  战队：{{ a.authorTeamName || a.AuthorTeamName || '无' }}
                </div>
              </div>
            </div>
            <div class="text-caption text-disabled mt-2">发表于：{{ toDateStr(a.createdAt || a.CreatedAt) }}</div>
          </v-card-item>
          <v-card-actions>
            <v-btn size="small" :loading="likingIds.has(a.id || a.Id)" color="primary" variant="tonal" @click="onLike(a)" prepend-icon="favorite">
              点赞 {{ (a.likes ?? a.Likes ?? 0) }}
            </v-btn>
            <v-chip size="small" class="ml-2" color="default" variant="tonal">
              <v-icon start icon="visibility" /> 浏览 {{ (a.views ?? a.Views ?? 0) }}
            </v-chip>
            <v-spacer />
            <v-btn variant="text" :to="`/articles/${a.id || a.Id}`" prepend-icon="visibility">查看详情</v-btn>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>

    <v-card v-else class="pa-8 text-center">
      <v-icon size="40" color="primary" icon="article" />
      <div class="text-h6 mt-3">暂无文章</div>
      <div class="text-medium-emphasis">登录后可以发布属于你的首篇文章</div>
    </v-card>

    <div class="d-flex justify-center mt-4" v-if="totalCount > pageSize">
      <v-pagination v-model="page" :length="Math.ceil(totalCount / pageSize)" total-visible="7" @update:modelValue="load" />
    </div>
  </v-container>
</template>

<style scoped>
</style>
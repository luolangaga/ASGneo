<script setup>
import { ref, reactive, onMounted, watch } from 'vue'
import { getArticles, createArticle, getArticle, likeArticle, addArticleView } from '../services/articles'
import { notifySuccess, notifyError } from '../notify'

const loading = ref(false)
const errorMsg = ref('')
const keyword = ref('')
const sortBy = ref('createdAt')
const desc = ref(true)
const page = ref(1)
const pageSize = ref(10)
const total = ref(0)
const items = ref([])

const createDialog = ref(false)
const form = reactive({ title: '', contentMarkdown: '' })

onMounted(() => { fetchData() })
watch([page, pageSize], () => fetchData())

async function fetchData() {
  loading.value = true
  errorMsg.value = ''
  try {
    const res = await getArticles({ query: keyword.value || '', page: page.value, pageSize: pageSize.value, sortBy: sortBy.value || null, desc: desc.value })
    items.value = res?.Items ?? res?.items ?? []
    total.value = res?.TotalCount ?? res?.totalCount ?? items.value.length
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载失败'
  } finally { loading.value = false }
}

function openCreate() {
  Object.assign(form, { title: '', contentMarkdown: '' })
  createDialog.value = true
}

async function submitCreate() {
  try {
    const dto = { Title: form.title, ContentMarkdown: form.contentMarkdown }
    await createArticle(dto)
    notifySuccess('文章创建成功')
    createDialog.value = false
    await fetchData()
  } catch (err) { notifyError(err?.payload?.message || err?.message || '创建失败') }
}

async function onLike(item) {
  try { await likeArticle(item.Id || item.id); notifySuccess('点赞成功'); await fetchData() } catch (err) { notifyError(err?.payload?.message || err?.message || '点赞失败') }
}
async function onAddView(item) {
  try { await addArticleView(item.Id || item.id); await fetchData() } catch (err) { /* 静默失败 */ }
}

function fmtDate(v) {
  if (!v) return ''
  const d = new Date(v)
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}
</script>
<template>
  <v-container class="py-6" style="max-width: 1200px">
    <div class="d-flex align-center mb-4">
      <div class="text-h5">文章管理</div>
      <v-spacer></v-spacer>
      <v-text-field v-model="keyword" density="compact" placeholder="搜索标题/内容" append-inner-icon="mdi-magnify" class="mr-2" style="max-width: 280px" @keyup.enter="fetchData" />
      <v-btn color="primary" prepend-icon="mdi-magnify" @click="fetchData">搜索</v-btn>
      <v-divider vertical class="mx-4" />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">创建文章</v-btn>
    </div>

    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />

    <v-data-table :items="items" :items-length="total" :page.sync="page" :items-per-page="pageSize" :loading="loading" class="elevation-1">
      <template #headers>
        <tr>
          <th class="text-left">标题</th>
          <th class="text-left">作者</th>
          <th class="text-left">创建时间</th>
          <th class="text-left">浏览</th>
          <th class="text-left">点赞</th>
          <th class="text-left">评论</th>
          <th class="text-left">操作</th>
        </tr>
      </template>
      <template #item="{ item }">
        <tr>
          <td>{{ item.Title || item.title }}</td>
          <td>{{ item.AuthorName || item.authorName }}</td>
          <td>{{ fmtDate(item.CreatedAt || item.createdAt) }}</td>
          <td>{{ item.Views ?? item.views ?? 0 }}</td>
          <td>{{ item.Likes ?? item.likes ?? 0 }}</td>
          <td>{{ item.CommentsCount ?? item.commentsCount ?? 0 }}</td>
          <td>
            <v-btn variant="text" prepend-icon="mdi-eye" @click="onAddView(item)">增加浏览</v-btn>
            <v-btn variant="text" prepend-icon="mdi-thumb-up" @click="onLike(item)">点赞</v-btn>
          </td>
        </tr>
      </template>
      <template #bottom>
        <div class="d-flex align-center pa-2">
          <v-spacer></v-spacer>
          <v-select :items="[5,10,20,50]" v-model="pageSize" density="compact" style="max-width: 120px" />
        </div>
      </template>
    </v-data-table>

    <!-- 创建文章 -->
    <v-dialog v-model="createDialog" max-width="820">
      <v-card>
        <v-card-title>创建文章</v-card-title>
        <v-card-text>
          <v-text-field v-model="form.title" label="标题" required />
          <v-textarea v-model="form.contentMarkdown" label="内容(Markdown)" rows="12" auto-grow />
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn variant="text" @click="createDialog=false">取消</v-btn>
          <v-btn color="primary" @click="submitCreate">保存</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>
<style scoped>
</style>
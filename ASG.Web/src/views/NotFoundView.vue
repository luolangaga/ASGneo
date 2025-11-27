<script setup>
import PageHero from '../components/PageHero.vue'
import { ref } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()
const keyword = ref('')

function goSearch() {
  const q = keyword.value.trim()
  if (q) router.push({ name: 'team-search', query: { q } })
  else router.push({ name: 'team-search' })
}
</script>

<template>
  <PageHero
    title="页面未找到（404）"
    subtitle="可能链接有误、内容已迁移，或权限不足。"
    icon="sentiment_dissatisfied"
  >
    <template #actions>
      <v-btn color="primary" prepend-icon="home" to="/">返回首页</v-btn>
      <v-btn variant="tonal" prepend-icon="grid_view" to="/events">去赛事看板</v-btn>
      <v-btn variant="text" prepend-icon="article" to="/articles">去文章列表</v-btn>
    </template>
  </PageHero>

  <v-container class="py-8" style="max-width: 1000px">
    <v-row justify="center">
      <v-col cols="12" md="8">
        <v-card class="pa-6">
          <div class="text-h6 mb-2">试试站内搜索</div>
          <div class="text-medium-emphasis mb-4">输入战队或赛事关键字进行查找</div>
          <v-text-field
            v-model="keyword"
            placeholder="例如：ASG、秋季赛、战队名称"
            prepend-inner-icon="search"
            clearable
            hide-details
            @keyup.enter="goSearch"
          />
          <div class="d-flex gap-2 mt-2">
            <v-btn color="primary" prepend-icon="search" @click="goSearch">搜索</v-btn>
            <v-btn variant="text" prepend-icon="arrow_back" @click="router.back()">返回上一页</v-btn>
          </div>
        </v-card>

        <v-card class="pa-6 mt-6" color="surfaceVariant" variant="tonal">
          <div class="text-subtitle-2 mb-2">可能的原因</div>
          <ul class="text-body-2 pl-4">
            <li>链接拼写错误或已过期</li>
            <li>内容已迁移到新位置</li>
            <li>您可能没有访问该内容的权限</li>
          </ul>
        </v-card>
      </v-col>
    </v-row>
  </v-container>
</template>

<style scoped>
.gap-2 { gap: 8px; }
</style>
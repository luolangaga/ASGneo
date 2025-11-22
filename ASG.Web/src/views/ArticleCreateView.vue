<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import PageHero from '../components/PageHero.vue'
import MarkdownEditor from '../components/MarkdownEditor.vue'
import { createArticle } from '../services/articles'

const router = useRouter()
const saving = ref(false)
const errorMsg = ref('')

const title = ref('')
const content = ref('')

async function onSubmit() {
  saving.value = true
  errorMsg.value = ''
  try {
    const payload = {
      title: title.value.trim(),
      contentMarkdown: content.value?.trim() || ''
    }
    if (!payload.title) {
      errorMsg.value = '请填写帖子标题'
      throw new Error('缺少标题')
    }
    if (!payload.contentMarkdown) {
      errorMsg.value = '请填写帖子内容'
      throw new Error('缺少内容')
    }
    const created = await createArticle(payload)
    const id = created?.id || created?.Id
    if (id) router.push(`/articles/${id}`)
    else router.push('/articles')
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '发布帖子失败'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <PageHero title="发布帖子" subtitle="撰写 Markdown 正文，分享你的观点" icon="edit" />
  <v-container class="py-6" style="max-width: 860px">
    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
    <v-text-field v-model="title" label="标题" prepend-inner-icon="article" class="mb-3" />
    <MarkdownEditor v-model="content" label="正文内容 (Markdown)" class="mb-3" />
    <div class="d-flex justify-end">
      <v-btn color="primary" :loading="saving" @click="onSubmit" prepend-icon="send">发布</v-btn>
    </div>
  </v-container>
</template>

<style scoped>
</style>
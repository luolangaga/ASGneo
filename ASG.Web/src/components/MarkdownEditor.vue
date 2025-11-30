<script setup>
import { computed, ref, nextTick } from 'vue'
import { renderMarkdown } from '../utils/markdown'
import { uploadImage } from '../services/files'

const props = defineProps({
  modelValue: { type: String, default: '' },
  label: { type: String, default: '简介' },
  rows: { type: Number, default: 6 },
  maxLength: { type: Number, default: 999 },
  density: { type: String, default: 'comfortable' },
})
const emit = defineEmits(['update:modelValue'])

const tab = ref('edit')
const inputRef = ref(null)
const fileInputRef = ref(null)
const uploadingImg = ref(false)
const biliDialogOpen = ref(false)
const biliInput = ref('')
const value = computed({
  get: () => props.modelValue || '',
  set: v => emit('update:modelValue', v),
})

const length = computed(() => (value.value || '').length)
const rendered = computed(() => renderMarkdown(value.value || ''))
const limit = computed(() => (props.maxLength && props.maxLength > 0) ? props.maxLength : null)

function toEdit() { tab.value = 'edit' }
function toPreview() { tab.value = 'preview' }

function insertAtCursor(snippet) {
  const s = String(snippet || '')
  try {
    const el = inputRef.value?.$el?.querySelector('textarea') || inputRef.value?.$el?.querySelector('input')
    if (el && typeof el.selectionStart === 'number' && typeof el.selectionEnd === 'number') {
      const start = el.selectionStart
      const end = el.selectionEnd
      const v = value.value || ''
      value.value = v.slice(0, start) + s + v.slice(end)
      nextTick(() => {
        try {
          el.focus()
          const pos = start + s.length
          el.setSelectionRange(pos, pos)
        } catch {}
      })
      return
    }
  } catch {}
  // 兜底：追加到末尾
  value.value = (value.value || '') + s
}

function insertHeading(level = 2) {
  const lvl = Math.min(Math.max(level, 1), 6)
  const prefix = '#'.repeat(lvl)
  insertAtCursor(`\n${prefix} 标题\n`)
}

function insertLink() {
  insertAtCursor(`[链接文本](https://example.com)`)
}

function insertImage() {
  insertAtCursor(`![图片描述](https://example.com/image.png)`)
}

function triggerUpload() {
  try { fileInputRef.value?.click?.() } catch {}
}

async function onUploadImage(e) {
  const file = e?.target?.files?.[0]
  if (!file) return
  uploadingImg.value = true
  try {
    const res = await uploadImage(file)
    const url = res?.url || res?.imageUrl || res?.Url || res?.URL
    if (!url) throw new Error('上传成功，但未返回图片URL')
    insertAtCursor(`![图片](${url})`)
  } catch (err) {
    console.error('上传图片失败', err)
    // 简易反馈：插入占位并提示
    insertAtCursor(`![图片上传失败]()`)
  } finally {
    uploadingImg.value = false
    // 清空选择，便于再次选择同一文件
    try { e.target.value = '' } catch {}
  }
}

function openBiliDialog() {
  biliDialogOpen.value = true
}

function insertBiliFromInput() {
  const raw = String(biliInput.value || '').trim()
  if (!raw) { biliDialogOpen.value = false; return }
  let url = raw
  const bv = raw.match(/\b(BV[0-9A-Za-z]+)/i)
  const av = raw.match(/\bav(\d+)/i)
  if (bv) url = `https://www.bilibili.com/video/${bv[1]}`
  else if (av) url = `https://www.bilibili.com/video/av${av[1]}`
  insertAtCursor(`\n${url}\n`)
  biliDialogOpen.value = false
  biliInput.value = ''
}
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-2">
      <div class="text-caption text-medium-emphasis">
        {{ label }}（支持 Markdown<span v-if="limit">，字数上限 {{ limit }}</span>）
      </div>
      <div>
        <v-btn size="small" variant="text" :class="{ 'text-primary': tab==='edit' }" @click="toEdit">编辑</v-btn>
        <v-btn size="small" variant="text" :class="{ 'text-primary': tab==='preview' }" @click="toPreview">预览</v-btn>
      </div>
    </div>
    <div v-if="tab==='edit'">
      <div class="d-flex align-center gap-2 mb-2">
        <v-btn size="small" variant="tonal" prepend-icon="title" @click="insertHeading(2)">标题</v-btn>
        <v-btn size="small" variant="tonal" prepend-icon="link" @click="insertLink">链接</v-btn>
        <v-btn size="small" variant="tonal" prepend-icon="image" @click="insertImage">图片</v-btn>
        <v-btn size="small" variant="tonal" prepend-icon="smart_display" @click="openBiliDialog">哔哩哔哩视频</v-btn>
        <v-btn size="small" color="primary" variant="tonal" :loading="uploadingImg" prepend-icon="file_upload" @click="triggerUpload">上传图片</v-btn>
        <input ref="fileInputRef" type="file" accept="image/png,image/jpeg,image/jpg,image/webp,image/gif" @change="onUploadImage" style="display:none" />
      </div>
      <v-textarea
        ref="inputRef"
        v-model="value"
        :label="label"
        prepend-inner-icon="text_fields"
        :rows="rows"
        :counter="limit ?? undefined"
        :maxlength="limit ?? undefined"
        :density="density"
      />
    </div>
    <div v-else class="md-preview text-body-2" v-html="rendered" />
    <div class="text-caption text-medium-emphasis mt-1">
      当前字数：{{ length }}<template v-if="limit">（上限 {{ limit }}）</template>
    </div>
  </div>
  <v-dialog v-model="biliDialogOpen" max-width="480">
    <v-card>
      <v-card-title>插入哔哩哔哩视频</v-card-title>
      <v-card-text>
        <v-text-field v-model="biliInput" label="BV号或视频链接" prepend-inner-icon="smart_display" placeholder="例如：BV1xx411c7uA 或 https://www.bilibili.com/video/BV..." @keyup.enter="insertBiliFromInput" />
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="biliDialogOpen = false">取消</v-btn>
        <v-btn color="primary" prepend-icon="add_link" @click="insertBiliFromInput">插入</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
  
</template>

<style scoped>
.md-preview :deep(p){ margin: 0.4rem 0; }
.md-preview :deep(ul){ padding-left: 1.2rem; }
.md-preview :deep(ol){ padding-left: 1.2rem; }
.md-preview :deep(img){ max-width: 100%; height: auto; display: block; }
</style>

<script setup>
import { computed, ref } from 'vue'
import { renderMarkdown } from '../utils/markdown'

const props = defineProps({
  modelValue: { type: String, default: '' },
  label: { type: String, default: '简介' },
  rows: { type: Number, default: 6 },
  maxLength: { type: Number, default: 999 },
  density: { type: String, default: 'comfortable' },
})
const emit = defineEmits(['update:modelValue'])

const tab = ref('edit')
const value = computed({
  get: () => props.modelValue || '',
  set: v => emit('update:modelValue', v),
})

const length = computed(() => (value.value || '').length)
const rendered = computed(() => renderMarkdown(value.value || ''))

function toEdit() { tab.value = 'edit' }
function toPreview() { tab.value = 'preview' }
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-2">
      <div class="text-caption text-medium-emphasis">{{ label }}（支持 Markdown，字数需小于 1000）</div>
      <div>
        <v-btn size="small" variant="text" :class="{ 'text-primary': tab==='edit' }" @click="toEdit">编辑</v-btn>
        <v-btn size="small" variant="text" :class="{ 'text-primary': tab==='preview' }" @click="toPreview">预览</v-btn>
      </div>
    </div>
    <div v-if="tab==='edit'">
      <v-textarea
        v-model="value"
        :label="label"
        prepend-inner-icon="text_fields"
        :rows="rows"
        :counter="maxLength"
        :maxlength="maxLength"
        :density="density"
      />
    </div>
    <div v-else class="md-preview text-body-2" v-html="rendered" />
    <div class="text-caption text-medium-emphasis mt-1">当前字数：{{ length }}（上限 {{ maxLength }}）</div>
  </div>
  
</template>

<style scoped>
.md-preview :deep(p){ margin: 0.4rem 0; }
.md-preview :deep(ul){ padding-left: 1.2rem; }
.md-preview :deep(ol){ padding-left: 1.2rem; }
</style>
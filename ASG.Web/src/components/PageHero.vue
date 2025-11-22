<script setup>
const props = defineProps({
  title: { type: String, required: true },
  subtitle: { type: String, default: '' },
  icon: { type: String, default: 'emoji_events' },
})
</script>

<template>
  <v-sheet class="page-hero pa-6 pa-md-12">
    <v-container>
      <v-row align="center">
        <v-col cols="12" md="7">
          <div class="text-h5 text-md-h3 font-weight-bold mb-2">{{ title }}</div>
          <div v-if="subtitle" class="text-subtitle-2 text-md-subtitle-1 mb-4 mb-md-6" style="opacity: 0.9">{{ subtitle }}</div>
          <div class="actions-wrap d-flex flex-wrap">
            <slot name="actions" />
          </div>
        </v-col>
        <v-col cols="12" md="5" class="d-none d-md-flex justify-center">
          <slot name="media">
            <v-icon :icon="icon" size="120" color="white" class="hero-icon" />
          </slot>
        </v-col>
      </v-row>
    </v-container>
  </v-sheet>
</template>

<style scoped>
.page-hero {
  background: linear-gradient(135deg, #4b6cb7 0%, #182848 100%);
  color: #fff;
}
.hero-icon { opacity: 0.9; }
/* 窄屏动作区间距与滚动优化 */
.actions-wrap { gap: 8px; }
@media (max-width: 600px) {
  .actions-wrap { margin-bottom: 4px; }
}
/* 提升蓝底上的可读性：强制文本按钮/图标/标签为浅色 */
:deep(.page-hero .v-btn--variant-text),
:deep(.page-hero .v-btn--variant-text .v-icon) {
  color: #ffffff !important;
}
:deep(.page-hero .v-btn--variant-tonal) {
  color: #ffffff !important;
  background-color: rgba(255, 255, 255, 0.14) !important;
}
:deep(.page-hero .v-btn--variant-outlined) {
  color: #ffffff !important;
  border-color: rgba(255, 255, 255, 0.7) !important;
}
:deep(.page-hero .v-btn--variant-elevated) {
  color: #ffffff !important;
  background-color: rgba(255, 255, 255, 0.18) !important;
}
:deep(.page-hero .v-chip) {
  color: #ffffff !important;
  background-color: rgba(255, 255, 255, 0.12) !important;
}
:deep(.page-hero .text-medium-emphasis) {
  color: rgba(255, 255, 255, 0.85) !important;
}
</style>
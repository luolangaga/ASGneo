<script setup>
import { ref, onMounted } from 'vue'
import { getRecruitments } from '../services/recruitments'

const loading = ref(false)
const items = ref([])
const keyword = ref('')
const position = ref('')

async function load() {
  loading.value = true
  try { items.value = await getRecruitments({ q: keyword.value, position: position.value }) } finally { loading.value = false }
}

onMounted(load)
</script>

<template>
  <v-container class="py-8 page-container">
    <div class="text-h5 mb-3">招募任务</div>
    <v-row dense class="mb-4">
      <v-col cols="12" md="4"><v-text-field v-model="keyword" label="搜索" prepend-inner-icon="search" @keyup.enter="load" /></v-col>
      <v-col cols="12" md="4"><v-select v-model="position" :items="['Commentator','Director','Referee']" label="职位" prepend-inner-icon="work" /></v-col>
      <v-col cols="12" md="4"><v-btn color="primary" prepend-icon="search" @click="load">筛选</v-btn></v-col>
    </v-row>
    <v-row dense>
      <v-col cols="12" sm="6" md="4" v-for="it in items" :key="it.id">
        <v-card>
          <v-card-title>{{ it.title }}</v-card-title>
          <v-card-text>
            <div class="text-body-2">{{ it.description }}</div>
            <div class="text-caption mt-2">每场 {{ it.payPerMatch }} 元 • {{ it.positionType }}</div>
          </v-card-text>
          <v-card-actions>
            <router-link :to="{ name: 'recruitment-detail', params: { id: it.id } }"><v-btn color="primary" variant="tonal">查看</v-btn></router-link>
          </v-card-actions>
        </v-card>
      </v-col>
    </v-row>
    <v-row v-if="!items.length && !loading"><v-col cols="12"><v-alert type="info" text="暂无招募任务" /></v-col></v-row>
  </v-container>
</template>

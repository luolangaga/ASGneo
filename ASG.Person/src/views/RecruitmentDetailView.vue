<script setup>
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getRecruitment } from '../services/recruitments'
import { getMatches } from '../services/matches'

const route = useRoute()
const router = useRouter()
const id = route.params.id
const loading = ref(false)
const item = ref(null)
const matches = ref([])

async function load() {
  loading.value = true
  try {
    item.value = await getRecruitment(id)
    try {
      const all = await getMatches({ eventId: item.value?.eventId || item.value?.EventId, page: 1, pageSize: 200 })
      const ids = item.value?.matchIds || item.value?.MatchIds || []
      if (ids && ids.length) matches.value = all.filter(m => ids.includes(m.id || m.Id))
      else matches.value = all
    } catch {}
  } finally { loading.value = false }
}

function goApply() { router.push({ name: 'apply', params: { id } }) }

onMounted(load)
</script>

<template>
  <v-container class="py-8 page-container">
    <div class="text-h5 mb-3">任务详情</div>
    <v-skeleton-loader v-if="loading" type="card" />
    <template v-else>
      <v-card>
        <v-card-title>{{ item?.title }}</v-card-title>
        <v-card-text>
          <div class="text-body-2">{{ item?.description }}</div>
          <div class="text-caption mt-2">职位 {{ item?.positionType }} • 每场 {{ item?.payPerMatch }} 元</div>
          <div class="text-subtitle-2 mt-4">关联赛程</div>
          <v-table class="mt-2">
            <thead>
              <tr><th>时间</th><th>主队</th><th>客队</th></tr>
            </thead>
            <tbody>
              <tr v-for="m in matches" :key="m.id || m.Id">
                <td>{{ new Date(m.matchTime || m.MatchTime).toLocaleString() }}</td>
                <td>{{ m.homeTeamName || m.HomeTeamName }}</td>
                <td>{{ m.awayTeamName || m.AwayTeamName }}</td>
              </tr>
            </tbody>
          </v-table>
        </v-card-text>
        <v-card-actions>
          <v-btn color="primary" prepend-icon="send" @click="goApply">申请</v-btn>
        </v-card-actions>
      </v-card>
    </template>
  </v-container>
</template>

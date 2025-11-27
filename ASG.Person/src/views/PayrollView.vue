<script setup>
import { ref } from 'vue'
import { getPayroll } from '../services/payroll'

const rows = ref([])
const loading = ref(false)
const from = ref('')
const to = ref('')

async function load() {
  loading.value = true
  try { rows.value = await getPayroll({ from: from.value, to: to.value }) } finally { loading.value = false }
}

function total() { return rows.value.reduce((s, r) => s + Number(r.amount || 0), 0) }

function exportCsv() {
  const header = '日期,赛事,场次,职位,单价,金额\n'
  const body = rows.value.map(r => [r.date, r.eventName, r.matchTitle, r.positionType, r.payPerMatch, r.amount].join(',')).join('\n')
  const blob = new Blob(["\ufeff" + header + body], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = 'payroll.csv'
  a.click()
  URL.revokeObjectURL(url)
}
</script>

<template>
  <v-container class="py-8 page-container">
    <div class="text-h5 mb-3">工资结算</div>
    <v-row dense class="mb-4">
      <v-col cols="12" md="4"><v-text-field v-model="from" type="date" label="开始日期" /></v-col>
      <v-col cols="12" md="4"><v-text-field v-model="to" type="date" label="结束日期" /></v-col>
      <v-col cols="12" md="4"><v-btn color="primary" prepend-icon="search" @click="load">查询</v-btn></v-col>
    </v-row>
    <v-table>
      <thead>
        <tr>
          <th>日期</th><th>赛事</th><th>场次</th><th>职位</th><th>单价</th><th>金额</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="r in rows" :key="r.id">
          <td>{{ r.date }}</td><td>{{ r.eventName }}</td><td>{{ r.matchTitle }}</td><td>{{ r.positionType }}</td><td>{{ r.payPerMatch }}</td><td>{{ r.amount }}</td>
        </tr>
      </tbody>
    </v-table>
    <div class="mt-4 d-flex align-center">
      <div class="text-subtitle-1">总计：{{ total() }} 元</div>
      <v-spacer />
      <v-btn color="secondary" variant="tonal" prepend-icon="download" @click="exportCsv">导出 CSV</v-btn>
    </div>
  </v-container>
</template>

<script setup>
import { ref, onMounted, watch, nextTick, onBeforeUnmount, computed } from 'vue'
import * as echarts from 'echarts'
import { getMeta, getHeroes, getOverview, getTrend, compareHeroes, getCampTrend } from '../services/stats'

const loadingMeta = ref(false)
const meta = ref({ seasons: [], parts: [], positions: [], camps: [] })

const filters = ref({ season: null, part: null, position: '', campId: null, metric: 'use_rate', avgMode: 'equal_date' })
const campLabel = (id) => {
  const v = Number(id)
  if (v === 1) return '监管者'
  if (v === 2) return '求生者'
  return String(id ?? '')
}
const campOptions = computed(() => (meta.value.camps || []).map(c => ({ title: campLabel(c), value: c })))
const partOptions = computed(() => [{ title: '综合', value: null }, ...((meta.value.parts || []).map(p => ({ title: String(p), value: p })) )])
const heroes = ref([])
const selectedHero = ref(null)
const compareHeroIds = ref([])

const overviewLoading = ref(false)
const overviewItems = ref([])
const overviewCollapsed = ref(true)
const overviewMode = ref('latest')

const filtersInfoOpen = ref(false)
const overviewInfoOpen = ref(false)
const trendInfoOpen = ref(false)
const compareInfoOpen = ref(false)
const tboardInfoOpen = ref(false)
const regTierOpen = ref({ T0: true, T1: true, T2: true, T3: true, T4: true })
const surTierOpen = ref({ T0: true, T1: true, T2: true, T3: true, T4: true })
function pickLatest(item) { return item.metricLatest ?? item.MetricLatest ?? 0 }
function downloadCsv(filename, content) {
  try {
    const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename
    a.click()
    URL.revokeObjectURL(url)
  } catch {}
}
function exportTBoard(campId) {
  const src = campId === 1 ? tboardReg.value : tboardSur.value
  const rows = []
  const tiers = ['T0', 'T1', 'T2', 'T3', 'T4']
  for (const t of tiers) {
    for (const i of (src[t] || [])) {
      rows.push({
        Tier: t,
        HeroId: i.heroId || i.HeroId,
        Name: i.name || i.Name,
        Camp: campLabel(i.campId || i.CampId),
        ScorePct: fmtPct(weightedScore(i)),
        UseRateAvgPct: fmtPct(i.useRateAvg || i.UseRateAvg),
        WinRateAvgPct: fmtPct(i.winRateAvg || i.WinRateAvg),
        BanRateAvgPct: fmtPct(i.banRateAvg || i.BanRateAvg),
        PingRateAvgPct: fmtPct(i.pingRateAvg || i.PingRateAvg)
      })
    }
  }
  const headers = ['Tier','HeroId','Name','Camp','ScorePct','UseRateAvgPct','WinRateAvgPct','BanRateAvgPct','PingRateAvgPct']
  const lines = [headers.join(',')]
  for (const r of rows) {
    lines.push(headers.map(h => String(r[h]).replace(/,/g, '')).join(','))
  }
  downloadCsv(`${campId === 1 ? '监管者' : '求生者'}T级榜.csv`, lines.join('\n'))
}

const trendLoading = ref(false)
const trendPoints = ref([])

const compareLoading = ref(false)
const compareSeries = ref([])
const compareError = ref('')
const tboardLoading = ref(false)
const tboardReg = ref({ T0: [], T1: [], T2: [], T3: [], T4: [] })
const tboardSur = ref({ T0: [], T1: [], T2: [], T3: [], T4: [] })
const campTrendLoading = ref(false)
const campTrendSeries = ref([])
const campTrendError = ref('')

const trendChartRef = ref(null)
const overviewChartRef = ref(null)
const compareChartRef = ref(null)
const campTrendChartRef = ref(null)
let trendChart, overviewChart, compareChart
let campTrendChart

function fmtPct(v) {
  const percent = Number(v || 0) * 100
  return Math.round((percent + Number.EPSILON) * 100) / 100
}

function weightedScore(i) {
  const u = i.useRateAvg ?? i.UseRateAvg ?? 0
  const w = i.winRateAvg ?? i.WinRateAvg ?? 0
  const b = i.banRateAvg ?? i.BanRateAvg ?? 0
  const p = i.pingRateAvg ?? i.PingRateAvg ?? 0
  return 0.1 * u + 0.3 * w + 0.5 * b + 0.1 * p
}

function makeTiers(items) {
  const sorted = [...items].sort((a, b) => (weightedScore(b) - weightedScore(a)))
  const n = sorted.length
  const i0 = Math.max(1, Math.round(n * 0.10))
  const i1 = Math.max(i0, Math.round(n * 0.35))
  const i2 = Math.max(i1, Math.round(n * 0.60))
  const i3 = Math.max(i2, Math.round(n * 0.80))
  return {
    T0: sorted.slice(0, i0),
    T1: sorted.slice(i0, i1),
    T2: sorted.slice(i1, i2),
    T3: sorted.slice(i2, i3),
    T4: sorted.slice(i3)
  }
}

function pickAvg(item, metric) {
  const m = (metric || 'use_rate').toLowerCase()
  if (m === 'win_rate') return item.winRateAvg ?? item.WinRateAvg
  if (m === 'ban_rate') return item.banRateAvg ?? item.BanRateAvg
  if (m === 'ping_rate') return item.pingRateAvg ?? item.PingRateAvg
  if (m === 'use_rate') return item.useRateAvg ?? item.UseRateAvg
  return item.useRateAvg ?? item.UseRateAvg
}

const sortedOverview = computed(() => {
  const arr = [...overviewItems.value]
  const metric = filters.value.metric
  if (overviewMode.value === 'avg') {
    return arr.sort((a, b) => ((pickAvg(b, metric) ?? 0) - (pickAvg(a, metric) ?? 0)))
  }
  return arr.sort((a, b) => ((b.metricLatest ?? b.MetricLatest ?? 0) - (a.metricLatest ?? a.MetricLatest ?? 0)))
})

async function loadMeta() {
  loadingMeta.value = true
  try {
    meta.value = await getMeta()
    heroes.value = await getHeroes(filters.value.campId)
    selectedHero.value = heroes.value[0]?.heroId || heroes.value[0]?.HeroId || null
  } finally { loadingMeta.value = false }
}

async function loadOverview() {
  overviewLoading.value = true
  try {
    overviewItems.value = await getOverview({ ...filters.value })
    await nextTick()
    renderOverviewChart()
  } finally { overviewLoading.value = false }
}

async function loadTrend() {
  const hid = selectedHero.value
  if (!hid) { trendPoints.value = []; renderTrendChart(); return }
  trendLoading.value = true
  try {
    trendPoints.value = await getTrend(hid, { season: filters.value.season, part: filters.value.part, position: filters.value.position })
    await nextTick()
    renderTrendChart()
  } finally { trendLoading.value = false }
}

async function loadCompare() {
  const ids = compareHeroIds.value
  compareLoading.value = true
  try {
    if (!ids || ids.length === 0) {
      compareSeries.value = []
      compareError.value = '请选择至少一个角色进行对比'
      await nextTick(); renderCompareChart(); return
    }
    compareError.value = ''
    compareSeries.value = await compareHeroes(ids, { metric: filters.value.metric, season: filters.value.season, part: filters.value.part, position: filters.value.position })
    await nextTick()
    renderCompareChart()
  } catch (e) {
    compareError.value = e?.payload?.message || e?.message || '对比加载失败'
  } finally { compareLoading.value = false }
}

async function loadTBoards() {
  tboardLoading.value = true
  try {
    const base = { season: filters.value.season, part: filters.value.part, position: filters.value.position }
    const reg = await getOverview({ ...base, campId: 1 })
    const sur = await getOverview({ ...base, campId: 2 })
    tboardReg.value = makeTiers(reg || [])
    tboardSur.value = makeTiers(sur || [])
  } finally { tboardLoading.value = false }
}

function renderTrendChart() {
  if (!trendChart) trendChart = echarts.init(trendChartRef.value)
  const dates = trendPoints.value.map(p => p.date || p.Date)
  const win = trendPoints.value.map(p => fmtPct(p.winRate || p.WinRate))
  const use = trendPoints.value.map(p => fmtPct(p.useRate || p.UseRate))
  const ban = trendPoints.value.map(p => fmtPct(p.banRate || p.BanRate))
  const ping = trendPoints.value.map(p => fmtPct(p.pingRate || p.PingRate))
  trendChart.setOption({
    tooltip: { trigger: 'axis', valueFormatter: (v) => `${v}%` },
    legend: { data: ['胜率', '使用率', 'Ban率', '平局率'] },
    xAxis: { type: 'category', data: dates },
    yAxis: { type: 'value', axisLabel: { formatter: '{value}%' } },
    series: [
      { name: '胜率', type: 'line', smooth: true, data: win },
      { name: '使用率', type: 'line', smooth: true, data: use },
      { name: 'Ban率', type: 'line', smooth: true, data: ban },
      { name: '平局率', type: 'line', smooth: true, data: ping },
    ]
  })
}

function renderOverviewChart() {
  if (!overviewChart) overviewChart = echarts.init(overviewChartRef.value)
  const metric = filters.value.metric
  const items = [...overviewItems.value]
  const sorted = overviewMode.value === 'avg'
    ? items.sort((a, b) => ((pickAvg(b, metric) ?? 0) - (pickAvg(a, metric) ?? 0)))
    : items.sort((a, b) => ((b.metricLatest ?? b.MetricLatest ?? 0) - (a.metricLatest ?? a.MetricLatest ?? 0)))
  const visible = overviewCollapsed.value ? sorted.slice(0, 20) : sorted
  const labels = visible.map(i => i.name || i.Name)
  const vals = overviewMode.value === 'avg'
    ? visible.map(i => fmtPct(pickAvg(i, metric)))
    : visible.map(i => fmtPct(i.metricLatest ?? i.MetricLatest ?? 0))
  overviewChart.setOption({
    tooltip: { trigger: 'axis', valueFormatter: (v) => `${v}%` },
    xAxis: { type: 'category', data: labels },
    yAxis: { type: 'value', axisLabel: { formatter: '{value}%' } },
    series: [{ name: overviewMode.value === 'avg' ? `${metric}均值` : `${metric}最新值`, type: 'bar', data: vals }]
  })
}

function renderCompareChart() {
  if (!compareChart) compareChart = echarts.init(compareChartRef.value)
  const allDates = Array.from(new Set(compareSeries.value.flatMap(s => s.points || s.Points).map(p => p.date || p.Date))).sort()
  const series = (compareSeries.value || []).map(s => ({
    name: s.name || s.Name,
    type: 'line', smooth: true,
    data: allDates.map(d => {
      const p = (s.points || s.Points).find(x => (x.date || x.Date) === d)
      return fmtPct(p?.value ?? p?.Value ?? 0)
    })
  }))
  compareChart.setOption({
    tooltip: { trigger: 'axis', valueFormatter: (v) => `${v}%` },
    legend: { type: 'scroll' },
    xAxis: { type: 'category', data: allDates },
    yAxis: { type: 'value', axisLabel: { formatter: '{value}%' } },
    series
  })
}

async function loadCampTrend() {
  campTrendLoading.value = true
  try {
    campTrendError.value = ''
    const res = await getCampTrend({ season: filters.value.season, part: filters.value.part, position: filters.value.position })
    campTrendSeries.value = Array.isArray(res) ? res : []
    if (!campTrendSeries.value.length || !((campTrendSeries.value[0]?.points || campTrendSeries.value[0]?.Points || []).length)) {
      campTrendError.value = '暂无数据，请调整筛选条件'
    }
    await nextTick()
    renderCampTrendChart()
  } finally { campTrendLoading.value = false }
}

function renderCampTrendChart() {
  if (!campTrendChartRef.value) return
  try { campTrendChart?.dispose() } catch {}
  campTrendChart = echarts.init(campTrendChartRef.value)
  const allDates = Array.from(new Set((campTrendSeries.value || []).flatMap(s => s.points || s.Points).map(p => p.date || p.Date))).sort()
  const series = (campTrendSeries.value || []).map(s => ({
    name: s.name || s.Name,
    type: 'line', smooth: true,
    data: allDates.map(d => {
      const p = (s.points || s.Points).find(x => (x.date || x.Date) === d)
      return fmtPct(p?.value ?? p?.Value ?? 0)
    })
  }))
  campTrendChart.setOption({
    tooltip: { trigger: 'axis', valueFormatter: (v) => `${v}%` },
    legend: { type: 'scroll' },
    xAxis: { type: 'category', data: allDates },
    yAxis: { type: 'value', axisLabel: { formatter: '{value}%' } },
    series
  })
}

onMounted(async () => {
  await loadMeta()
  await loadOverview()
  await loadTrend()
  await loadTBoards()
  await loadCampTrend()
  try { window.addEventListener('resize', () => { trendChart?.resize(); overviewChart?.resize(); compareChart?.resize(); campTrendChart?.resize() }) } catch {}
  try {
    const ids = (heroes.value || []).slice(0, 2).map(h => h.heroId || h.HeroId).filter(Boolean)
    if (ids.length) { compareHeroIds.value = ids; await loadCompare() }
  } catch {}
})

watch(filters, async () => { await loadOverview(); await loadTrend(); await loadCompare(); await loadTBoards(); await loadCampTrend() }, { deep: true })
watch(compareHeroIds, async () => { await loadCompare() })
watch(() => filters.value.campId, async () => {
  heroes.value = await getHeroes(filters.value.campId)
  selectedHero.value = heroes.value[0]?.heroId || heroes.value[0]?.HeroId || null
  if (!compareHeroIds.value?.length) {
    const ids = (heroes.value || []).slice(0, 2).map(h => h.heroId || h.HeroId).filter(Boolean)
    compareHeroIds.value = ids
  }
})
watch(selectedHero, async () => { await loadTrend() })
watch(overviewMode, async () => { await nextTick(); renderOverviewChart() })
watch(overviewCollapsed, async () => { await nextTick(); renderOverviewChart() })
onBeforeUnmount(() => { try { trendChart?.dispose(); overviewChart?.dispose(); compareChart?.dispose() } catch {} })
onBeforeUnmount(() => { try { campTrendChart?.dispose() } catch {} })
</script>

<template>
  <v-container class="py-6">
    <PageHero title="数据分析" subtitle="选择条件→看排行→看趋势→做对比" icon="insights" />

    <v-card class="mb-4" variant="outlined">
      <v-card-title class="d-flex align-center">
        <v-icon class="mr-2" icon="tune" /> ① 选择条件
        <v-spacer />
        <v-btn size="small" variant="text" prepend-icon="help" @click="filtersInfoOpen = true">说明</v-btn>
      </v-card-title>
      <v-card-text>
  <v-row>
          <v-col cols="12" md="2">
            <v-select :items="meta.seasons" v-model="filters.season" label="赛季" clearable />
          </v-col>
          <v-col cols="12" md="2">
            <v-select :items="partOptions" item-title="title" item-value="value" v-model="filters.part" label="段位" clearable />
          </v-col>
          <v-col cols="12" md="3">
            <v-select :items="meta.positions" v-model="filters.position" label="位置标签" clearable />
          </v-col>
          <v-col cols="12" md="2">
            <v-select :items="campOptions" item-title="title" item-value="value" v-model="filters.campId" label="阵营" clearable />
          </v-col>
          <v-col cols="12" md="3">
            <v-select :items="[
              { title: '使用率', value: 'use_rate' },
              { title: '胜率', value: 'win_rate' },
              { title: 'Ban率', value: 'ban_rate' },
              { title: '平局率', value: 'ping_rate' },
            ]" item-title="title" item-value="value" v-model="filters.metric" label="指标" />
          </v-col>
          <v-col cols="12" md="2">
            <v-select :items="[
              { title: '口径：等权', value: 'equal_date' },
              { title: '口径：官方', value: 'official' },
            ]" item-title="title" item-value="value" v-model="filters.avgMode" label="口径" />
          </v-col>
        </v-row>
        <div class="text-caption text-medium-emphasis mt-2">提示：更改上方条件会同步刷新排行、趋势与对比。</div>
      </v-card-text>
    </v-card>

    <v-card class="mb-4" variant="outlined">
      <v-card-title class="d-flex align-center">
        <v-icon class="mr-2" icon="leaderboard" /> ② 概览排行
        <v-spacer />
        <v-btn-toggle v-model="overviewMode" class="mr-2" density="compact">
          <v-btn value="latest" variant="text">最新值</v-btn>
          <v-btn value="avg" variant="text">均值</v-btn>
        </v-btn-toggle>
        <v-btn size="small" variant="text" prepend-icon="help" @click="overviewInfoOpen = true">说明</v-btn>
      </v-card-title>
          <v-card-text>
            <div ref="overviewChartRef" style="width:100%;height:320px"></div>
            <div class="d-flex align-center mt-2">
              <v-chip label class="mr-2" color="default" variant="tonal">共 {{ overviewItems.length }} 角色</v-chip>
              <v-chip label v-if="overviewCollapsed" color="default" variant="tonal">当前显示 20 条</v-chip>
              <v-spacer />
              <v-btn size="small" variant="text" prepend-icon="unfold_more" v-if="overviewCollapsed" @click="overviewCollapsed = false">展开全部</v-btn>
              <v-btn size="small" variant="text" prepend-icon="unfold_less" v-else @click="overviewCollapsed = true">收起为 20 条</v-btn>
            </div>
            <v-table class="mt-4">
              <thead>
                <tr>
                  <th>角色</th>
                  <th>阵营</th>
                  <th>{{ overviewMode === 'avg' ? '当前指标均值' : '当前指标最新值' }}</th>
                  <th>使用率均值</th>
                  <th>胜率均值</th>
                  <th>Ban率均值</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="i in (sortedOverview.slice(0, overviewCollapsed ? 20 : overviewItems.length))" :key="i.heroId || i.HeroId">
                  <td>{{ i.name || i.Name }}</td>
                  <td>{{ campLabel(i.campId || i.CampId) }}</td>
                  <td>
                    <template v-if="overviewMode === 'avg'">{{ fmtPct(pickAvg(i, filters.metric)) }}%</template>
                    <template v-else>{{ fmtPct(pickLatest(i)) }}%</template>
                  </td>
                  <td>{{ fmtPct(i.useRateAvg || i.UseRateAvg) }}%</td>
                  <td>{{ fmtPct(i.winRateAvg || i.WinRateAvg) }}%</td>
                  <td>{{ fmtPct(i.banRateAvg || i.BanRateAvg) }}%</td>
                </tr>
              </tbody>
            </v-table>
          </v-card-text>
        </v-card>

    <v-row>
      <v-col cols="12" md="6">
        <v-card variant="outlined">
          <v-card-title class="d-flex align-center">
            <v-icon class="mr-2" icon="timeline" /> ③ 角色趋势（单角色）
            <v-spacer />
            <v-btn size="small" variant="text" prepend-icon="help" @click="trendInfoOpen = true">说明</v-btn>
          </v-card-title>
          <v-card-text>
            <v-select :items="heroes.map(h => ({ title: `${h.name || h.Name}（${campLabel(h.campId || h.CampId)}）`, value: h.heroId || h.HeroId }))" v-model="selectedHero" label="选择角色" />
            <div ref="trendChartRef" style="width:100%;height:320px"></div>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="12" md="6">
        <v-card variant="outlined">
          <v-card-title class="d-flex align-center">
            <v-icon class="mr-2" icon="show_chart" /> ④ 多角色对比
            <v-spacer />
            <v-btn size="small" variant="text" prepend-icon="help" @click="compareInfoOpen = true">说明</v-btn>
          </v-card-title>
          <v-card-text>
            <v-select multiple chips closable-chips item-title="title" item-value="value"
                      :items="heroes.map(h => ({ title: h.name || h.Name, value: h.heroId || h.HeroId }))"
                      v-model="compareHeroIds" label="选择对比角色" hint="至少选择 1 个角色" persistent-hint />
            <v-alert v-if="compareError" type="info" :text="compareError" class="my-2" />
            <div ref="compareChartRef" style="width:100%;height:360px"></div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-card class="mb-4" variant="outlined">
      <v-card-title class="d-flex align-center">
        <v-icon class="mr-2" icon="balance" /> ⑥ 阵营综合胜率对比
      </v-card-title>
      <v-card-text>
        <v-progress-linear v-if="campTrendLoading" indeterminate color="primary" />
        <v-alert v-if="campTrendError" type="info" :text="campTrendError" class="mb-2" />
        <div v-show="!campTrendError" ref="campTrendChartRef" style="width:100%;height:320px"></div>
      </v-card-text>
    </v-card>

    <v-card class="mb-4" variant="outlined">
      <v-card-title class="d-flex align-center">
        <v-icon class="mr-2" icon="military_tech" /> ⑤ T级榜（监管者 / 求生者）
        <v-spacer />
        <v-btn size="small" class="mr-2" variant="text" prepend-icon="file_download" @click="exportTBoard(1)">导出监管者 CSV</v-btn>
        <v-btn size="small" class="mr-2" variant="text" prepend-icon="file_download" @click="exportTBoard(2)">导出求生者 CSV</v-btn>
        <v-btn size="small" variant="text" prepend-icon="help" @click="tboardInfoOpen = true">说明</v-btn>
      </v-card-title>
      <v-card-text>
        <v-progress-linear v-if="tboardLoading" indeterminate color="primary" />
        <template v-else>
          <div class="text-subtitle-2 mb-2">监管者</div>
          <div class="mb-2">
            <v-chip label class="mr-2" color="default" variant="tonal">T0 {{ tboardReg.T0.length }}</v-chip>
            <v-chip label class="mr-2" color="default" variant="tonal">T1 {{ tboardReg.T1.length }}</v-chip>
            <v-chip label class="mr-2" color="default" variant="tonal">T2 {{ tboardReg.T2.length }}</v-chip>
            <v-chip label class="mr-2" color="default" variant="tonal">T3 {{ tboardReg.T3.length }}</v-chip>
            <v-chip label color="default" variant="tonal">T4 {{ tboardReg.T4.length }}</v-chip>
          </div>
          <div class="d-flex align-center">
            <div class="text-subtitle-2">T0</div>
            <v-spacer /><v-btn size="small" variant="text" @click="regTierOpen.T0 = !regTierOpen.T0">{{ regTierOpen.T0 ? '收起' : '展开' }}</v-btn>
          </div>
          <v-expand-transition>
          <div v-show="regTierOpen.T0">
          <v-table density="compact" class="mb-4">
            <thead>
              <tr>
                <th>角色</th>
                <th>阵营</th>
                <th>综合得分</th>
                <th>使用率均值</th>
                <th>胜率均值</th>
                <th>Ban率均值</th>
                <th>平局率均值</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="i in tboardReg.T0" :key="i.heroId || i.HeroId">
                <td>{{ i.name || i.Name }}</td>
                <td>{{ campLabel(i.campId || i.CampId) }}</td>
                <td>{{ fmtPct(weightedScore(i)) }}%</td>
                <td>{{ fmtPct(i.useRateAvg || i.UseRateAvg) }}%</td>
                <td>{{ fmtPct(i.winRateAvg || i.WinRateAvg) }}%</td>
                <td>{{ fmtPct(i.banRateAvg || i.BanRateAvg) }}%</td>
                <td>{{ fmtPct(i.pingRateAvg || i.PingRateAvg) }}%</td>
              </tr>
            </tbody>
          </v-table>
          </div>
          </v-expand-transition>
          <div class="d-flex align-center">
            <div class="text-subtitle-2">T1</div>
            <v-spacer /><v-btn size="small" variant="text" @click="regTierOpen.T1 = !regTierOpen.T1">{{ regTierOpen.T1 ? '收起' : '展开' }}</v-btn>
          </div>
          <v-expand-transition>
          <div v-show="regTierOpen.T1">
          <v-table density="compact" class="mb-4">
            <thead>
              <tr>
                <th>角色</th>
                <th>阵营</th>
                <th>综合得分</th>
                <th>使用率均值</th>
                <th>胜率均值</th>
                <th>Ban率均值</th>
                <th>平局率均值</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="i in tboardReg.T1" :key="i.heroId || i.HeroId">
                <td>{{ i.name || i.Name }}</td>
                <td>{{ campLabel(i.campId || i.CampId) }}</td>
                <td>{{ fmtPct(weightedScore(i)) }}%</td>
                <td>{{ fmtPct(i.useRateAvg || i.UseRateAvg) }}%</td>
                <td>{{ fmtPct(i.winRateAvg || i.WinRateAvg) }}%</td>
                <td>{{ fmtPct(i.banRateAvg || i.BanRateAvg) }}%</td>
                <td>{{ fmtPct(i.pingRateAvg || i.PingRateAvg) }}%</td>
              </tr>
            </tbody>
          </v-table>
          </div>
          </v-expand-transition>
          <div class="d-flex align-center">
            <div class="text-subtitle-2">T2</div>
            <v-spacer /><v-btn size="small" variant="text" @click="regTierOpen.T2 = !regTierOpen.T2">{{ regTierOpen.T2 ? '收起' : '展开' }}</v-btn>
          </div>
          <v-expand-transition>
          <div v-show="regTierOpen.T2">
          <v-table density="compact" class="mb-4">
            <thead>
              <tr>
                <th>角色</th>
                <th>阵营</th>
                <th>综合得分</th>
                <th>使用率均值</th>
                <th>胜率均值</th>
                <th>Ban率均值</th>
                <th>平局率均值</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="i in tboardReg.T2" :key="i.heroId || i.HeroId">
                <td>{{ i.name || i.Name }}</td>
                <td>{{ campLabel(i.campId || i.CampId) }}</td>
                <td>{{ fmtPct(weightedScore(i)) }}%</td>
                <td>{{ fmtPct(i.useRateAvg || i.UseRateAvg) }}%</td>
                <td>{{ fmtPct(i.winRateAvg || i.WinRateAvg) }}%</td>
                <td>{{ fmtPct(i.banRateAvg || i.BanRateAvg) }}%</td>
                <td>{{ fmtPct(i.pingRateAvg || i.PingRateAvg) }}%</td>
              </tr>
            </tbody>
          </v-table>
          </div>
          </v-expand-transition>
          <div class="d-flex align-center">
            <div class="text-subtitle-2">T3</div>
            <v-spacer /><v-btn size="small" variant="text" @click="regTierOpen.T3 = !regTierOpen.T3">{{ regTierOpen.T3 ? '收起' : '展开' }}</v-btn>
          </div>
          <v-expand-transition>
          <div v-show="regTierOpen.T3">
          <v-table density="compact" class="mb-4">
            <thead>
              <tr>
                <th>角色</th>
                <th>阵营</th>
                <th>综合得分</th>
                <th>使用率均值</th>
                <th>胜率均值</th>
                <th>Ban率均值</th>
                <th>平局率均值</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="i in tboardReg.T3" :key="i.heroId || i.HeroId">
                <td>{{ i.name || i.Name }}</td>
                <td>{{ campLabel(i.campId || i.CampId) }}</td>
                <td>{{ fmtPct(weightedScore(i)) }}%</td>
                <td>{{ fmtPct(i.useRateAvg || i.UseRateAvg) }}%</td>
                <td>{{ fmtPct(i.winRateAvg || i.WinRateAvg) }}%</td>
                <td>{{ fmtPct(i.banRateAvg || i.BanRateAvg) }}%</td>
                <td>{{ fmtPct(i.pingRateAvg || i.PingRateAvg) }}%</td>
              </tr>
            </tbody>
          </v-table>
          </div>
          </v-expand-transition>
          <div class="d-flex align-center">
            <div class="text-subtitle-2">T4</div>
            <v-spacer /><v-btn size="small" variant="text" @click="regTierOpen.T4 = !regTierOpen.T4">{{ regTierOpen.T4 ? '收起' : '展开' }}</v-btn>
          </div>
          <v-expand-transition>
          <div v-show="regTierOpen.T4">
          <v-table density="compact">
            <thead>
              <tr>
                <th>角色</th>
                <th>阵营</th>
                <th>综合得分</th>
                <th>使用率均值</th>
                <th>胜率均值</th>
                <th>Ban率均值</th>
                <th>平局率均值</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="i in tboardReg.T4" :key="i.heroId || i.HeroId">
                <td>{{ i.name || i.Name }}</td>
                <td>{{ campLabel(i.campId || i.CampId) }}</td>
                <td>{{ fmtPct(weightedScore(i)) }}%</td>
                <td>{{ fmtPct(i.useRateAvg || i.UseRateAvg) }}%</td>
                <td>{{ fmtPct(i.winRateAvg || i.WinRateAvg) }}%</td>
                <td>{{ fmtPct(i.banRateAvg || i.BanRateAvg) }}%</td>
                <td>{{ fmtPct(i.pingRateAvg || i.PingRateAvg) }}%</td>
              </tr>
            </tbody>
          </v-table>
          </div>
          </v-expand-transition>

          <div class="text-subtitle-2 mb-2 mt-6">求生者</div>
          <div class="mb-2">
            <v-chip label class="mr-2" color="default" variant="tonal">T0 {{ tboardSur.T0.length }}</v-chip>
            <v-chip label class="mr-2" color="default" variant="tonal">T1 {{ tboardSur.T1.length }}</v-chip>
            <v-chip label class="mr-2" color="default" variant="tonal">T2 {{ tboardSur.T2.length }}</v-chip>
            <v-chip label class="mr-2" color="default" variant="tonal">T3 {{ tboardSur.T3.length }}</v-chip>
            <v-chip label color="default" variant="tonal">T4 {{ tboardSur.T4.length }}</v-chip>
          </div>
          <div class="d-flex align-center">
            <div class="text-subtitle-2">T0</div>
            <v-spacer /><v-btn size="small" variant="text" @click="surTierOpen.T0 = !surTierOpen.T0">{{ surTierOpen.T0 ? '收起' : '展开' }}</v-btn>
          </div>
          <v-expand-transition>
          <div v-show="surTierOpen.T0">
          <v-table density="compact" class="mb-4">
            <thead>
              <tr>
                <th>角色</th>
                <th>阵营</th>
                <th>综合得分</th>
                <th>使用率均值</th>
                <th>胜率均值</th>
                <th>Ban率均值</th>
                <th>平局率均值</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="i in tboardSur.T0" :key="i.heroId || i.HeroId">
                <td>{{ i.name || i.Name }}</td>
                <td>{{ campLabel(i.campId || i.CampId) }}</td>
                <td>{{ fmtPct(weightedScore(i)) }}%</td>
                <td>{{ fmtPct(i.useRateAvg || i.UseRateAvg) }}%</td>
                <td>{{ fmtPct(i.winRateAvg || i.WinRateAvg) }}%</td>
                <td>{{ fmtPct(i.banRateAvg || i.BanRateAvg) }}%</td>
                <td>{{ fmtPct(i.pingRateAvg || i.PingRateAvg) }}%</td>
              </tr>
            </tbody>
          </v-table>
          </div>
          </v-expand-transition>
          <div class="d-flex align-center">
            <div class="text-subtitle-2">T1</div>
            <v-spacer /><v-btn size="small" variant="text" @click="surTierOpen.T1 = !surTierOpen.T1">{{ surTierOpen.T1 ? '收起' : '展开' }}</v-btn>
          </div>
          <v-expand-transition>
          <div v-show="surTierOpen.T1">
          <v-table density="compact" class="mb-4">
            <thead>
              <tr>
                <th>角色</th>
                <th>阵营</th>
                <th>综合得分</th>
                <th>使用率均值</th>
                <th>胜率均值</th>
                <th>Ban率均值</th>
                <th>平局率均值</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="i in tboardSur.T1" :key="i.heroId || i.HeroId">
                <td>{{ i.name || i.Name }}</td>
                <td>{{ campLabel(i.campId || i.CampId) }}</td>
                <td>{{ fmtPct(weightedScore(i)) }}%</td>
                <td>{{ fmtPct(i.useRateAvg || i.UseRateAvg) }}%</td>
                <td>{{ fmtPct(i.winRateAvg || i.WinRateAvg) }}%</td>
                <td>{{ fmtPct(i.banRateAvg || i.BanRateAvg) }}%</td>
                <td>{{ fmtPct(i.pingRateAvg || i.PingRateAvg) }}%</td>
              </tr>
            </tbody>
          </v-table>
          </div>
          </v-expand-transition>
          <div class="d-flex align-center">
            <div class="text-subtitle-2">T2</div>
            <v-spacer /><v-btn size="small" variant="text" @click="surTierOpen.T2 = !surTierOpen.T2">{{ surTierOpen.T2 ? '收起' : '展开' }}</v-btn>
          </div>
          <v-expand-transition>
          <div v-show="surTierOpen.T2">
          <v-table density="compact" class="mb-4">
            <thead>
              <tr>
                <th>角色</th>
                <th>阵营</th>
                <th>综合得分</th>
                <th>使用率均值</th>
                <th>胜率均值</th>
                <th>Ban率均值</th>
                <th>平局率均值</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="i in tboardSur.T2" :key="i.heroId || i.HeroId">
                <td>{{ i.name || i.Name }}</td>
                <td>{{ campLabel(i.campId || i.CampId) }}</td>
                <td>{{ fmtPct(weightedScore(i)) }}%</td>
                <td>{{ fmtPct(i.useRateAvg || i.UseRateAvg) }}%</td>
                <td>{{ fmtPct(i.winRateAvg || i.WinRateAvg) }}%</td>
                <td>{{ fmtPct(i.banRateAvg || i.BanRateAvg) }}%</td>
                <td>{{ fmtPct(i.pingRateAvg || i.PingRateAvg) }}%</td>
              </tr>
            </tbody>
          </v-table>
          </div>
          </v-expand-transition>
          <div class="d-flex align-center">
            <div class="text-subtitle-2">T3</div>
            <v-spacer /><v-btn size="small" variant="text" @click="surTierOpen.T3 = !surTierOpen.T3">{{ surTierOpen.T3 ? '收起' : '展开' }}</v-btn>
          </div>
          <v-expand-transition>
          <div v-show="surTierOpen.T3">
          <v-table density="compact" class="mb-4">
            <thead>
              <tr>
                <th>角色</th>
                <th>阵营</th>
                <th>综合得分</th>
                <th>使用率均值</th>
                <th>胜率均值</th>
                <th>Ban率均值</th>
                <th>平局率均值</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="i in tboardSur.T3" :key="i.heroId || i.HeroId">
                <td>{{ i.name || i.Name }}</td>
                <td>{{ campLabel(i.campId || i.CampId) }}</td>
                <td>{{ fmtPct(weightedScore(i)) }}%</td>
                <td>{{ fmtPct(i.useRateAvg || i.UseRateAvg) }}%</td>
                <td>{{ fmtPct(i.winRateAvg || i.WinRateAvg) }}%</td>
                <td>{{ fmtPct(i.banRateAvg || i.BanRateAvg) }}%</td>
                <td>{{ fmtPct(i.pingRateAvg || i.PingRateAvg) }}%</td>
              </tr>
            </tbody>
          </v-table>
          </div>
          </v-expand-transition>
          <div class="d-flex align-center">
            <div class="text-subtitle-2">T4</div>
            <v-spacer /><v-btn size="small" variant="text" @click="surTierOpen.T4 = !surTierOpen.T4">{{ surTierOpen.T4 ? '收起' : '展开' }}</v-btn>
          </div>
          <v-expand-transition>
          <div v-show="surTierOpen.T4">
          <v-table density="compact">
            <thead>
              <tr>
                <th>角色</th>
                <th>阵营</th>
                <th>综合得分</th>
                <th>使用率均值</th>
                <th>胜率均值</th>
                <th>Ban率均值</th>
                <th>平局率均值</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="i in tboardSur.T4" :key="i.heroId || i.HeroId">
                <td>{{ i.name || i.Name }}</td>
                <td>{{ campLabel(i.campId || i.CampId) }}</td>
                <td>{{ fmtPct(weightedScore(i)) }}%</td>
                <td>{{ fmtPct(i.useRateAvg || i.UseRateAvg) }}%</td>
                <td>{{ fmtPct(i.winRateAvg || i.WinRateAvg) }}%</td>
                <td>{{ fmtPct(i.banRateAvg || i.BanRateAvg) }}%</td>
                <td>{{ fmtPct(i.pingRateAvg || i.PingRateAvg) }}%</td>
              </tr>
            </tbody>
          </v-table>
          </div>
          </v-expand-transition>
        </template>
      </v-card-text>
    </v-card>
  </v-container>

  <v-dialog v-model="filtersInfoOpen" max-width="520">
    <v-card>
      <v-card-title>选择条件说明</v-card-title>
      <v-card-text>
        <div>赛季、段位、位置与阵营用于过滤数据源。指标决定展示的统计类别（使用率/胜率/Ban率/平局率）。修改后会同步更新排行、趋势与对比。</div>
      </v-card-text>
      <v-card-actions>
        <v-spacer /><v-btn variant="text" @click="filtersInfoOpen = false">知道了</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-dialog v-model="overviewInfoOpen" max-width="520">
    <v-card>
      <v-card-title>概览排行说明</v-card-title>
      <v-card-text>
        <div>按所选指标的“最新值/均值”从高到低排序。柱状图与表格一致，默认显示前20条，可展开全部。</div>
      </v-card-text>
      <v-card-actions>
        <v-spacer /><v-btn variant="text" @click="overviewInfoOpen = false">知道了</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-dialog v-model="trendInfoOpen" max-width="520">
    <v-card>
      <v-card-title>角色趋势说明</v-card-title>
      <v-card-text>
        <div>展示当前选择角色在时间维度上的使用率、胜率、Ban率、平局率变化。可用于观察版本与赛事影响。</div>
      </v-card-text>
      <v-card-actions>
        <v-spacer /><v-btn variant="text" @click="trendInfoOpen = false">知道了</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-dialog v-model="compareInfoOpen" max-width="520">
    <v-card>
      <v-card-title>多角色对比说明</v-card-title>
      <v-card-text>
        <div>选择多个角色，按所选指标对比其时间序列。用于评估相对强度与选择倾向。</div>
      </v-card-text>
      <v-card-actions>
        <v-spacer /><v-btn variant="text" @click="compareInfoOpen = false">知道了</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <v-dialog v-model="tboardInfoOpen" max-width="520">
    <v-card>
      <v-card-title>T级榜说明</v-card-title>
      <v-card-text>
        <div>根据综合得分将角色分为 T0/T1/T2/T3/T4 五档。综合得分权重：使用率10%、胜率30%、Ban率50%、平局率10%。分档比例：T0 前10%，T1 前35%，T2 前60%，T3 前80%，T4 后20%。可通过上方赛季、段位、位置与阵营筛选，段位支持“综合”。</div>
      </v-card-text>
      <v-card-actions>
        <v-spacer /><v-btn variant="text" @click="tboardInfoOpen = false">知道了</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

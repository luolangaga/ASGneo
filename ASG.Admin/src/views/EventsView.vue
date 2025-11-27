<script setup>
import { ref, reactive, onMounted, watch } from 'vue'
import { searchEvents, getEvents, createEvent, updateEvent, deleteEvent, setChampion, uploadEventLogo, getEventRegistrations, getEvent, exportRegistrationsCsv } from '../services/events'
import { getTeams } from '../services/teams'
import { auditQq } from '../services/audit'
import { notifySuccess, notifyError } from '../notify'

const loading = ref(false)
const errorMsg = ref('')
const keyword = ref('')
const items = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(12)

const createDialog = ref(false)
const editDialog = ref(false)
const championDialog = ref(false)
const logoDialog = ref(false)
const registrationsDialog = ref(false)

const editing = ref(null)
const registrations = ref([])
const championTeamId = ref(null)
const showFull = ref(false)
const exporting = ref(false)

const statusOptions = [
  { text: '草稿', value: 0 },
  { text: '报名中', value: 1 },
  { text: '报名已结束', value: 2 },
  { text: '比赛进行中', value: 3 },
  { text: '比赛已结束', value: 4 },
  { text: '已取消', value: 5 },
]

const form = reactive({
  name: '',
  description: '',
  registrationStartTime: '',
  registrationEndTime: '',
  competitionStartTime: '',
  competitionEndTime: '',
  maxTeams: null,
  status: 0,
})

function toLocalInput(dt) {
  if (!dt) return ''
  const d = new Date(dt)
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}
function fromLocalInput(str) {
  if (!str) return null
  const d = new Date(str)
  return d.toISOString()
}

function fmtDate(v) {
  if (!v) return ''
  const d = new Date(v)
  const pad = n => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth()+1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function statusText(v) {
  const val = typeof v === 'number' ? v : Number(v)
  return statusOptions.find(s => s.value === val)?.text ?? val
}

function buildEventDto(isUpdate = false) {
  const name = (form.name || '').trim()
  const rs = fromLocalInput(form.registrationStartTime)
  const re = fromLocalInput(form.registrationEndTime)
  const cs = fromLocalInput(form.competitionStartTime)
  const ce = form.competitionEndTime ? fromLocalInput(form.competitionEndTime) : null
  const mt = form.maxTeams ? Number(form.maxTeams) : null
  if (!name) throw new Error('请输入赛事名称')
  if (!rs || !re || !cs) throw new Error('请填写报名开始/结束和比赛开始时间')
  if (new Date(rs) >= new Date(re)) throw new Error('报名结束时间必须晚于报名开始时间')
  if (new Date(re) >= new Date(cs)) throw new Error('报名结束时间必须早于比赛开始时间')
  if (mt !== null && (isNaN(mt) || mt < 1 || mt > 1000)) throw new Error('最大队伍数量须在1-1000之间')
  const dto = {
    Name: name,
    Description: form.description || null,
    RegistrationStartTime: rs,
    RegistrationEndTime: re,
    CompetitionStartTime: cs,
    CompetitionEndTime: ce,
    MaxTeams: mt,
  }
  if (isUpdate) dto.Status = Number(form.status ?? 0)
  return dto
}

onMounted(() => { fetchData() })
watch([page, pageSize], () => fetchData())

async function fetchData() {
  loading.value = true
  errorMsg.value = ''
  try {
    const q = String(keyword.value || '').trim()
    if (q) {
      const res = await searchEvents(q, page.value, pageSize.value)
      items.value = res?.Items ?? res?.items ?? []
      total.value = res?.TotalCount ?? res?.totalCount ?? items.value.length
    } else {
      const res = await getEvents()
      items.value = Array.isArray(res) ? res : (res?.Items ?? res?.items ?? [])
      total.value = items.value.length
    }
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载失败'
  } finally {
    loading.value = false
  }
}

function openCreate() {
  Object.assign(form, { name: '', description: '', registrationStartTime: '', registrationEndTime: '', competitionStartTime: '', competitionEndTime: '', maxTeams: null, status: 0 })
  createDialog.value = true
}

function openEdit(item) {
  editing.value = item
  Object.assign(form, {
    name: item.Name || item.name || '',
    description: item.Description || item.description || '',
    registrationStartTime: toLocalInput(item.RegistrationStartTime || item.registrationStartTime),
    registrationEndTime: toLocalInput(item.RegistrationEndTime || item.registrationEndTime),
    competitionStartTime: toLocalInput(item.CompetitionStartTime || item.competitionStartTime),
    competitionEndTime: toLocalInput(item.CompetitionEndTime || item.competitionEndTime),
    maxTeams: item.MaxTeams ?? item.maxTeams ?? null,
    status: item.Status ?? item.status ?? 0,
  })
  editDialog.value = true
}

async function submitCreate() {
  try {
    const dto = buildEventDto(false)
    await createEvent(dto)
    notifySuccess('赛事创建成功')
    createDialog.value = false
    await fetchData()
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '创建失败')
  }
}

async function submitEdit() {
  try {
    const id = editing.value?.Id || editing.value?.id
    if (!id) throw new Error('缺少赛事ID')
    const dto = buildEventDto(true)
    await updateEvent(id, dto)
    notifySuccess('赛事更新成功')
    editDialog.value = false
    await fetchData()
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '更新失败')
  }
}

async function onDelete(item) {
  if (!confirm(`删除赛事 ${item.Name || item.name}?`)) return
  try {
    const id = item.Id || item.id
    await deleteEvent(id)
    notifySuccess('删除成功')
    await fetchData()
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '删除失败')
  }
}

async function openChampion(item) {
  championTeamId.value = item.ChampionTeamId ?? item.championTeamId ?? null
  registrations.value = []
  try {
    const id = item.Id || item.id
    const regs = await getEventRegistrations(id)
    registrations.value = (regs || []).map(r => ({ text: r.TeamName || r.teamName, value: r.TeamId || r.teamId }))
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '加载报名队伍失败')
  }
  editing.value = item
  championDialog.value = true
}

async function openRegistrations(item) {
  editing.value = item
  registrations.value = []
  showFull.value = false
  try {
    const id = item.Id || item.id
    const regs = await getEventRegistrations(id)
    registrations.value = (regs || []).map(r => ({
      teamName: r.TeamName || r.teamName,
      teamId: r.TeamId || r.teamId,
      qqMasked: r.QqNumberMasked || r.qqNumberMasked || '',
      qqFull: r.QqNumberFull || r.qqNumberFull || '',
      status: r.Status || r.status,
    }))
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '加载报名列表失败')
  }
  registrationsDialog.value = true
}

function maskOrFull(row) {
  if (showFull.value && row.qqFull) return row.qqFull
  return row.qqMasked || row.qqFull || ''
}

async function confirmShowFull() {
  if (!editing.value) return
  if (!confirm('显示完整QQ号需要管理员或赛事创建者权限，确认继续？')) return
  showFull.value = true
  try {
    const id = editing.value?.Id || editing.value?.id
    // 记录查看审计（一次性）
    await Promise.all((registrations.value || []).map(r => auditQq(id, r.teamId, 'view')))
  } catch {}
}

async function copyOne(row) {
  const text = maskOrFull(row)
  if (!text) return
  try {
    await navigator.clipboard.writeText(text)
    notifySuccess('已复制QQ号')
    if (showFull.value && row.qqFull && editing.value) {
      const id = editing.value?.Id || editing.value?.id
      await auditQq(id, row.teamId, 'copy')
    }
  } catch (err) {
    try {
      const input = document.createElement('textarea')
      input.value = text
      document.body.appendChild(input)
      input.select()
      document.execCommand('copy')
      document.body.removeChild(input)
      notifySuccess('已复制QQ号')
    } catch {
      notifyError('复制失败，请手动选择复制')
    }
  }
}

async function copyAll() {
  const list = (registrations.value || []).map(r => maskOrFull(r)).filter(Boolean)
  if (!list.length) return
  try {
    await navigator.clipboard.writeText(list.join('\n'))
    notifySuccess('已批量复制QQ号')
    if (showFull.value && editing.value) {
      const id = editing.value?.Id || editing.value?.id
      await Promise.all((registrations.value || []).map(r => auditQq(id, r.teamId, 'copy')))
    }
  } catch {
    try {
      const input = document.createElement('textarea')
      input.value = list.join('\n')
      document.body.appendChild(input)
      input.select()
      document.execCommand('copy')
      document.body.removeChild(input)
      notifySuccess('已批量复制QQ号')
    } catch {
      notifyError('复制失败，请手动选择复制')
    }
  }
}

async function exportCsv() {
  if (!editing.value) return
  exporting.value = true
  try {
    const id = editing.value?.Id || editing.value?.id
    const blob = await exportRegistrationsCsv(id)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    const safeName = String(editing.value?.Name || editing.value?.name || 'event').replace(/[^a-zA-Z0-9_\-]/g, '_')
    a.href = url
    a.download = `${safeName}-registrations.csv`
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
    notifySuccess('导出完成')
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '导出失败')
  } finally {
    exporting.value = false
  }
}

async function submitChampion() {
  try {
    const id = editing.value?.Id || editing.value?.id
    await setChampion(id, championTeamId.value || null)
    notifySuccess('冠军设置成功')
    championDialog.value = false
    await fetchData()
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '设置失败')
  }
}

function openLogo(item) {
  editing.value = item
  logoDialog.value = true
}

async function submitLogo(file) {
  try {
    const id = editing.value?.Id || editing.value?.id
    if (!file) throw new Error('请先选择Logo文件')
    await uploadEventLogo(id, file)
    notifySuccess('Logo上传成功')
    logoDialog.value = false
    await fetchData()
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '上传失败')
  }
}
</script>

<template>
  <v-container class="py-6" style="max-width: 1200px">
    <div class="d-flex align-center mb-4">
      <div class="text-h5">赛事管理</div>
      <v-spacer></v-spacer>
      <v-text-field v-model="keyword" density="compact" placeholder="搜索名称/描述" append-inner-icon="mdi-magnify" class="mr-2" style="max-width: 280px" @keyup.enter="fetchData" />
      <v-btn color="primary" prepend-icon="mdi-magnify" @click="fetchData">搜索</v-btn>
      <v-divider vertical class="mx-4" />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">创建赛事</v-btn>
    </div>

    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />

    <v-data-table :items="items" :items-length="total" :page.sync="page" :items-per-page="pageSize" :loading="loading" class="elevation-1">
      <template #headers>
        <tr>
          <th class="text-left">名称</th>
          <th class="text-left">报名时间</th>
          <th class="text-left">比赛时间</th>
          <th class="text-left">状态</th>
          <th class="text-left">冠军</th>
          <th class="text-left">操作</th>
        </tr>
      </template>
      <template #item="{ item }">
        <tr>
          <td>
            <div class="d-flex align-center">
              <v-avatar v-if="item.LogoUrl || item.logoUrl" size="32" class="mr-2"><img :src="item.LogoUrl || item.logoUrl" alt="logo"></v-avatar>
              <div>{{ item.Name || item.name }}</div>
            </div>
          </td>
          <td>
            <div>{{ fmtDate(item.RegistrationStartTime || item.registrationStartTime) }} ~ {{ fmtDate(item.RegistrationEndTime || item.registrationEndTime) }}</div>
          </td>
          <td>
            <div>{{ fmtDate(item.CompetitionStartTime || item.competitionStartTime) }} <span v-if="item.CompetitionEndTime || item.competitionEndTime">~ {{ fmtDate(item.CompetitionEndTime || item.competitionEndTime) }}</span></div>
          </td>
          <td>{{ statusText(item.Status ?? item.status) }}</td>
          <td>{{ item.ChampionTeamName || item.championTeamName || '未设置' }}</td>
          <td>
            <v-btn variant="text" prepend-icon="mdi-pencil" @click="openEdit(item)">编辑</v-btn>
            <v-btn variant="text" prepend-icon="mdi-crown" @click="openChampion(item)">设置冠军</v-btn>
            <v-btn variant="text" prepend-icon="mdi-account-group" @click="openRegistrations(item)">报名队伍</v-btn>
            <v-btn variant="text" prepend-icon="mdi-image" @click="openLogo(item)">Logo</v-btn>
            <v-btn color="error" variant="text" prepend-icon="mdi-delete" @click="onDelete(item)">删除</v-btn>
          </td>
        </tr>
      </template>
      <template #bottom>
        <div class="d-flex align-center pa-2">
          <v-spacer></v-spacer>
          <v-select :items="[6,12,24,48]" v-model="pageSize" density="compact" style="max-width: 120px" />
        </div>
      </template>
    </v-data-table>

    <!-- 创建 -->
    <v-dialog v-model="createDialog" max-width="640">
      <v-card>
        <v-card-title>创建赛事</v-card-title>
        <v-card-text>
          <v-text-field v-model="form.name" label="名称" required />
          <v-textarea v-model="form.description" label="描述" />
          <v-row>
            <v-col cols="12" md="6"><v-text-field v-model="form.registrationStartTime" type="datetime-local" label="报名开始" /></v-col>
            <v-col cols="12" md="6"><v-text-field v-model="form.registrationEndTime" type="datetime-local" label="报名结束" /></v-col>
          </v-row>
          <v-row>
            <v-col cols="12" md="6"><v-text-field v-model="form.competitionStartTime" type="datetime-local" label="比赛开始" /></v-col>
            <v-col cols="12" md="6"><v-text-field v-model="form.competitionEndTime" type="datetime-local" label="比赛结束(可选)" /></v-col>
          </v-row>
          <v-text-field v-model="form.maxTeams" type="number" label="最大队伍(可选)" />
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn variant="text" @click="createDialog=false">取消</v-btn>
          <v-btn color="primary" @click="submitCreate">保存</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- 编辑 -->
    <v-dialog v-model="editDialog" max-width="680">
      <v-card>
        <v-card-title>编辑赛事</v-card-title>
        <v-card-text>
          <v-text-field v-model="form.name" label="名称" required />
          <v-textarea v-model="form.description" label="描述" />
          <v-row>
            <v-col cols="12" md="6"><v-text-field v-model="form.registrationStartTime" type="datetime-local" label="报名开始" /></v-col>
            <v-col cols="12" md="6"><v-text-field v-model="form.registrationEndTime" type="datetime-local" label="报名结束" /></v-col>
          </v-row>
          <v-row>
            <v-col cols="12" md="6"><v-text-field v-model="form.competitionStartTime" type="datetime-local" label="比赛开始" /></v-col>
            <v-col cols="12" md="6"><v-text-field v-model="form.competitionEndTime" type="datetime-local" label="比赛结束(可选)" /></v-col>
          </v-row>
          <v-row>
            <v-col cols="12" md="6"><v-text-field v-model="form.maxTeams" type="number" label="最大队伍(可选)" /></v-col>
            <v-col cols="12" md="6">
              <v-select :items="statusOptions" item-title="text" item-value="value" v-model="form.status" label="状态" />
            </v-col>
          </v-row>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn variant="text" @click="editDialog=false">取消</v-btn>
          <v-btn color="primary" @click="submitEdit">保存</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- 冠军设置 -->
    <v-dialog v-model="championDialog" max-width="560">
      <v-card>
        <v-card-title>设置冠军战队</v-card-title>
        <v-card-text>
          <div class="text-medium-emphasis mb-2">从已报名队伍中选择；留空清除冠军</div>
          <v-select :items="registrations" v-model="championTeamId" label="冠军战队(可选)" clearable />
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn variant="text" @click="championDialog=false">取消</v-btn>
          <v-btn color="primary" @click="submitChampion">保存</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Logo上传 -->
    <v-dialog v-model="logoDialog" max-width="520">
      <v-card>
        <v-card-title>上传赛事Logo</v-card-title>
        <v-card-text>
          <v-file-input accept="image/*" label="选择Logo" @update:model-value="files => submitLogo(files?.[0])" />
          <div class="text-medium-emphasis">选择文件后立即上传</div>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn variant="text" @click="logoDialog=false">关闭</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- 报名队伍 -->
    <v-dialog v-model="registrationsDialog" max-width="900">
      <v-card>
        <v-card-title class="d-flex align-center">
          <div>报名队伍</div>
          <v-spacer></v-spacer>
          <v-btn :loading="exporting" color="primary" prepend-icon="mdi-download" @click="exportCsv">导出CSV</v-btn>
        </v-card-title>
        <v-card-text>
          <div class="text-medium-emphasis mb-2">QQ号默认遮蔽显示，鼠标悬停显示完整（在上方确认后）；也可一键复制。</div>
          <div class="mb-3">
            <v-btn color="secondary" prepend-icon="mdi-shield-check" @click="confirmShowFull">显示完整号码</v-btn>
            <v-btn class="ml-2" prepend-icon="mdi-content-copy" @click="copyAll">批量复制QQ号</v-btn>
          </div>
          <v-table density="comfortable">
            <thead>
              <tr>
                <th class="text-left">队伍</th>
                <th class="text-left">QQ号</th>
                <th class="text-left">操作</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in registrations" :key="row.teamId">
                <td>{{ row.teamName }}</td>
                <td>
                  <span :title="showFull && row.qqFull ? row.qqFull : ''">{{ maskOrFull(row) }}</span>
                </td>
                <td>
                  <v-btn size="small" prepend-icon="mdi-content-copy" @click="copyOne(row)">复制</v-btn>
                </td>
              </tr>
            </tbody>
          </v-table>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn variant="text" @click="registrationsDialog=false">关闭</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>

</template>

<style scoped>
</style>

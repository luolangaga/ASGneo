<script setup>
import { ref, reactive, onMounted, watch } from 'vue'
import { getTeams, searchTeams, getTeam, createTeam, updateTeam, deleteTeam, uploadTeamLogo } from '../services/teams'
import { getMyRole } from '../services/roles'
import { notifySuccess, notifyError } from '../notify'

const loading = ref(false)
const errorMsg = ref('')
const keyword = ref('')
const items = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)

const createDialog = ref(false)
const editDialog = ref(false)
const logoDialog = ref(false)
const teamsShowFull = ref(false)

const editing = ref(null)

const form = reactive({
  name: '',
  password: '',
  qq: '',
  description: '',
  players: [{ name: '', gameId: '', gameRank: '', description: '' }],
})

onMounted(() => { fetchData() })
watch([page, pageSize], () => fetchData())

async function fetchData() {
  loading.value = true
  errorMsg.value = ''
  try {
    let res
    if (keyword.value) {
      res = await searchTeams(keyword.value || '', page.value, pageSize.value)
    } else {
      res = await getTeams(page.value, pageSize.value)
    }
    items.value = res?.Items ?? res?.items ?? []
    total.value = res?.TotalCount ?? res?.totalCount ?? items.value.length
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载失败'
  } finally { loading.value = false }
}

function maskQq(v) {
  const s = String(v || '').trim()
  if (!s) return ''
  if (s.length <= 7) return s
  const prefix = s.slice(0, 3)
  const suffix = s.slice(-4)
  return `${prefix}****${suffix}`
}

async function enableTeamsFull() {
  if (!confirm('显示完整QQ号需要管理员权限，确认继续？')) return
  try {
    const role = await getMyRole()
    const val = role?.Value ?? role?.value
    if (Number(val) !== 2) {
      throw new Error('仅管理员可查看完整QQ号')
    }
    teamsShowFull.value = true
    notifySuccess('已启用完整QQ显示')
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '权限验证失败')
  }
}

function addPlayer() { form.players.push({ name: '', gameId: '', gameRank: '', description: '' }) }
function removePlayer(i) { if (form.players.length > 1) form.players.splice(i, 1) }

function openCreate() {
  Object.assign(form, { name: '', password: '', qq: '', description: '', players: [{ name: '', gameId: '', gameRank: '', description: '' }] })
  createDialog.value = true
}

async function submitCreate() {
  try {
    const dto = {
      Name: form.name,
      Password: form.password,
      QqNumber: form.qq,
      Description: form.description || null,
      Players: form.players.map(p => ({ Name: p.name, GameId: p.gameId || null, GameRank: p.gameRank || null, Description: p.description || null })),
    }
    await createTeam(dto)
    notifySuccess('战队创建成功')
    createDialog.value = false
    await fetchData()
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '创建失败')
  }
}

async function openEdit(item) {
  try {
    const id = item.Id || item.id
    const detail = await getTeam(id)
    editing.value = detail
    Object.assign(form, {
      name: detail.Name || detail.name || '',
      password: '',
      qq: detail.QqNumber || detail.qqNumber || '',
      description: detail.Description || detail.description || '',
      players: (detail.Players || detail.players || []).map(pl => ({ id: pl.Id || pl.id, name: pl.Name || pl.name, gameId: pl.GameId || pl.gameId || '', gameRank: pl.GameRank || pl.gameRank || '', description: pl.Description || pl.description || '' }))
    })
    if (!form.players.length) form.players = [{ name: '', gameId: '', gameRank: '', description: '' }]
    editDialog.value = true
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '加载详情失败')
  }
}

async function submitEdit() {
  try {
    const id = editing.value?.Id || editing.value?.id
    if (!id) throw new Error('缺少战队ID')
    const dto = {
      Name: form.name,
      QqNumber: form.qq || null,
      Description: form.description || null,
      Players: form.players.map(p => ({ Id: p.id || null, Name: p.name, GameId: p.gameId || null, GameRank: p.gameRank || null, Description: p.description || null })),
    }
    await updateTeam(id, dto)
    notifySuccess('战队更新成功')
    editDialog.value = false
    await fetchData()
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '更新失败')
  }
}

async function onDelete(item) {
  if (!confirm(`删除战队 ${item.Name || item.name}?`)) return
  try {
    const id = item.Id || item.id
    await deleteTeam(id)
    notifySuccess('删除成功')
    await fetchData()
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '删除失败')
  }
}

function openLogo(item) { editing.value = item; logoDialog.value = true }
async function submitLogo(file) {
  try {
    const id = editing.value?.Id || editing.value?.id
    if (!file) throw new Error('请先选择Logo文件')
    await uploadTeamLogo(id, file)
    notifySuccess('Logo上传成功')
    logoDialog.value = false
    await fetchData()
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '上传失败')
  }
}

async function copyTeamQq(item) {
  const text = String(item.QqNumber || item.qqNumber || '').trim()
  if (!text) return
  try {
    await navigator.clipboard.writeText(text)
    notifySuccess('已复制QQ号')
  } catch {
    try {
      const input = document.createElement('textarea')
      input.value = text
      document.body.appendChild(input)
      input.select()
      document.execCommand('copy')
      document.body.removeChild(input)
      notifySuccess('已复制QQ号')
    } catch {
      notifyError('复制失败')
    }
  }
}
</script>

<template>
  <v-container class="py-6" style="max-width: 1200px">
    <div class="d-flex align-center mb-4">
      <div class="text-h5">战队管理</div>
      <v-spacer></v-spacer>
      <v-text-field v-model="keyword" density="compact" placeholder="搜索战队名称" append-inner-icon="mdi-magnify" class="mr-2" style="max-width: 280px" @keyup.enter="fetchData" />
      <v-btn color="primary" prepend-icon="mdi-magnify" @click="fetchData">搜索</v-btn>
      <v-divider vertical class="mx-4" />
      <v-btn color="primary" prepend-icon="mdi-plus" @click="openCreate">创建战队</v-btn>
    </div>

    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />

    <v-data-table :items="items" :items-length="total" :page.sync="page" :items-per-page="pageSize" :loading="loading" class="elevation-1">
      <template #headers>
        <tr>
          <th class="text-left">名称</th>
          <th class="text-left">描述</th>
          <th class="text-left">QQ号</th>
          <th class="text-left">成员数</th>
          <th class="text-left">点赞</th>
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
          <td>{{ item.Description || item.description }}</td>
          <td>
            <span :title="teamsShowFull ? (item.QqNumber || item.qqNumber || '') : ''">{{ teamsShowFull ? (item.QqNumber || item.qqNumber || '') : maskQq(item.QqNumber || item.qqNumber || '') }}</span>
            <v-btn size="x-small" class="ml-2" prepend-icon="mdi-content-copy" @click="() => copyTeamQq(item)">复制</v-btn>
            <v-btn v-if="!teamsShowFull" size="x-small" class="ml-1" prepend-icon="mdi-shield-check" @click="enableTeamsFull">显示完整号码</v-btn>
          </td>
          <td>{{ (item.Players || item.players || []).length }}</td>
          <td>{{ item.Likes ?? item.likes ?? 0 }}</td>
          <td>
            <v-btn variant="text" prepend-icon="mdi-pencil" @click="openEdit(item)">编辑</v-btn>
            <v-btn variant="text" prepend-icon="mdi-image" @click="openLogo(item)">Logo</v-btn>
            <v-btn color="error" variant="text" prepend-icon="mdi-delete" @click="onDelete(item)">删除</v-btn>
          </td>
        </tr>
      </template>
      <template #bottom>
        <div class="d-flex align-center pa-2">
          <v-spacer></v-spacer>
          <v-select :items="[5,10,20,50]" v-model="pageSize" density="compact" style="max-width: 120px" />
        </div>
      </template>
    </v-data-table>

    <!-- 创建战队 -->
    <v-dialog v-model="createDialog" max-width="720">
      <v-card>
        <v-card-title>创建战队</v-card-title>
        <v-card-text>
          <v-text-field v-model="form.name" label="战队名称" required />
          <v-text-field v-model="form.password" label="战队密码(至少6位)" type="password" required />
          <v-text-field v-model="form.qq" label="QQ号" required />
          <v-textarea v-model="form.description" label="战队描述(可选)" />
          <div class="text-subtitle-2 mt-2 mb-1">玩家列表</div>
          <div v-for="(p,i) in form.players" :key="i" class="d-flex flex-wrap gap-2 mb-2">
            <v-text-field v-model="p.name" label="姓名" style="max-width: 200px" />
            <v-text-field v-model="p.gameId" label="游戏ID" style="max-width: 180px" />
            <v-text-field v-model="p.gameRank" label="段位" style="max-width: 140px" />
            <v-text-field v-model="p.description" label="描述" style="max-width: 240px" />
            <v-btn variant="text" color="error" prepend-icon="mdi-delete" @click="removePlayer(i)" :disabled="form.players.length===1">移除</v-btn>
          </div>
          <v-btn variant="text" prepend-icon="mdi-plus" @click="addPlayer">添加玩家</v-btn>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn variant="text" @click="createDialog=false">取消</v-btn>
          <v-btn color="primary" @click="submitCreate">保存</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- 编辑战队 -->
    <v-dialog v-model="editDialog" max-width="720">
      <v-card>
        <v-card-title>编辑战队</v-card-title>
        <v-card-text>
          <v-text-field v-model="form.name" label="战队名称" required />
          <v-text-field v-model="form.qq" label="QQ号(可选)" />
          <v-textarea v-model="form.description" label="战队描述(可选)" />
          <div class="text-subtitle-2 mt-2 mb-1">玩家列表</div>
          <div v-for="(p,i) in form.players" :key="p.id || i" class="d-flex flex-wrap gap-2 mb-2">
            <v-text-field v-model="p.name" label="姓名" style="max-width: 200px" />
            <v-text-field v-model="p.gameId" label="游戏ID" style="max-width: 180px" />
            <v-text-field v-model="p.gameRank" label="段位" style="max-width: 140px" />
            <v-text-field v-model="p.description" label="描述" style="max-width: 240px" />
            <v-btn variant="text" color="error" prepend-icon="mdi-delete" @click="removePlayer(i)" :disabled="form.players.length===1">移除</v-btn>
          </div>
          <v-btn variant="text" prepend-icon="mdi-plus" @click="addPlayer">添加玩家</v-btn>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn variant="text" @click="editDialog=false">取消</v-btn>
          <v-btn color="primary" @click="submitEdit">保存</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- Logo上传 -->
    <v-dialog v-model="logoDialog" max-width="520">
      <v-card>
        <v-card-title>上传战队Logo</v-card-title>
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
  </v-container>
</template>

<style scoped>
.gap-2 { gap: 8px; }
</style>

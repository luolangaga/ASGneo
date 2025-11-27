<script setup>
import { ref, onMounted } from 'vue'
import { searchTeams, generateInvite } from '../services/teams'
import { notifySuccess, notifyError } from '../notify'

const keyword = ref('')
const loading = ref(false)
const items = ref([])
const page = ref(1)
const pageSize = ref(10)
const errorMsg = ref('')

const validDays = ref(7)
const lastInvite = ref(null)
const inviteDialog = ref(false)

async function fetchTeams() {
  loading.value = true
  errorMsg.value = ''
  try {
    const res = await searchTeams(keyword.value || '', page.value, pageSize.value)
    items.value = res?.Items ?? res?.items ?? []
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载战队失败'
  } finally {
    loading.value = false
  }
}

onMounted(() => fetchTeams())

async function onGenerate(item) {
  try {
    const id = item.Id || item.id
    const dto = await generateInvite(id, Number(validDays.value) || 7)
    lastInvite.value = dto
    inviteDialog.value = true
    notifySuccess('邀请Token已生成')
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '生成失败')
  }
}

async function copy(text) {
  try {
    await navigator.clipboard.writeText(String(text))
    notifySuccess('已复制到剪贴板')
  } catch {
    try {
      const input = document.createElement('textarea')
      input.value = String(text)
      document.body.appendChild(input)
      input.select()
      document.execCommand('copy')
      document.body.removeChild(input)
      notifySuccess('已复制到剪贴板')
    } catch {
      notifyError('复制失败')
    }
  }
}
</script>

<template>
  <v-container class="py-6" style="max-width: 980px">
    <div class="d-flex align-center mb-4">
      <div class="text-h5">生成绑定 Token</div>
      <v-spacer></v-spacer>
      <v-text-field 
        v-model="keyword" 
        density="compact" 
        placeholder="输入战队名称搜索" 
        append-inner-icon="mdi-magnify" 
        class="mr-2" 
        style="max-width: 320px" 
        @keyup.enter="fetchTeams" 
      />
      <v-btn color="primary" prepend-icon="mdi-magnify" @click="fetchTeams">搜索</v-btn>
    </div>

    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />

    <div class="d-flex align-center mb-3">
      <div class="text-subtitle-2 mr-2">有效期(天)：</div>
      <v-text-field v-model="validDays" type="number" density="compact" style="max-width: 120px" />
      <v-spacer></v-spacer>
      <div class="text-medium-emphasis">生成后可在QQ群聊使用：<code>绑定战队 Token</code></div>
    </div>

    <v-data-table :items="items" :loading="loading" class="elevation-1">
      <template #headers>
        <tr>
          <th class="text-left">战队</th>
          <th class="text-left">描述</th>
          <th class="text-left">操作</th>
        </tr>
      </template>
      <template #item="{ item }">
        <tr>
          <td>{{ item.Name || item.name }}</td>
          <td>{{ item.Description || item.description }}</td>
          <td>
            <v-btn color="primary" prepend-icon="mdi-key-outline" @click="onGenerate(item)">生成Token</v-btn>
          </td>
        </tr>
      </template>
    </v-data-table>

    <v-dialog v-model="inviteDialog" max-width="640">
      <v-card>
        <v-card-title>邀请信息</v-card-title>
        <v-card-text>
          <div class="mb-2">战队：{{ lastInvite?.TeamName }}</div>
          <div class="mb-2">Token：<code>{{ lastInvite?.Token }}</code> <v-btn size="x-small" class="ml-2" prepend-icon="mdi-content-copy" @click="() => copy(lastInvite?.Token)">复制</v-btn></div>
          <div class="mb-2">过期时间：{{ new Date(lastInvite?.ExpiresAt).toLocaleString() }}</div>
          <v-divider class="my-3" />
          <div class="text-medium-emphasis">使用方法：</div>
          <div>将上方 Token 发送到指定QQ群，命令：<code>绑定战队 Token</code>。</div>
          <div>绑定成功后，Token 立即失效。</div>
        </v-card-text>
        <v-card-actions>
          <v-spacer></v-spacer>
          <v-btn variant="text" @click="inviteDialog=false">关闭</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
  
</template>

<style scoped>
</style>

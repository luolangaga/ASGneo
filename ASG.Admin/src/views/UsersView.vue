<script setup>
import { ref, onMounted, watch } from 'vue'
import { getUsersPaged, deleteUser, updateEmailCredits, createUser } from '../services/users'
import { getAllRoles, updateUserRole } from '../services/roles'
import { notifySuccess, notifyError } from '../notify'

const loading = ref(false)
const errorMsg = ref('')
const items = ref([])
const total = ref(0)
const page = ref(1)
const pageSize = ref(10)
const search = ref('')
const roles = ref([])

// 创建用户
const createDialog = ref(false)
const createLoading = ref(false)
const newUser = ref({
  email: '',
  password: '',
  fullName: '',
  role: 0
})

// 编辑邮件积分
const creditsDialog = ref(false)
const creditsSaving = ref(false)
const creditsValue = ref(0)
const creditsUser = ref(null)

const headers = [
  { title: '邮箱', key: 'email', align: 'start' },
  { title: '姓名', key: 'fullName', align: 'start' },
  { title: '角色', key: 'roleName', align: 'start', sortable: false },
  { title: '邮件积分', key: 'emailCredits', align: 'start' },
  { title: '注册时间', key: 'createdAt', align: 'start' },
  { title: '操作', key: 'actions', align: 'end', sortable: false },
]

onMounted(async () => {
  try { roles.value = await getAllRoles() } catch {}
  // fetchData() // Initial fetch is triggered by v-data-table-server mounting or options update
})

// watch([page, pageSize], () => { fetchData() }) // v-data-table-server handles this via @update:options

async function loadItems({ page: p, itemsPerPage: ps, sortBy }) {
  page.value = p
  pageSize.value = ps
  await fetchData()
}

async function fetchData() {
  loading.value = true
  errorMsg.value = ''
  try {
    const params = { 
      pageNumber: page.value, 
      pageSize: pageSize.value,
      search: search.value 
    }
    const res = await getUsersPaged(params)
    // 对齐 RoleController 的分页返回结构
    items.value = res?.Users ?? res?.users ?? []
    total.value = res?.TotalCount ?? res?.totalCount ?? items.value.length
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载失败'
  } finally { loading.value = false }
}

async function onDelete(user) {
  if (!confirm(`删除用户 ${user.email} ?`)) return
  try {
    await deleteUser(user.id || user.Id)
    notifySuccess('删除成功')
    await fetchData()
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '删除失败')
  }
}

function findRoleValueByName(roleName) {
  const r = roles.value.find(r => (r?.RoleName || r?.roleName) === roleName)
  return r?.Value ?? r?.value ?? null
}

async function onAssignRole(user, roleName) {
  const roleValue = findRoleValueByName(roleName)
  if (roleValue == null) {
    notifyError('未知角色')
    return
  }
  try {
    await updateUserRole(user.id || user.Id, roleValue)
    notifySuccess('角色更新成功')
    await fetchData()
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '角色更新失败')
  }
}

function openCredits(user) {
  creditsUser.value = user
  const current = user.emailCredits ?? user.EmailCredits ?? 0
  creditsValue.value = Number(current) || 0
  creditsDialog.value = true
}

async function saveCredits() {
  if (!creditsUser.value) return
  creditsSaving.value = true
  try {
    const id = creditsUser.value.id || creditsUser.value.Id
    await updateEmailCredits(id, Number(creditsValue.value || 0))
    notifySuccess('邮件积分已更新')
    creditsDialog.value = false
    creditsUser.value = null
    await fetchData()
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '更新失败')
  } finally {
    creditsSaving.value = false
  }
}

async function onCreateUser() {
  if (!newUser.value.email || !newUser.value.password || !newUser.value.fullName) {
    notifyError('请填写所有必填项')
    return
  }
  createLoading.value = true
  try {
    await createUser(newUser.value)
    notifySuccess('用户创建成功')
    createDialog.value = false
    newUser.value = { email: '', password: '', fullName: '', role: 0 }
    await fetchData()
  } catch (err) {
    notifyError(err?.payload?.message || err?.message || '创建失败')
  } finally {
    createLoading.value = false
  }
}

function formatDate(dateStr) {
  if (!dateStr) return '-'
  return new Date(dateStr).toLocaleString()
}
</script>

<template>
  <v-container fluid class="pa-6">
    <div class="d-flex flex-wrap align-center justify-space-between gap-4 mb-6">
      <div>
        <h1 class="text-h4 font-weight-bold text-primary mb-1">用户管理</h1>
        <div class="text-subtitle-1 text-medium-emphasis">管理系统所有注册用户及其权限</div>
      </div>
      
      <div class="d-flex align-center gap-2" style="min-width: 300px">
        <v-btn 
          color="primary" 
          height="48" 
          class="px-4 mr-2"
          prepend-icon="mdi-plus"
          @click="createDialog = true"
        >
          新建用户
        </v-btn>

        <v-text-field 
          v-model="search" 
          density="comfortable" 
          variant="outlined"
          placeholder="搜索邮箱/姓名..." 
          prepend-inner-icon="mdi-magnify" 
          hide-details 
          @keyup.enter="fetchData"
          bg-color="surface"
          class="flex-grow-1"
        />
        <v-btn 
          color="secondary" 
          variant="tonal"
          height="48" 
          class="px-6"
          prepend-icon="mdi-magnify"
          @click="fetchData"
        >
          搜索
        </v-btn>
      </div>
    </div>

    <v-alert v-if="errorMsg" type="error" variant="tonal" :text="errorMsg" class="mb-6" closable />

    <v-card border elevation="0" rounded="lg">
      <v-data-table-server
        v-model:items-per-page="pageSize"
        v-model:page="page"
        :headers="headers"
        :items="items"
        :items-length="total"
        :loading="loading"
        @update:options="loadItems"
        class="rounded-lg"
        hover
      >
        <!-- 邮箱列 -->
        <template #item.email="{ item }">
          <div class="d-flex align-center gap-2">
            <v-avatar color="primary-lighten-4" size="32" variant="flat">
              <span class="text-primary font-weight-bold text-caption">
                {{ (item.email || '?').charAt(0).toUpperCase() }}
              </span>
            </v-avatar>
            <div class="font-weight-medium">{{ item.email }}</div>
          </div>
        </template>

        <!-- 姓名列 -->
        <template #item.fullName="{ item }">
          {{ item.fullName || item.FullName || '-' }}
        </template>

        <!-- 角色列 -->
        <template #item.roleName="{ item }">
          <v-select 
            :items="roles.map(r => r?.RoleName || r?.roleName || r)" 
            :model-value="item.roleName || item.RoleName" 
            density="compact" 
            variant="outlined"
            hide-details
            bg-color="transparent"
            style="width: 140px" 
            @update:model-value="val => onAssignRole(item, val)" 
          />
        </template>

        <!-- 积分列 -->
        <template #item.emailCredits="{ item }">
          <v-chip 
            color="amber-darken-2" 
            variant="outlined" 
            size="small" 
            class="font-weight-bold pr-1"
          >
            {{ item.emailCredits ?? item.EmailCredits ?? 0 }}
            <v-btn 
              variant="text" 
              density="compact" 
              size="x-small" 
              icon="mdi-pencil" 
              class="ml-1"
              @click="openCredits(item)" 
              aria-label="编辑积分"
            ></v-btn>
          </v-chip>
        </template>

        <!-- 时间列 -->
        <template #item.createdAt="{ item }">
          <span class="text-caption text-medium-emphasis">
            {{ formatDate(item.createdAt || item.CreatedAt) }}
          </span>
        </template>

        <!-- 操作列 -->
        <template #item.actions="{ item }">
          <div class="d-flex justify-end gap-1">
            <v-btn 
              variant="text" 
              color="info" 
              size="small"
              prepend-icon="mdi-information-outline" 
              :to="`/users/${item.id || item.Id}`"
            >
              详情
            </v-btn>
            <v-btn 
              variant="text" 
              color="error" 
              size="small"
              prepend-icon="mdi-delete-outline" 
              @click="onDelete(item)"
            >
              删除
            </v-btn>
          </div>
        </template>
      </v-data-table-server>
    </v-card>

    <!-- 新建用户对话框 -->
    <v-dialog v-model="createDialog" max-width="500">
      <v-card rounded="lg">
        <v-card-title class="d-flex align-center justify-space-between pl-4 pr-2 py-3 border-b">
          <span>新建用户</span>
          <v-btn icon="mdi-close" variant="text" size="small" @click="createDialog = false"></v-btn>
        </v-card-title>
        
        <v-card-text class="pt-4">
          <v-row>
            <v-col cols="12">
              <v-text-field 
                v-model="newUser.email" 
                label="邮箱" 
                variant="outlined" 
                prepend-inner-icon="mdi-email-outline"
                hide-details="auto"
              />
            </v-col>
            <v-col cols="12">
              <v-text-field 
                v-model="newUser.fullName" 
                label="姓名" 
                variant="outlined" 
                prepend-inner-icon="mdi-account-outline"
                hide-details="auto"
              />
            </v-col>
            <v-col cols="12">
              <v-text-field 
                v-model="newUser.password" 
                label="密码" 
                type="password"
                variant="outlined" 
                prepend-inner-icon="mdi-lock-outline"
                hide-details="auto"
                hint="至少6位字符"
              />
            </v-col>
            <v-col cols="12">
              <v-select
                v-model="newUser.role"
                :items="roles"
                item-title="RoleName"
                item-value="Value"
                label="角色"
                variant="outlined"
                prepend-inner-icon="mdi-shield-account-outline"
                hide-details="auto"
              />
            </v-col>
          </v-row>
        </v-card-text>
        
        <v-card-actions class="px-4 pb-4">
          <v-spacer></v-spacer>
          <v-btn variant="text" @click="createDialog = false">取消</v-btn>
          <v-btn color="primary" variant="flat" :loading="createLoading" @click="onCreateUser">创建用户</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <!-- 编辑邮件积分对话框 -->
    <v-dialog v-model="creditsDialog" max-width="400">
      <v-card rounded="lg">
        <v-card-title class="d-flex align-center justify-space-between pl-4 pr-2 py-3 border-b">
          <span>编辑邮件积分</span>
          <v-btn icon="mdi-close" variant="text" size="small" @click="creditsDialog = false"></v-btn>
        </v-card-title>
        
        <v-card-text class="pt-4">
          <div class="text-body-2 text-medium-emphasis mb-4">
            正在修改用户 <strong>{{ creditsUser?.email }}</strong> 的邮件积分。
          </div>
          <v-text-field 
            v-model.number="creditsValue" 
            type="number" 
            label="现有积分" 
            variant="outlined"
            min="0" 
            step="1" 
            hide-details 
            density="comfortable" 
            prefix="#"
          />
        </v-card-text>
        
        <v-card-actions class="px-4 pb-4">
          <v-spacer></v-spacer>
          <v-btn variant="text" @click="creditsDialog = false">取消</v-btn>
          <v-btn color="primary" variant="flat" :loading="creditsSaving" @click="saveCredits">保存更改</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </v-container>
</template>

<style scoped>
.gap-2 { gap: 8px; }
.gap-4 { gap: 16px; }
</style>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import PageHero from '../components/PageHero.vue'
import MarkdownEditor from '../components/MarkdownEditor.vue'
import { currentUser, isAuthenticated, updateCurrentUser } from '../stores/auth'
import { getTeam, updateTeam, uploadTeamLogo, deleteTeam } from '../services/teams'
import { getProfile } from '../services/user'

const router = useRouter()

const me = computed(() => currentUser.value)
const teamId = computed(() => me.value?.teamId || me.value?.TeamId || null)

const loading = ref(false)
const saving = ref(false)
const uploadingLogo = ref(false)
const errorMsg = ref('')
const successMsg = ref('')
const deleting = ref(false)

const name = ref('')
const description = ref('')
const players = ref([])
const logoUrl = ref('')

const canEdit = computed(() => isAuthenticated.value && !!teamId.value)

function addPlayer() {
  players.value.push({ id: undefined, name: '', gameId: '', gameRank: '', description: '' })
}

function removePlayer(index) {
  players.value.splice(index, 1)
}

async function loadTeam() {
  if (!teamId.value) return
  loading.value = true
  errorMsg.value = ''
  try {
    const t = await getTeam(teamId.value)
    name.value = t.name || ''
    description.value = t.description || ''
    logoUrl.value = t.logoUrl || ''
    players.value = (t.players || []).map(p => ({
      id: p.id,
      name: p.name || '',
      gameId: p.gameId || '',
      gameRank: p.gameRank || '',
      description: p.description || '',
    }))
    if (!players.value.length) addPlayer()
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载战队失败'
  } finally {
    loading.value = false
  }
}

async function onSubmit() {
  if (!teamId.value) return
  saving.value = true
  errorMsg.value = ''
  successMsg.value = ''
  try {
    const descLen = (description.value || '').length
    if (descLen >= 1000) {
      errorMsg.value = '战队描述字数需小于 1000'
      throw new Error('战队描述超长')
    }
    if (!players.value.length) {
      errorMsg.value = '至少需要一个队员'
      throw new Error('队员为空')
    }
    const payload = {
      name: name.value.trim(),
      description: description.value?.trim() || null,
      players: players.value.map(p => ({
        // 现有玩家保留id，新增玩家不发送id字段
        id: p.id || undefined,
        name: (p.name || '').trim(),
        gameId: p.gameId?.trim() || null,
        gameRank: p.gameRank?.trim() || null,
        description: p.description?.trim() || null,
      })),
    }
    const updated = await updateTeam(teamId.value, payload)
    // 回填最新数据
    name.value = updated.name || name.value
    description.value = updated.description || description.value
    logoUrl.value = updated.logoUrl || logoUrl.value
    players.value = (updated.players || []).map(p => ({
      id: p.id,
      name: p.name || '',
      gameId: p.gameId || '',
      gameRank: p.gameRank || '',
      description: p.description || '',
    }))
    successMsg.value = '保存成功'
  } catch (err) {
    // 权限或其它错误
    const msg = err?.payload?.message || err?.message || '保存失败'
    errorMsg.value = msg.includes('权限') ? '你没有权限修改此战队' : msg
  } finally {
    saving.value = false
  }
}

async function onUploadLogo(e) {
  if (!teamId.value) return
  const file = e.target?.files?.[0]
  if (!file) return
  uploadingLogo.value = true
  errorMsg.value = ''
  successMsg.value = ''
  try {
    const res = await uploadTeamLogo(teamId.value, file)
    logoUrl.value = res.logoUrl || logoUrl.value
    successMsg.value = '徽标已更新'
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '上传徽标失败'
  } finally {
    uploadingLogo.value = false
    // 清空文件选择
    e.target.value = ''
  }
}

onMounted(() => {
  if (!canEdit.value) return
  loadTeam()
})

async function onDeleteTeam() {
  if (!teamId.value) return
  const ok = window.confirm(`确定要删除战队“${name.value}”吗？此操作不可恢复。`)
  if (!ok) return
  deleting.value = true
  errorMsg.value = ''
  successMsg.value = ''
  try {
    await deleteTeam(teamId.value)
    // 刷新当前用户，清空本地的 teamId
    try {
      const profile = await getProfile()
      updateCurrentUser(profile)
    } catch {}
    router.push('/teams/create')
  } catch (err) {
    const msg = err?.payload?.message || err?.message || '删除战队失败'
    errorMsg.value = msg
  } finally {
    deleting.value = false
  }
}
</script>

<template>
  <PageHero title="编辑我的战队" subtitle="修改战队基础信息与队员" icon="group">
    <template #actions>
      <v-btn variant="text" class="mb-3" to="/teams/search" prepend-icon="group">搜索战队</v-btn>
    </template>
  </PageHero>
  <v-container class="py-6" style="max-width: 900px">
    <template v-if="!isAuthenticated">
      <v-alert type="info" text="请先登录以编辑你的战队" class="mb-4" />
      <div class="d-flex gap-2">
        <v-btn color="primary" to="/login" prepend-icon="login">去登录</v-btn>
        <v-btn color="secondary" to="/register" prepend-icon="person_add">注册账号</v-btn>
      </div>
    </template>
    <template v-else-if="!teamId">
      <v-alert type="info" text="你还没有战队，创建或绑定一个战队以编辑。" class="mb-4" />
      <div class="d-flex flex-wrap gap-2 mb-4">
        <v-btn color="primary" to="/teams/create" prepend-icon="group">去创建战队</v-btn>
        <v-btn color="secondary" to="/profile" prepend-icon="link">去绑定已有战队</v-btn>
      </div>
    </template>
    <template v-else>
      <v-card>
        <v-card-title class="d-flex align-center">
          <v-avatar size="56" class="mr-3" color="primary" variant="tonal">
            <template v-if="logoUrl">
              <v-img :src="logoUrl" cover />
            </template>
            <template v-else>
              <span class="text-subtitle-1">{{ (name || '').charAt(0).toUpperCase() }}</span>
            </template>
          </v-avatar>
          <div class="flex-grow-1">编辑战队信息</div>
          <v-btn variant="text" to="/events" prepend-icon="grid_view">返回赛事</v-btn>
        </v-card-title>
        <v-card-text>
          <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
          <v-alert v-if="successMsg" type="success" :text="successMsg" class="mb-4" />

          <template v-if="loading">
            <v-skeleton-loader type="article" />
          </template>
          <template v-else>
            <v-form @submit.prevent="onSubmit">
              <v-text-field v-model="name" label="战队名称" prepend-inner-icon="group" required />
              <MarkdownEditor v-model="description" label="战队描述" :rows="6" :maxLength="999" />

              <v-divider class="my-4" />
              <div class="text-subtitle-1 mb-2">战队徽标</div>
              <div class="d-flex align-center gap-3 mb-3">
                <v-avatar size="64" v-if="logoUrl">
                  <v-img :src="logoUrl" cover />
                </v-avatar>
                <v-file-input
                  accept="image/png,image/jpeg,image/jpg,image/webp"
                  label="上传新徽标"
                  prepend-icon="image"
                  :loading="uploadingLogo"
                  @change="onUploadLogo"
                  hide-details
                />
              </div>

              <v-divider class="my-4" />
              <div class="d-flex align-center justify-space-between mb-2">
                <div class="text-subtitle-1">队员信息（至少 1 人）</div>
                <v-btn color="secondary" variant="tonal" @click="addPlayer" prepend-icon="person_add">添加队员</v-btn>
              </div>
              <v-row>
                <v-col cols="12" v-for="(p, idx) in players" :key="idx">
                  <v-card variant="tonal" class="mb-3">
                    <v-card-text>
                      <v-row>
                        <v-col cols="12" md="4">
                          <v-text-field v-model="p.name" label="队员昵称" prepend-inner-icon="person" required />
                        </v-col>
                        <v-col cols="12" md="4">
                          <v-text-field v-model="p.gameId" label="游戏ID" prepend-inner-icon="sports_esports" />
                        </v-col>
                        <v-col cols="12" md="4">
                          <v-text-field v-model="p.gameRank" label="段位/等级" prepend-inner-icon="star" />
                        </v-col>
                        <v-col cols="12">
                          <v-textarea v-model="p.description" label="简介" prepend-inner-icon="text_fields" />
                        </v-col>
                      </v-row>
                      <div class="d-flex justify-end">
                        <v-btn color="error" variant="text" @click="removePlayer(idx)" prepend-icon="delete" :disabled="players.length <= 1">移除</v-btn>
                      </div>
                    </v-card-text>
                  </v-card>
                </v-col>
              </v-row>

              <div class="d-flex justify-end">
                <v-btn color="error" variant="text" :loading="deleting" @click="onDeleteTeam" prepend-icon="delete" class="mr-2">删除战队</v-btn>
                <v-btn :loading="saving" color="primary" type="submit" prepend-icon="save">保存修改</v-btn>
              </div>
            </v-form>
          </template>
        </v-card-text>
      </v-card>
    </template>
  </v-container>
</template>

<style scoped>
.gap-2 { gap: 8px; }
.gap-3 { gap: 12px; }
</style>
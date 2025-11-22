<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { getProfile, updateProfile, uploadAvatar } from '../services/user'
import { updateCurrentUser, currentUser } from '../stores/auth'
import { getTeam, getTeamHonors, uploadTeamLogo, bindTeamByName, leaveTeam, getMyPlayer, upsertMyPlayer } from '../services/teams'
import { notify } from '../stores/notify'
import PageHero from '../components/PageHero.vue'
import { renderMarkdown } from '../utils/markdown'

const router = useRouter()
const loading = ref(false)
const saving = ref(false)
const uploading = ref(false)
const errorMsg = ref('')

const email = ref('')
const fullName = ref('')
const roleName = ref('')
const avatarUrl = ref('')
const localPreview = ref('')
const emailCredits = ref(0)
const userId = computed(() => (currentUser.value?.id || currentUser.value?.Id || ''))
// 战队相关状态
const team = ref(null)
const teamLoading = ref(false)
const teamError = ref('')
const logoUploading = ref(false)
const unbinding = ref(false)
// 战队荣誉
const honors = ref([])
const honorsLoading = ref(false)
const honorsError = ref('')
// 绑定战队表单状态
const showBind = ref(false)
const bindName = ref('')
const bindPassword = ref('')
const binding = ref(false)
const bindError = ref('')
const showPlayerPrompt = ref(false)
const playerSectionEl = ref(null)

// 我的玩家状态
const myPlayer = ref({ name: '', gameId: '', gameRank: '', description: '' })
const playerLoading = ref(false)
const playerSaving = ref(false)
const playerError = ref('')


async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    const profile = await getProfile()
    email.value = profile.email
    fullName.value = profile.fullName || profile.FullName || ''
    roleName.value = profile.roleDisplayName || profile.roleName || ''
    avatarUrl.value = profile.avatarUrl || ''
    emailCredits.value = profile.emailCredits ?? profile.EmailCredits ?? 0
    // 同步到全局用户（保持头像字段）
    updateCurrentUser({ ...(currentUser.value || {}), ...profile })

    // 如果用户已有战队，加载战队信息（兼容不同大小写）
    const tId = currentUser.value?.teamId || currentUser.value?.TeamId
    if (tId) {
      teamLoading.value = true
      teamError.value = ''
      try {
        team.value = await getTeam(tId)
        // 加载战队荣誉（冠军赛事）
        honorsLoading.value = true
        honorsError.value = ''
        try {
          honors.value = await getTeamHonors(tId)
        } catch (err2) {
          honorsError.value = err2?.payload?.message || err2?.message || '加载战队荣誉失败'
        } finally {
          honorsLoading.value = false
        }
      } catch (err) {
        teamError.value = err?.payload?.message || err?.message || '加载战队失败'
      } finally {
        teamLoading.value = false
      }
      try {
        playerLoading.value = true
        playerError.value = ''
        const p = await getMyPlayer()
        myPlayer.value = p || { name: '', gameId: '', gameRank: '', description: '' }
      } catch (err3) {
        playerError.value = err3?.payload?.message || err3?.message || ''
      } finally {
        playerLoading.value = false
      }
    }
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载资料失败'
  } finally {
    loading.value = false
  }
}

onMounted(load)

async function onSaveProfile() {
  saving.value = true
  errorMsg.value = ''
  try {
    const updated = await updateProfile({ fullName: fullName.value.trim() })
    avatarUrl.value = updated.avatarUrl || ''
    updateCurrentUser({ ...(currentUser.value || {}), ...updated })
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '保存失败'
  } finally {
    saving.value = false
  }
}

async function onFileChange(files) {
  const file = Array.isArray(files) ? files[0] : files
  if (!file) return
  localPreview.value = URL.createObjectURL(file)
  uploading.value = true
  errorMsg.value = ''
  try {
    const res = await uploadAvatar(file)
    avatarUrl.value = res?.avatarUrl || ''
    updateCurrentUser({ ...(currentUser.value || {}), avatarUrl: avatarUrl.value })
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '上传失败'
  } finally {
    uploading.value = false
  }
}

async function onTeamLogoChange(files) {
  const file = Array.isArray(files) ? files[0] : files
  const teamId = team.value?.id || team.value?.Id
  if (!file || !teamId) return
  logoUploading.value = true
  teamError.value = ''
  try {
    const res = await uploadTeamLogo(teamId, file)
    const newUrl = res?.logoUrl || res?.LogoUrl || team.value.logoUrl || team.value.LogoUrl || ''
    team.value = { ...team.value, logoUrl: newUrl, LogoUrl: newUrl }
  } catch (err) {
    teamError.value = err?.payload?.message || err?.message || '上传战队徽标失败'
  } finally {
    logoUploading.value = false
  }
}

async function onBindTeamByName() {
  binding.value = true
  bindError.value = ''
  try {
    const name = bindName.value.trim()
    const password = bindPassword.value.trim()
    if (!name || !password) {
      bindError.value = '请输入战队名称与密码'
      return
    }
    const res = await bindTeamByName({ name, password })
    // 重新加载资料以刷新TeamId并显示战队信息
    await load()
    showBind.value = false
    bindName.value = ''
    bindPassword.value = ''
    if (res?.needsPlayer) {
      showPlayerPrompt.value = true
    }
  } catch (err) {
    bindError.value = err?.payload?.message || err?.message || '绑定失败'
  } finally {
    binding.value = false
  }
}

async function onUnbindTeam() {
  if (unbinding.value) return
  // 简单确认，避免误触
  const ok = window.confirm('确认要退出当前战队吗？退出后将无法进行战队管理操作。')
  if (!ok) return
  unbinding.value = true
  teamError.value = ''
  try {
    const teamId = currentUser.value?.teamId || currentUser.value?.TeamId
    if (!teamId) { throw new Error('当前未绑定战队') }
    await leaveTeam(teamId)
    notify({ text: '已退出战队', color: 'success' })
    team.value = null
    // 重新加载资料以刷新用户 TeamId 状态
    await load()
  } catch (err) {
    teamError.value = err?.payload?.message || err?.message || '解绑失败'
  } finally {
    unbinding.value = false
  }
}

function toMd(s) {
  return renderMarkdown(s || '')
}

function goToPlayerSection() {
  try {
    showPlayerPrompt.value = false
    setTimeout(() => {
      const el = playerSectionEl.value
      if (el && typeof el.scrollIntoView === 'function') {
        el.scrollIntoView({ behavior: 'smooth', block: 'start' })
      }
    }, 50)
  } catch {}
}

function copyUserId() {
  try {
    const id = userId.value || ''
    if (!id) return
    navigator.clipboard?.writeText(id)
    notify({ text: '用户ID已复制', color: 'success' })
  } catch {
    // 兜底：无法复制时不抛错
  }
}

async function onSavePlayer() {
  const tId = currentUser.value?.teamId || currentUser.value?.TeamId
  if (!tId) {
    playerError.value = '请先绑定或创建战队，再创建玩家'
    return
  }
  playerSaving.value = true
  playerError.value = ''
  try {
    const payload = {
      name: (myPlayer.value?.name || '').trim(),
      gameId: myPlayer.value?.gameId || '',
      gameRank: myPlayer.value?.gameRank || '',
      description: myPlayer.value?.description || '',
    }
    const res = await upsertMyPlayer(payload)
    myPlayer.value = res || myPlayer.value
    notify({ text: '玩家信息已保存', color: 'success' })
  } catch (err) {
    playerError.value = err?.payload?.message || err?.message || '保存玩家失败'
  } finally {
    playerSaving.value = false
  }
}
</script>

<template>
  <PageHero title="个人资料" subtitle="头像、姓名与战队管理" icon="person">
    <template #actions>
      <v-btn variant="text" class="mb-3" to="/" prepend-icon="home">返回首页</v-btn>
    </template>
  </PageHero>
  <v-container class="py-8 narrow-container">
    <v-card>
      <v-card-title>个人资料</v-card-title>
      <v-card-text>
        <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />

        <v-row>
          <v-col cols="12" md="4">
            <div class="d-flex flex-column align-center">
              <v-avatar size="120" class="mb-3">
                <v-img :src="localPreview || avatarUrl" alt="avatar" cover>
                  <template #placeholder>
                    <div class="img-skeleton"></div>
                  </template>
                </v-img>
              </v-avatar>
              <v-file-input
                label="更换头像"
                accept="image/png, image/jpeg, image/jpg, image/webp"
                prepend-inner-icon="image"
                :loading="uploading"
                @update:modelValue="onFileChange"
              />
            </div>
          </v-col>
          <v-col cols="12" md="8">
            <v-form @submit.prevent="onSaveProfile">
              <v-text-field v-model="email" label="邮箱" prepend-inner-icon="mail" readonly />
              <div class="d-flex align-center">
                <v-text-field :model-value="userId" label="用户ID" prepend-inner-icon="badge" readonly class="flex-grow-1" />
                <v-btn variant="text" class="ml-2" prepend-icon="content_copy" @click="copyUserId">复制</v-btn>
              </div>
              <v-text-field v-model="fullName" label="姓名" prepend-inner-icon="person" required />
              <v-text-field v-model="roleName" label="角色" prepend-inner-icon="shield_person" readonly />
              <div class="d-flex align-center gap-2 mt-2">
                <v-chip color="primary" variant="tonal" prepend-icon="mail">邮件积分：{{ emailCredits }}</v-chip>
                <span class="text-caption text-medium-emphasis">用于邮件通知与提醒</span>
              </div>
              <div class="d-flex align-center justify-end">
                <v-btn :loading="saving" type="submit" color="primary">保存</v-btn>
              </div>
            </v-form>
          </v-col>
        </v-row>
      </v-card-text>
    </v-card>
  </v-container>
  <v-progress-linear v-if="loading" indeterminate color="primary" />
  <div class="text-center mt-2" v-if="loading">正在加载资料...</div>
  
  <!-- 我的战队 -->
  <v-container class="py-6 narrow-container">
    <v-card>
      <v-card-title>我的战队</v-card-title>
      <v-card-text>
        <template v-if="teamLoading">
          <v-progress-linear indeterminate color="primary" />
        </template>
        <v-alert v-if="teamError" type="error" :text="teamError" class="mb-3" />

        <template v-if="(currentUser?.teamId || currentUser?.TeamId) && team">
          <v-row>
            <v-col cols="12" md="4">
              <div class="d-flex flex-column align-center">
                <v-avatar size="120" class="mb-3" v-if="team.logoUrl || team.LogoUrl">
                  <v-img :src="team.logoUrl || team.LogoUrl" alt="team logo" cover>
                    <template #placeholder>
                      <div class="img-skeleton"></div>
                    </template>
                  </v-img>
                </v-avatar>
                <v-file-input
                  label="上传战队徽标"
                  accept="image/png, image/jpeg, image/jpg, image/webp"
                  prepend-inner-icon="image"
                  :loading="logoUploading"
                  @update:modelValue="onTeamLogoChange"
                />
              </div>
            </v-col>
            <v-col cols="12" md="8">
              <div class="text-subtitle-1 mb-2">{{ team.name || team.Name }}</div>
              <div class="text-body-2 mb-2 md-content" v-if="team.description || team.Description" v-html="toMd(team.description || team.Description)"></div>
              <div class="text-subtitle-2 mb-2">队员</div>
              <v-list density="compact" lines="two">
                <v-list-item v-for="p in (team.players || team.Players || [])" :key="p.id || p.Id || p.name || p.Name">
                  <v-list-item-title>{{ p.name || p.Name }}</v-list-item-title>
                  <v-list-item-subtitle>
                    <span v-if="p.gameId || p.GameId">ID: {{ p.gameId || p.GameId }} </span>
                    <span v-if="p.gameRank || p.GameRank" class="ml-2">段位: {{ p.gameRank || p.GameRank }}</span>
                  </v-list-item-subtitle>
                </v-list-item>
              </v-list>

              <v-divider class="my-4" />
              <div class="text-subtitle-2 mb-2">战队荣誉</div>
              <template v-if="honorsLoading">
                <v-progress-linear indeterminate color="primary" />
              </template>
              <v-alert v-if="honorsError" type="error" :text="honorsError" class="mb-2" />
              <template v-if="(honors || []).length">
                <v-list density="compact">
                  <v-list-item v-for="e in honors" :key="e.id || e.Id">
                    <template #prepend>
                      <v-avatar size="32" v-if="e.logoUrl || e.LogoUrl">
                        <v-img :src="e.logoUrl || e.LogoUrl" alt="event logo" cover>
                          <template #placeholder>
                            <div class="img-skeleton"></div>
                          </template>
                        </v-img>
                      </v-avatar>
                    </template>
                    <v-list-item-title>{{ e.name || e.Name }}</v-list-item-title>
                    <v-list-item-subtitle>冠军</v-list-item-subtitle>
                  </v-list-item>
                </v-list>
              </template>
              <template v-else-if="!honorsLoading && !honorsError">
                <v-alert type="info" text="暂无荣誉" density="compact" />
              </template>
              <v-divider class="my-4" />
              <div class="d-flex justify-end align-center gap-2">
                <v-btn color="primary" variant="tonal" to="/teams/edit" prepend-icon="edit">编辑信息</v-btn>
                <v-btn color="error" variant="text" :loading="unbinding" prepend-icon="logout" @click="onUnbindTeam">退出战队</v-btn>
              </div>
            </v-col>
          </v-row>
        </template>
        <template v-else>
          <v-alert type="info" text="你还没有战队，创建或绑定一个战队以报名赛事。" class="mb-3" />
          <div class="d-flex flex-wrap gap-2 mb-4">
            <v-btn color="primary" to="/teams/create" prepend-icon="group">去创建战队</v-btn>
            <v-btn color="secondary" prepend-icon="link" @click="showBind = true">绑定已有战队</v-btn>
          </div>

          <v-expand-transition>
            <div v-if="showBind">
              <v-form @submit.prevent="onBindTeamByName">
                <v-text-field v-model="bindName" label="战队名称" prepend-inner-icon="group" required />
                <v-text-field v-model="bindPassword" label="战队密码" type="password" prepend-inner-icon="lock" required />
                <div class="d-flex align-center justify-end">
                  <v-btn type="submit" color="secondary" :loading="binding" prepend-icon="link">绑定战队</v-btn>
                </div>
              </v-form>
              <v-alert v-if="bindError" type="error" :text="bindError" class="mt-3" />
            </div>
          </v-expand-transition>
        </template>
      </v-card-text>
    </v-card>
  </v-container>

  <v-container class="py-6 narrow-container">
    <v-card>
      <v-card-title>我的玩家</v-card-title>
      <v-card-text ref="playerSectionEl">
        <v-alert v-if="playerError" type="error" :text="playerError" class="mb-4" />
        <template v-if="(currentUser?.teamId || currentUser?.TeamId)">
          <v-form @submit.prevent="onSavePlayer">
            <v-text-field v-model="myPlayer.name" label="玩家昵称" prepend-inner-icon="person" required />
            <v-row>
              <v-col cols="12" md="6">
                <v-text-field v-model="myPlayer.gameId" label="游戏ID" prepend-inner-icon="sports_esports" />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field v-model="myPlayer.gameRank" label="段位/等级" prepend-inner-icon="star" />
              </v-col>
            </v-row>
            <v-textarea v-model="myPlayer.description" label="简介" prepend-inner-icon="text_fields" />
            <div class="d-flex justify-end">
              <v-btn :loading="playerSaving" color="primary" type="submit" prepend-icon="save">保存玩家</v-btn>
            </div>
          </v-form>
        </template>
        <template v-else>
          <v-alert type="info" text="请先绑定或创建战队，再创建玩家" />
          <div class="d-flex justify-end mt-2">
            <v-btn color="primary" variant="text" to="/teams/create" prepend-icon="group_add">创建战队</v-btn>
            <v-btn color="secondary" variant="text" to="/teams/edit" prepend-icon="edit">编辑战队</v-btn>
          </div>
        </template>
      </v-card-text>
    </v-card>
  </v-container>

  <v-dialog v-model="showPlayerPrompt" max-width="520">
    <v-card>
      <v-card-title class="text-h6">添加你的玩家</v-card-title>
      <v-card-text>
        你已绑定战队，但还没有“我的玩家”。现在去创建一个玩家并自动加入你的战队吗？
      </v-card-text>
      <v-card-actions class="justify-end">
        <v-btn variant="text" @click="showPlayerPrompt=false">稍后再说</v-btn>
        <v-btn color="primary" prepend-icon="person_add" @click="goToPlayerSection">去添加玩家</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
  
  
  
</template>

<style scoped>
</style>
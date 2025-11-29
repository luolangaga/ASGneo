<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { getProfile, updateProfile, uploadAvatar } from '../services/user'
import { updateCurrentUser, currentUser } from '../stores/auth'
import { getTeam, getTeamHonors, uploadTeamLogo, bindTeamByName, leaveTeam, getMyPlayer, upsertMyPlayer, generateInvite } from '../services/teams'
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
const inviteLoading = ref(false)
const inviteError = ref('')
const inviteDialog = ref(false)
const inviteDto = ref(null)
const inviteValidDays = ref(7)

// 我的玩家状态
const myPlayer = ref({ name: '', gameId: '', gameRank: '', description: '' })
const playerLoading = ref(false)
const playerSaving = ref(false)
const playerError = ref('')
const hasPlayer = ref(false)


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
    const tId = currentUser.value?.displayTeamId || currentUser.value?.DisplayTeamId || currentUser.value?.ownedTeamId || currentUser.value?.OwnedTeamId || currentUser.value?.teamId || currentUser.value?.TeamId
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
        hasPlayer.value = !!p
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
    const player = await getMyPlayer()
    const memberTeamId = player?.teamId || player?.TeamId || currentUser.value?.displayTeamId || currentUser.value?.DisplayTeamId
    if (!memberTeamId) { throw new Error('当前未加入任何战队') }
    await leaveTeam(memberTeamId)
    notify({ text: '已退出战队', color: 'success' })
    team.value = null
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
function playerTypeName(pt) { const v = Number(pt ?? 2); return v === 1 ? '监管者' : '求生者' }

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

function onCreatePlayerClick() {
  const tId = currentUser.value?.teamId || currentUser.value?.TeamId
  if (!tId) {
    showBind.value = true
    return
  }
}

async function onGenerateInvite() {
  try {
    const teamId = team.value?.id || team.value?.Id
    if (!teamId) return
    inviteLoading.value = true
    inviteError.value = ''
    const dto = await generateInvite(teamId, Number(inviteValidDays.value) || 7)
    inviteDto.value = dto
    inviteDialog.value = true
  } catch (err) {
    inviteError.value = err?.payload?.message || err?.message || '生成失败'
  } finally {
    inviteLoading.value = false
  }
}

async function copyInviteToken() {
  try {
    const t = inviteDto.value?.Token || inviteDto.value?.token
    if (!t) return
    await navigator.clipboard.writeText(String(t))
    notify({ text: '已复制Token', color: 'success' })
  } catch {
    try {
      const input = document.createElement('textarea')
      input.value = String(inviteDto.value?.Token || inviteDto.value?.token || '')
      document.body.appendChild(input)
      input.select()
      document.execCommand('copy')
      document.body.removeChild(input)
      notify({ text: '已复制Token', color: 'success' })
    } catch {}
  }
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
    <div class="mb-8">
      <div class="d-flex align-center mb-4">
        <v-icon icon="person" color="primary" class="mr-2" />
        <h3 class="text-h6 font-weight-bold">基本资料</h3>
      </div>
      
      <v-card variant="flat" border rounded="xl" class="pa-6">
        <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-6" variant="tonal" />

        <v-row>
          <v-col cols="12" md="4">
            <div class="d-flex flex-column align-center">
              <v-avatar size="120" class="mb-4" color="surface-variant" rounded="circle" elevation="2">
                <v-img :src="localPreview || avatarUrl" alt="avatar" cover>
                  <template #placeholder>
                    <div class="d-flex align-center justify-center" style="width:100%;height:100%">
                      <lottie-player src="/animations/loading.json" background="transparent" speed="1" loop autoplay style="width:96px;height:96px"></lottie-player>
                    </div>
                  </template>
                </v-img>
              </v-avatar>
              <v-file-input
                label="更换头像"
                accept="image/png, image/jpeg, image/jpg, image/webp"
                prepend-inner-icon="image"
                :loading="uploading"
                variant="outlined"
                density="compact"
                hide-details="auto"
                @update:modelValue="onFileChange"
              />
            </div>
          </v-col>
          <v-col cols="12" md="8">
            <v-form @submit.prevent="onSaveProfile">
              <v-row dense>
                <v-col cols="12">
                   <v-text-field v-model="email" label="邮箱" prepend-inner-icon="mail" readonly variant="outlined" density="comfortable" />
                </v-col>
                <v-col cols="12">
                   <div class="d-flex align-center">
                    <v-text-field :model-value="userId" label="用户ID" prepend-inner-icon="badge" readonly class="flex-grow-1" variant="outlined" density="comfortable" />
                    <v-btn variant="text" class="ml-2" icon="content_copy" @click="copyUserId" title="复制ID"></v-btn>
                  </div>
                </v-col>
                <v-col cols="12">
                  <v-text-field v-model="fullName" label="姓名" prepend-inner-icon="person" required variant="outlined" density="comfortable" />
                </v-col>
                <v-col cols="12">
                   <v-text-field v-model="roleName" label="角色" prepend-inner-icon="shield_person" readonly variant="outlined" density="comfortable" />
                </v-col>
              </v-row>
             
              <div class="d-flex align-center gap-2 mt-4">
                <v-chip color="primary" variant="tonal" prepend-icon="mail">邮件积分：{{ emailCredits }}</v-chip>
                <span class="text-caption text-medium-emphasis">用于邮件通知与提醒</span>
                <v-spacer />
                <v-btn :loading="saving" type="submit" color="primary" variant="flat" class="px-6">保存修改</v-btn>
              </div>
            </v-form>
          </v-col>
        </v-row>
      </v-card>
    </div>
  </v-container>
  
  <v-progress-linear v-if="loading" indeterminate color="primary" />
  
  <!-- 我的战队 -->
  <v-container class="py-6 narrow-container">
    <div class="mb-8">
      <div class="d-flex align-center mb-4">
        <v-icon icon="groups" color="primary" class="mr-2" />
        <h3 class="text-h6 font-weight-bold">我的战队</h3>
      </div>

      <template v-if="teamLoading">
        <v-skeleton-loader type="card" rounded="xl" />
      </template>
      
      <v-alert v-if="teamError" type="error" :text="teamError" class="mb-4" variant="tonal" />

      <v-card v-if="((currentUser?.displayTeamId || currentUser?.DisplayTeamId || currentUser?.ownedTeamId || currentUser?.OwnedTeamId || currentUser?.teamId || currentUser?.TeamId) && team)" variant="flat" border rounded="xl" class="pa-6">
          <v-row>
            <v-col cols="12" md="4">
              <div class="d-flex flex-column align-center">
                <v-avatar size="120" class="mb-4" rounded="lg" color="surface-variant">
                  <v-img v-if="team.logoUrl || team.LogoUrl" :src="team.logoUrl || team.LogoUrl" alt="team logo" cover>
                    <template #placeholder>
                      <div class="d-flex align-center justify-center" style="width:100%;height:100%">
                        <lottie-player src="/animations/loading.json" background="transparent" speed="1" loop autoplay style="width:96px;height:96px"></lottie-player>
                      </div>
                    </template>
                  </v-img>
                  <v-icon v-else icon="groups" size="48" color="medium-emphasis" />
                </v-avatar>
                <v-file-input
                  label="上传战队徽标"
                  accept="image/png, image/jpeg, image/jpg, image/webp"
                  prepend-inner-icon="image"
                  :loading="logoUploading"
                  variant="outlined"
                  density="compact"
                  hide-details="auto"
                  @update:modelValue="onTeamLogoChange"
                />
              </div>
            </v-col>
            <v-col cols="12" md="8">
              <div class="d-flex align-center mb-2">
                <div class="text-h5 font-weight-bold">{{ team.name || team.Name }}</div>
                <v-chip size="small" color="primary" class="ml-3" variant="tonal">ID: {{ team.id || team.Id }}</v-chip>
              </div>
              
              <div class="text-body-2 mb-4 md-content text-medium-emphasis" v-if="team.description || team.Description" v-html="toMd(team.description || team.Description)"></div>
              <div v-else class="text-body-2 mb-4 text-medium-emphasis font-italic">暂无简介</div>
              
              <div class="text-subtitle-2 mb-2 font-weight-bold">队员列表</div>
              <v-card variant="outlined" rounded="lg" class="mb-4">
              <v-list density="compact" lines="two" class="bg-transparent">
                <v-list-item v-for="p in (team.players || team.Players || [])" :key="p.id || p.Id || p.name || p.Name">
                    <template #prepend>
                      <v-avatar size="32" color="primary-lighten-4" class="mr-2">
                        <v-icon icon="person" color="primary" size="small" />
                      </v-avatar>
                    </template>
                    <v-list-item-title class="font-weight-medium">{{ p.name || p.Name }}</v-list-item-title>
                    <v-list-item-subtitle>
                      <span v-if="p.gameId || p.GameId">ID: {{ p.gameId || p.GameId }} </span>
                      <span v-if="p.gameRank || p.GameRank" class="ml-2">段位: {{ p.gameRank || p.GameRank }}</span>
                      <span v-if="(p.playerType ?? p.PlayerType) != null" class="ml-2">角色: {{ playerTypeName(p.playerType ?? p.PlayerType) }}</span>
                    </v-list-item-subtitle>
                  </v-list-item>
                </v-list>
              </v-card>

              <div class="text-subtitle-2 mb-2 font-weight-bold">战队荣誉</div>
              <template v-if="honorsLoading">
                <v-progress-linear indeterminate color="primary" rounded class="mb-2" />
              </template>
              <v-alert v-if="honorsError" type="error" :text="honorsError" class="mb-2" variant="tonal" />
              
              <template v-if="(honors || []).length">
                <v-list density="compact" class="bg-transparent mb-4">
                  <v-list-item v-for="e in honors" :key="e.id || e.Id" rounded="lg" class="mb-1">
                    <template #prepend>
                      <v-avatar size="32" v-if="e.logoUrl || e.LogoUrl" rounded>
                        <v-img :src="e.logoUrl || e.LogoUrl" alt="event logo" cover>
                          <template #placeholder>
                            <div class="d-flex align-center justify-center" style="width:100%;height:100%">
                              <lottie-player src="/animations/loading.json" background="transparent" speed="1" loop autoplay style="width:28px;height:28px"></lottie-player>
                            </div>
                          </template>
                        </v-img>
                      </v-avatar>
                      <v-icon v-else icon="emoji_events" color="warning" />
                    </template>
                    <v-list-item-title>{{ e.name || e.Name }}</v-list-item-title>
                    <v-list-item-subtitle class="text-warning font-weight-bold">冠军</v-list-item-subtitle>
                  </v-list-item>
                </v-list>
              </template>
              <template v-else-if="!honorsLoading && !honorsError">
                <div class="text-caption text-medium-emphasis mb-4 font-italic">暂无荣誉记录</div>
              </template>

              <div class="d-flex justify-end align-center flex-wrap gap-2 mt-4">
                <v-btn color="primary" variant="tonal" to="/teams/edit" prepend-icon="edit">编辑信息</v-btn>
                <v-btn v-if="(currentUser?.displayTeamId || currentUser?.DisplayTeamId)" color="error" variant="text" :loading="unbinding" prepend-icon="logout" @click="onUnbindTeam">退出战队</v-btn>
                <v-btn v-else color="error" variant="text" to="/teams/edit" prepend-icon="manage_accounts">管理战队</v-btn>
                <v-btn color="secondary" variant="flat" :loading="inviteLoading" prepend-icon="key" @click="onGenerateInvite">生成绑定Token</v-btn>
              </div>
            </v-col>
          </v-row>
      </v-card>
      
      <template v-else>
        <v-card variant="outlined" border rounded="xl" class="pa-8 text-center">
           <v-icon icon="group_off" size="64" color="medium-emphasis" class="mb-4" />
           <div class="text-h6 mb-2">你还没有战队</div>
           <div class="text-body-2 text-medium-emphasis mb-6" style="max-width: 400px; margin: 0 auto;">
             创建或绑定一个战队以报名赛事，与队友一起征战赛场。
           </div>
           
           <div class="d-flex justify-center gap-4 mb-6">
            <v-btn color="primary" to="/teams/create" prepend-icon="group_add" size="large" elevation="2">创建战队</v-btn>
            <v-btn color="secondary" variant="tonal" prepend-icon="link" @click="showBind = true" size="large">绑定战队</v-btn>
           </div>
           
           <v-expand-transition>
            <div v-if="showBind" class="text-left mx-auto" style="max-width: 400px;">
              <v-divider class="mb-6" />
              <div class="text-subtitle-1 font-weight-bold mb-4">绑定已有战队</div>
              <v-form @submit.prevent="onBindTeamByName">
                <v-text-field v-model="bindName" label="战队名称" prepend-inner-icon="group" required variant="outlined" density="comfortable" class="mb-2" />
                <v-text-field v-model="bindPassword" label="战队密码" type="password" prepend-inner-icon="lock" required variant="outlined" density="comfortable" class="mb-4" />
                <div class="d-flex align-center justify-end">
                  <v-btn variant="text" @click="showBind = false" class="mr-2">取消</v-btn>
                  <v-btn type="submit" color="secondary" :loading="binding" prepend-icon="link">确认绑定</v-btn>
                </div>
              </v-form>
              <v-alert v-if="bindError" type="error" :text="bindError" class="mt-4" variant="tonal" />
            </div>
          </v-expand-transition>
        </v-card>
      </template>
    </div>
  </v-container>

  <v-container class="py-6 narrow-container">
    <div class="mb-8">
      <div class="d-flex align-center mb-4">
        <v-icon icon="sports_esports" color="primary" class="mr-2" />
        <h3 class="text-h6 font-weight-bold">我的玩家信息</h3>
      </div>
      
      <v-card variant="flat" border rounded="xl" class="pa-6" ref="playerSectionEl">
        <v-alert v-if="playerError" type="error" :text="playerError" class="mb-4" variant="tonal" />
        
        <template v-if="(currentUser?.teamId || currentUser?.TeamId)">
          <v-alert v-if="!hasPlayer && !playerLoading" type="info" text="你还没有玩家档案，请填写下方信息创建玩家并自动加入战队。" class="mb-6" variant="tonal" border="start" icon="info" />
          
          <v-form @submit.prevent="onSavePlayer">
            <v-text-field v-model="myPlayer.name" label="玩家昵称" prepend-inner-icon="person" required variant="outlined" density="comfortable" />
            <v-row>
              <v-col cols="12" md="6">
                <v-text-field v-model="myPlayer.gameId" label="游戏ID" prepend-inner-icon="sports_esports" variant="outlined" density="comfortable" />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field v-model="myPlayer.gameRank" label="段位/等级" prepend-inner-icon="star" variant="outlined" density="comfortable" />
              </v-col>
            </v-row>
            <v-textarea v-model="myPlayer.description" label="简介" prepend-inner-icon="text_fields" variant="outlined" density="comfortable" rows="3" auto-grow />
            
            <div class="d-flex justify-end mt-4">
              <v-btn :loading="playerSaving" color="primary" type="submit" prepend-icon="save" variant="flat" class="px-6">保存玩家档案</v-btn>
            </div>
          </v-form>
        </template>
        <template v-else>
          <div class="text-center py-8">
             <v-icon icon="lock" color="medium-emphasis" size="48" class="mb-2" />
             <div class="text-body-1 text-medium-emphasis">请先绑定或创建战队，再管理玩家档案</div>
          </div>
        </template>
      </v-card>
    </div>
  </v-container>

  <v-dialog v-model="showPlayerPrompt" max-width="520">
    <v-card rounded="xl">
      <v-card-title class="text-h6 pa-4">
        <v-icon icon="person_add" color="primary" class="mr-2" />
        添加你的玩家
      </v-card-title>
      <v-card-text class="px-4 pb-2">
        你已绑定战队，但还没有“我的玩家”。现在去创建一个玩家并自动加入你的战队吗？
      </v-card-text>
      <v-card-actions class="justify-end pa-4">
        <v-btn variant="text" @click="showPlayerPrompt=false">稍后再说</v-btn>
        <v-btn color="primary" prepend-icon="arrow_forward" @click="goToPlayerSection" variant="flat">去添加玩家</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
  <v-dialog v-model="inviteDialog" max-width="640">
    <v-card rounded="xl">
      <v-card-title class="d-flex align-center pa-4">
        <v-icon icon="key" color="primary" class="mr-2" />
        <span class="text-h6">绑定 Token</span>
      </v-card-title>
      <v-card-text class="px-4">
        <v-alert v-if="inviteError" type="error" :text="inviteError" class="mb-4" variant="tonal" />
        
        <div class="d-flex align-center gap-4 mb-6">
          <v-text-field 
            v-model="inviteValidDays" 
            type="number" 
            label="有效期(天)" 
            variant="outlined" 
            density="compact" 
            hide-details
            style="max-width: 120px" 
          />
          <v-btn color="primary" variant="tonal" :loading="inviteLoading" @click="onGenerateInvite" prepend-icon="refresh">重新生成</v-btn>
        </div>
        
        <v-card variant="tonal" color="surface-variant" class="pa-4 mb-4 border-dashed">
          <div class="d-flex justify-space-between mb-2">
             <span class="text-medium-emphasis">战队</span>
             <span class="font-weight-bold">{{ inviteDto?.TeamName || inviteDto?.teamName || team?.name || team?.Name }}</span>
          </div>
           <div class="d-flex justify-space-between mb-2 align-center">
             <span class="text-medium-emphasis">Token</span>
             <div class="d-flex align-center">
                <code class="bg-surface pa-1 rounded mr-2">{{ inviteDto?.Token || inviteDto?.token || '---' }}</code>
                <v-btn size="small" variant="text" icon="content_copy" @click="copyInviteToken" title="复制"></v-btn>
             </div>
          </div>
          <div class="d-flex justify-space-between">
             <span class="text-medium-emphasis">过期时间</span>
             <span>{{ (inviteDto?.ExpiresAt || inviteDto?.expiresAt) ? new Date(inviteDto?.ExpiresAt || inviteDto?.expiresAt).toLocaleString() : '---' }}</span>
          </div>
        </v-card>

        <div class="text-body-2 text-medium-emphasis">
          <div class="mb-1">👉 在 QQ 群发送命令：<code class="bg-surface pa-1 rounded">绑定战队 [Token]</code></div>
          <div>⚠️ 绑定成功后，Token 将立即失效。</div>
        </div>
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer></v-spacer>
        <v-btn variant="text" @click="inviteDialog=false">关闭</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
</style>

<script setup>
import { ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { registerTeam, applyTeamLogoFromUrl, uploadTeamLogo } from '../services/teams'
import MarkdownEditor from '../components/MarkdownEditor.vue'
import ResultDialog from '../components/ResultDialog.vue'
import { extractErrorDetails } from '../services/api'
import { updateCurrentUser, currentUser } from '../stores/auth'
import { generateTeamLogo } from '../services/ai'

const router = useRouter()
const name = ref('')
const password = ref('')
const qq = ref('')
const description = ref('')
const players = ref([
  { name: '', gameId: '', gameRank: '', description: '' },
])
const saving = ref(false)
const errorMsg = ref('')
const successMsg = ref('')
const showSuccess = ref(false)
const errorOpen = ref(false)
const errorDetails = ref([])
const aiLogoUrl = ref('')
const generating = ref(false)
const logoFile = ref(null)
const createdTeamId = ref(null)

function addPlayer() {
  players.value.push({ name: '', gameId: '', gameRank: '', description: '' })
}

function removePlayer(index) {
  players.value.splice(index, 1)
}

async function onSubmit() {
  saving.value = true
  errorMsg.value = ''
  successMsg.value = ''
  try {
    const descLen = (description.value || '').length
    if (descLen >= 1000) {
      errorMsg.value = '战队描述字数需小于 1000'
      throw new Error('战队描述超长')
    }
    const qqVal = (qq.value || '').trim()
    if (!qqVal) {
      errorMsg.value = '请填写QQ号'
      throw new Error('QQ号必填')
    }
    const payload = {
      name: name.value.trim(),
      password: password.value,
      description: description.value?.trim() || null,
      qqNumber: qqVal,
      players: players.value.map(p => ({
        name: p.name.trim(),
        gameId: p.gameId?.trim() || null,
        gameRank: p.gameRank?.trim() || null,
        description: p.description?.trim() || null,
      })),
    }
    if (!aiLogoUrl.value && !logoFile.value) {
      errorMsg.value = '请上传队伍Logo或使用AI生成'
      throw new Error('缺少队伍Logo')
    }
    const team = await registerTeam(payload)
    updateCurrentUser({ ...(currentUser.value || {}), teamId: team.id })
    if (team?.id) {
      createdTeamId.value = team.id
      if (aiLogoUrl.value) {
        try { await applyTeamLogoFromUrl(team.id, aiLogoUrl.value) } catch {}
      } else if (logoFile.value) {
        try { await uploadTeamLogo(team.id, logoFile.value) } catch {}
      }
    }
    successMsg.value = '创建战队成功，正在跳转到编辑页面'
    showSuccess.value = true
    setTimeout(() => { router.push('/teams/edit') }, 1200)
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '创建战队失败'
    errorDetails.value = extractErrorDetails(err?.payload)
  } finally {
    saving.value = false
  }
}

async function onGenerateLogo() {
  errorMsg.value = ''
  const nm = (name.value || '').trim()
  const desc = (description.value || '').trim()
  if (!nm || !desc) { errorMsg.value = '请先填写战队名称与描述'; return }
  generating.value = true
  try {
    const r = await generateTeamLogo({ name: nm, description: desc })
    aiLogoUrl.value = r?.url || r?.Url || ''
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '生成徽标失败'
  } finally {
    generating.value = false
  }
}

function onLogoSelected(files) {
  errorMsg.value = ''
  const file = Array.isArray(files) ? files[0] : files
  if (!file) { logoFile.value = null; return }
  const okTypes = ['image/png', 'image/jpeg', 'image/jpg', 'image/webp']
  if (!okTypes.includes(file.type)) { errorMsg.value = '文件类型不支持'; logoFile.value = null; return }
  if (file.size > 5 * 1024 * 1024) { errorMsg.value = '文件过大，最大5MB'; logoFile.value = null; return }
  logoFile.value = file
}

function onGoInvite() {
  if (!createdTeamId.value) {
    errorMsg.value = '请先在上方提交创建战队，再生成邀请链接'
    return
  }
  router.push('/teams/edit')
}

watch(errorMsg, (v) => { if (v) errorOpen.value = true })
</script>

<template>
  <v-container class="py-8" style="max-width: 860px">
    <v-card>
      <v-card-title>创建战队</v-card-title>
      <v-card-text>
        <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
        <v-alert v-if="successMsg && !showSuccess" type="success" :text="successMsg" class="mb-4" />
        <v-form @submit.prevent="onSubmit">
          <v-text-field v-model="name" label="战队名称" prepend-inner-icon="group" required />
          <v-text-field v-model="password" label="战队密码" type="password" prepend-inner-icon="lock" required />
          <v-text-field v-model="qq" label="QQ号" prepend-inner-icon="chat" required />
          <MarkdownEditor v-model="description" label="战队描述" :rows="6" :maxLength="999" />
          <div class="d-flex align-center gap-3 mt-2">
            <v-btn :loading="generating" variant="tonal" color="primary" prepend-icon="auto_awesome" @click="onGenerateLogo">AI生成徽标</v-btn>
            <v-avatar v-if="aiLogoUrl" size="40"><v-img :src="aiLogoUrl" cover /></v-avatar>
          </div>
          <div class="mt-2">
            <v-file-input
              prepend-icon="add_photo_alternate"
              density="comfortable"
              accept="image/png, image/jpeg, image/jpg, image/webp"
              show-size
              @update:modelValue="onLogoSelected"
              label="或上传队伍Logo"
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
            <v-btn :loading="saving" color="primary" type="submit" prepend-icon="save">创建战队</v-btn>
          </div>
        </v-form>
        <v-divider class="my-4" />
        <div class="d-flex align-center justify-space-between mb-2">
          <div class="text-subtitle-1">邀请队员加入</div>
          <v-btn color="secondary" variant="tonal" @click="onGoInvite" prepend-icon="link">生成邀请链接</v-btn>
        </div>
        <div class="text-caption text-medium-emphasis">提示：必须先提交并创建战队，随后将跳转到“编辑战队”页面进行邀请链接生成。</div>
      </v-card-text>
    </v-card>
    <ResultDialog v-model="showSuccess" :type="'success'" :message="successMsg" :autoCloseMs="2800" />
    <ResultDialog v-model="errorOpen" :type="'error'" :message="errorMsg" :details="errorDetails" />
  </v-container>
</template>

<style scoped>
</style>

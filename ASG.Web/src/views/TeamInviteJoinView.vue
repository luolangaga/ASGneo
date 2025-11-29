<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import PageHero from '../components/PageHero.vue'
import { isAuthenticated, updateCurrentUser } from '../stores/auth'
import { getProfile } from '../services/user'
import { getInvite, acceptInvite, getMyPlayer } from '../services/teams'
import ResultDialog from '../components/ResultDialog.vue'
import { extractErrorDetails } from '../services/api'

const route = useRoute()
const router = useRouter()
const token = computed(() => route.params.token)

const loading = ref(false)
const errorMsg = ref('')
const successMsg = ref('')
const showSuccess = ref(false)
const errorOpen = ref(false)
const errorDetails = ref([])
const invite = ref(null)
const joining = ref(false)
const hasMyPlayer = ref(false)
const myPlayer = ref(null)
const useExisting = ref(true)

const player = ref({ name: '', gameId: '', gameRank: '', description: '' })


onMounted(async () => {
  loading.value = true
  errorMsg.value = ''
  try {
    const info = await getInvite(token.value)
    invite.value = info
    if (isAuthenticated.value) {
      try {
        const p = await getMyPlayer()
        if (p && p.id) {
          hasMyPlayer.value = true
          myPlayer.value = p
          useExisting.value = true
        }
      } catch {}
    }
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '邀请信息加载失败或已过期'
  } finally {
    loading.value = false
  }
})

async function onJoin() {
  if (!isAuthenticated.value) {
    errorMsg.value = '请先登录再加入战队'
    return
  }
  if (!hasMyPlayer.value) {
    if (!player.value.name.trim()) {
      errorMsg.value = '请填写玩家昵称'
      return
    }
  }
  joining.value = true
  errorMsg.value = ''
  successMsg.value = ''
  try {
    const res = await acceptInvite(token.value, hasMyPlayer.value && useExisting.value ? null : player.value)
    successMsg.value = '加入成功，已将你的玩家添加到战队'
    try {
      const profile = await getProfile()
      updateCurrentUser(profile)
    } catch {}
    const tid = invite.value?.teamId || invite.value?.TeamId || res?.teamId || res?.TeamId
    setTimeout(() => router.push(tid ? `/teams/${tid}` : '/teams/edit'), 800)
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加入失败'
    errorDetails.value = extractErrorDetails(err?.payload)
  } finally {
    joining.value = false
  }
}

function chooseUseExisting(val) {
  useExisting.value = !!val
}

watch(successMsg, (v) => { if (v) showSuccess.value = true })
watch(errorMsg, (v) => { if (v) errorOpen.value = true })
</script>

<template>
  <PageHero title="加入战队" subtitle="通过邀请链接加入战队" icon="group_add">
    <template #actions>
      <v-btn variant="text" to="/events" prepend-icon="grid_view">查看赛事</v-btn>
    </template>
  </PageHero>
  <v-container class="py-6" style="max-width: 800px">
    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
    <template v-if="loading">
      <v-skeleton-loader type="article" />
    </template>
    <template v-else>
      <template v-if="invite">
        <v-card class="mb-4">
          <v-card-title>受邀战队：{{ invite.teamName || invite.TeamName }}</v-card-title>
          <v-card-text>
            <div>邀请有效期至：{{ new Date(invite.expiresAt || invite.ExpiresAt).toLocaleString() }}</div>
          </v-card-text>
        </v-card>
        <v-card>
          <v-card-title>我的玩家信息</v-card-title>
          <v-card-text>
            <template v-if="hasMyPlayer">
              <v-alert type="info" class="mb-3">
                检测到你的玩家：{{ myPlayer?.name || myPlayer?.Name }}
              </v-alert>
              <div class="d-flex gap-2 mb-3">
                <v-btn color="primary" :loading="joining" prepend-icon="group_add" @click="chooseUseExisting(true); onJoin()">直接用我的玩家加入</v-btn>
                <v-btn color="secondary" variant="tonal" prepend-icon="edit" @click="chooseUseExisting(false)">改用新玩家填写</v-btn>
              </div>
            </template>

            <v-form v-if="!hasMyPlayer || !useExisting" @submit.prevent="onJoin">
              <v-text-field v-model="player.name" label="玩家昵称" prepend-inner-icon="person" required />
              <v-row>
                <v-col cols="12" md="6">
                  <v-text-field v-model="player.gameId" label="游戏ID" prepend-inner-icon="sports_esports" />
                </v-col>
                <v-col cols="12" md="6">
                  <v-text-field v-model="player.gameRank" label="段位/等级" prepend-inner-icon="star" />
                </v-col>
              </v-row>
              <v-textarea v-model="player.description" label="简介" prepend-inner-icon="text_fields" />
              <div class="d-flex justify-end">
                <v-btn color="primary" :loading="joining" type="submit" prepend-icon="group_add">加入战队</v-btn>
              </div>
            </v-form>
          </v-card-text>
        </v-card>
      </template>
      <template v-else>
        <v-alert type="warning" text="邀请无效或已过期" />
      </template>
    </template>
  </v-container>
  <ResultDialog v-model="showSuccess" :type="'success'" :message="successMsg" />
  <ResultDialog v-model="errorOpen" :type="'error'" :message="errorMsg" :details="errorDetails" />
</template>

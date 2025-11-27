<template>
  <PageHero title="战队详情" subtitle="查看战队信息、队员与荣誉" icon="group">
    <template #actions>
      <v-btn variant="text" class="mb-3" to="/teams/search" prepend-icon="search">返回搜索</v-btn>
    </template>
  </PageHero>
  <v-container class="py-6" style="max-width: 960px">
    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />

    <template v-if="loading">
      <v-skeleton-loader type="article" />
    </template>

    <template v-else>
      <v-card class="mb-4">
        <v-card-title class="d-flex align-center">
          <v-avatar size="64" class="mr-3" color="primary" variant="tonal">
            <template v-if="team.logoUrl">
              <v-img :src="team.logoUrl" cover>
                <template #placeholder>
                  <div class="d-flex align-center justify-center" style="width:100%;height:100%">
                    <lottie-player src="/animations/loading.json" background="transparent" speed="1" loop autoplay style="width:64px;height:64px"></lottie-player>
                  </div>
                </template>
              </v-img>
            </template>
            <template v-else>
              <span class="text-h6">{{ avatarLetter }}</span>
            </template>
          </v-avatar>
          <div class="flex-grow-1">
            <div class="d-flex align-center">
              <div class="text-h6 mr-3">{{ team.name || '未命名战队' }}</div>
              <v-chip v-if="team.hasDispute || team.HasDispute" color="error" variant="tonal" size="small" prepend-icon="report">存在纠纷</v-chip>
            </div>
            <div class="text-medium-emphasis d-flex align-center gap-4">
              <span>👍 {{ team.likes ?? 0 }}</span>
              
            </div>
          </div>
          <div class="like-anim-container mr-2">
            <v-btn
              color="secondary"
              variant="tonal"
              :loading="likeLoading"
              @click="doLike"
              prepend-icon="thumb_up_off_alt"
            >点赞</v-btn>
            <lottie-player v-if="likeFx" class="like-anim" src="/animations/Love_Animation_with_Particle.json" background="transparent" speed="1" loop autoplay style="width:180px;height:180px"></lottie-player>
          </div>
          <v-btn
            v-if="ownerUserId"
            class="mr-2"
            variant="text"
            color="secondary"
            :to="`/messages/${ownerUserId}`"
            prepend-icon="chat"
          >联系队长</v-btn>
          <v-btn variant="text" color="primary" @click="goEdit" prepend-icon="edit">编辑我的战队</v-btn>
        </v-card-title>
        <v-card-text>
          <div v-if="team.description" class="md-content" v-html="toMd(team.description)"></div>
          <v-alert v-else type="info" variant="tonal" text="暂无战队描述" />
        </v-card-text>
      </v-card>

      <v-card class="mb-4" v-if="team.hasDispute || team.HasDispute">
        <v-card-title>纠纷详情</v-card-title>
        <v-card-text>
          <div v-if="team.disputeDetail || team.DisputeDetail" class="md-content" v-html="toMd(team.disputeDetail || team.DisputeDetail)" />
          <div v-else class="text-medium-emphasis">主办方暂未填写纠纷说明</div>
          <div class="mt-2" v-if="team.communityPostId || team.CommunityPostId">
            <router-link :to="'/articles/' + (team.communityPostId || team.CommunityPostId)" class="text-decoration-none">
              关联社区帖子：{{ team.communityPostId || team.CommunityPostId }}
            </router-link>
          </div>
        </v-card-text>
      </v-card>

      

      <v-card class="mb-4">
        <v-card-title>队员</v-card-title>
        <v-card-text>
          <v-alert v-if="team.hidePlayers || team.HidePlayers" type="warning" variant="tonal" text="队长已设置队员信息对外不可见（仅主办方/赛事管理员/管理员可见）" class="mb-3" />
          <v-list v-if="filteredPlayers?.length" density="comfortable">
            <v-list-item v-for="p in filteredPlayers" :key="p.id || p.Id">
              <v-list-item-title>{{ p.name }}</v-list-item-title>
              <v-list-item-subtitle>
                <span v-if="p.gameId">ID：{{ p.gameId }}</span>
                <span v-if="p.gameRank" class="ml-2">段位：{{ p.gameRank }}</span>
                <span v-if="(p.playerType ?? p.PlayerType) != null" class="ml-2">角色类型：{{ playerTypeName(p.playerType ?? p.PlayerType) }}</span>
              </v-list-item-subtitle>
              <template #append>
                <v-btn
                  v-if="getPlayerUserId(p)"
                  variant="text"
                  color="secondary"
                  :to="`/messages/${getPlayerUserId(p)}`"
                  prepend-icon="chat"
                >联系队员</v-btn>
              </template>
            </v-list-item>
          </v-list>
          <v-alert v-else-if="team.hidePlayers || team.HidePlayers" type="warning" variant="tonal" text="队员信息已隐藏" />
          <v-alert v-else type="info" variant="tonal" text="暂无队员信息" />
        </v-card-text>
      </v-card>

      <v-card>
        <v-card-title>战队荣誉</v-card-title>
        <v-card-text>
          <template v-if="honorsLoading">
            <v-progress-linear indeterminate color="primary" />
          </template>
          <v-alert v-if="honorsError" type="error" :text="honorsError" class="mb-2" />
          <template v-if="honors?.length">
            <v-list density="comfortable">
              <v-list-item v-for="e in honors" :key="e.id || e.Id">
                <template #prepend>
                  <v-avatar size="34" v-if="e.logoUrl || e.LogoUrl">
                    <v-img :src="e.logoUrl || e.LogoUrl" cover>
                      <template #placeholder>
                        <div class="d-flex align-center justify-center" style="width:100%;height:100%">
                          <lottie-player src="/animations/loading.json" background="transparent" speed="1" loop autoplay style="width:64px;height:64px"></lottie-player>
                        </div>
                      </template>
                    </v-img>
                  </v-avatar>
                </template>
                <v-list-item-title>{{ e.name || e.Name }}</v-list-item-title>
                <v-list-item-subtitle>冠军</v-list-item-subtitle>
              </v-list-item>
            </v-list>
          </template>
          <v-alert v-else-if="!honorsLoading && !honorsError" type="info" variant="tonal" text="暂无荣誉" />
        </v-card-text>
      </v-card>
    </template>
  </v-container>
  
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import PageHero from '../components/PageHero.vue'
import { getTeam, getTeamHonors, likeTeam } from '../services/teams'
import { getRegistrationAnswers } from '../services/events'
import { renderMarkdown } from '../utils/markdown'

const route = useRoute()
const router = useRouter()

const teamId = computed(() => String(route.params.id || '').trim())
const eventIdQuery = computed(() => String(route.query.eventId || '').trim())
const loading = ref(false)
const errorMsg = ref('')
const team = ref({})

const honorsLoading = ref(false)
const honorsError = ref('')
const honors = ref([])
const likeLoading = ref(false)
const likeFx = ref(false)
 

const avatarLetter = computed(() => (team.value?.name || '').trim().charAt(0).toUpperCase() || '?')
function toMd(s) { return renderMarkdown(s || '') }
const ownerUserId = computed(() => (team.value?.ownerId || team.value?.OwnerId || team.value?.userId || team.value?.UserId || ''))
function getPlayerUserId(p) { return p?.userId || p?.UserId || '' }
function playerTypeName(pt) { const v = Number(pt ?? 2); return v === 1 ? '监管者' : '求生者' }
const selectedPlayerIds = ref([])
const selectedIdSet = computed(() => new Set(selectedPlayerIds.value || []))
const filteredPlayers = computed(() => {
  const all = team.value?.players || []
  if (!eventIdQuery.value) return all
  const set = selectedIdSet.value
  return set.size > 0 ? all.filter(p => set.has(String(p.id || p.Id))) : all
})

async function loadAll() {
  errorMsg.value = ''
  if (!teamId.value) {
    errorMsg.value = '无效的战队ID'
    return
  }
  loading.value = true
  try {
    team.value = await getTeam(teamId.value)
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '获取战队详情失败'
  } finally {
    loading.value = false
  }
  try {
    if (eventIdQuery.value) {
      const res = await getRegistrationAnswers(eventIdQuery.value, teamId.value)
      let obj = {}
      try { obj = typeof res === 'string' ? JSON.parse(res) : (res || {}) } catch { obj = {} }
      const ids = obj.selectedPlayerIds || obj.SelectedPlayerIds || []
      selectedPlayerIds.value = Array.isArray(ids) ? ids.map(x => String(x)) : []
    } else {
      selectedPlayerIds.value = []
    }
  } catch { selectedPlayerIds.value = [] }
  
  // honors
  try {
    honorsLoading.value = true
    honorsError.value = ''
    honors.value = await getTeamHonors(teamId.value)
  } catch (e2) {
    honorsError.value = e2?.payload?.message || e2?.message || '获取战队荣誉失败'
  } finally {
    honorsLoading.value = false
  }
  
}

function goEdit() { router.push('/teams/edit') }

async function doLike() {
  if (!teamId.value) return
  likeLoading.value = true
  try {
    const res = await likeTeam(teamId.value)
    const newLikes = res?.likes ?? res?.Likes
    if (typeof newLikes === 'number') {
      team.value = { ...team.value, likes: newLikes }
    }
    likeFx.value = true
    setTimeout(() => { likeFx.value = false }, 3200)
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || '点赞失败'
  } finally {
    likeLoading.value = false
  }
}

onMounted(loadAll)
</script>

<style scoped>
.md-content :deep(img) { max-width: 100%; }
.md-content :deep(pre) { white-space: pre-wrap; }
.like-anim-container { position: relative; display: inline-block }
.like-anim { position: absolute; left: 50%; transform: translateX(-50%); top: -130px; pointer-events: none; z-index: 2; will-change: opacity, transform; animation: hoverPulse 3.2s ease-out forwards }
@keyframes hoverPulse {
  0% { opacity: 0; transform: translate(-50%, -10px) scale(0.92) }
  12% { opacity: 1; transform: translate(-50%, 0) scale(1) }
  35% { transform: translate(-50%, -6px) }
  68% { transform: translate(-50%, -3px) }
  92% { transform: translate(-50%, -5px) }
  100% { opacity: 0; transform: translate(-50%, -5px) }
}
</style>

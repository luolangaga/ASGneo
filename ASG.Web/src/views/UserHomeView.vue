<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import PageHero from '../components/PageHero.vue'
import { getUser } from '../services/user'
import { getTeam, getTeamHonors } from '../services/teams'
import { renderMarkdown } from '../utils/markdown'

const route = useRoute()
const router = useRouter()
const userId = computed(() => route.params.id)

const loading = ref(false)
const errorMsg = ref('')
const user = ref(null)

const team = ref(null)
const teamLoading = ref(false)
const teamError = ref('')
const honors = ref([])
const honorsLoading = ref(false)
const honorsError = ref('')

const heroTitle = computed(() => {
  const name = user.value?.fullName || user.value?.FullName || ''
  return name ? `${name} 的主页` : '用户主页'
})

function toMd(s) { return renderMarkdown(s || '') }

async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    const u = await getUser(userId.value)
    user.value = u
    const tId = u?.teamId || u?.TeamId
    if (tId) {
      teamLoading.value = true
      teamError.value = ''
      try {
        team.value = await getTeam(tId)
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
    }
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载用户失败'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <PageHero :title="heroTitle" subtitle="查看TA的基本信息与战队" icon="person">
    <template #actions>
      <v-btn variant="text" class="mb-3" to="/articles" prepend-icon="article">返回文章</v-btn>
      <v-btn variant="text" class="mb-3" to="/" prepend-icon="home">返回首页</v-btn>
      <v-btn
        variant="text"
        class="mb-3"
        color="secondary"
        :to="`/messages/${userId}`"
        prepend-icon="chat"
      >联系TA</v-btn>
    </template>
  </PageHero>

  <v-container class="py-8" style="max-width: 900px">
    <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />

    <v-card class="mb-6">
      <v-card-title>用户信息</v-card-title>
      <v-card-text>
        <div class="d-flex align-center">
          <v-avatar size="96" class="mr-4">
            <template v-if="user?.avatarUrl || user?.AvatarUrl">
              <v-img :src="user?.avatarUrl || user?.AvatarUrl" alt="avatar" cover />
            </template>
            <template v-else>
              <span class="text-body-2">{{ (user?.fullName || user?.FullName || '')?.slice(0, 2) || 'NA' }}</span>
            </template>
          </v-avatar>
          <div>
            <div class="text-subtitle-1 font-weight-medium">{{ user?.fullName || user?.FullName || user?.email || '' }}</div>
            <div class="text-caption text-medium-emphasis">邮箱：{{ user?.email || user?.Email }}</div>
            <div class="text-caption text-medium-emphasis">角色：{{ user?.roleDisplayName || user?.roleName || user?.RoleName || '用户' }}</div>
          </div>
        </div>
      </v-card-text>
    </v-card>

    <v-card>
      <v-card-title>TA的战队</v-card-title>
      <v-card-text>
        <template v-if="teamLoading">
          <v-progress-linear indeterminate color="primary" />
        </template>
        <v-alert v-if="teamError" type="error" :text="teamError" class="mb-3" />

        <template v-if="(user?.teamId || user?.TeamId) && team">
          <v-row>
            <v-col cols="12" md="4">
              <div class="d-flex flex-column align-center">
                <v-avatar size="120" class="mb-3" v-if="team.logoUrl || team.LogoUrl">
                  <v-img :src="team.logoUrl || team.LogoUrl" alt="team logo" cover />
                </v-avatar>
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
                        <v-img :src="e.logoUrl || e.LogoUrl" alt="event logo" cover />
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
            </v-col>
          </v-row>
        </template>
        <template v-else>
          <v-alert type="info" text="该用户暂无战队" />
        </template>
      </v-card-text>
    </v-card>
  </v-container>
</template>

<style scoped>
.md-content { max-width: 960px; }
</style>
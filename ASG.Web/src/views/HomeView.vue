<script setup>
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { isAuthenticated } from '../stores/auth'

const router = useRouter()
const loggedIn = computed(() => isAuthenticated.value)

function goCreateEvent() {
  if (!loggedIn.value) {
    router.push('/login')
    return
  }
  router.push('/events/create')
}

function goCreateTeam() {
  if (!loggedIn.value) {
    router.push('/login')
    return
  }
  router.push('/teams/create')
}
</script>

<template>
  <v-container class="py-0">
    <!-- Hero -->
    <v-sheet class="hero pa-12">
      <v-container>
        <v-row align="center">
          <v-col cols="12" md="7">
            <div class="text-h4 text-md-h3 font-weight-bold mb-3">第五人格赛事统一管理平台</div>
            <div class="text-subtitle-1 text-medium-emphasis mb-6">
              面向所有主办方的赛事创建、报名审核、赛程管理一体化平台。
            </div>
            <div class="d-flex flex-wrap">
              <v-btn color="primary" class="mr-3 mb-3" variant="elevated" @click="goCreateEvent" prepend-icon="add">创建赛事</v-btn>
              <v-btn color="tertiary" class="mr-3 mb-3" to="/events" variant="tonal" prepend-icon="grid_view">浏览赛事</v-btn>
              <v-btn class="mr-3 mb-3" @click="goCreateTeam" color="tertiary" variant="tonal" prepend-icon="person_add">创建战队</v-btn>
              <v-btn class="mb-3" to="/teams/search" variant="text" prepend-icon="search">搜索战队</v-btn>
            </div>
            <div class="text-caption mt-2 text-medium-emphasis">
              主办方登录后可创建并管理赛事；参赛者可创建/绑定战队并报名。
            </div>
          </v-col>
          <v-col cols="12" md="5" class="d-none d-md-flex justify-center">
            <v-img src="/logo.svg" alt="平台Logo" width="160" class="hero-logo" />
          </v-col>
        </v-row>
      </v-container>
    </v-sheet>

    <!-- Features for Organizers -->
    <v-container class="py-10">
      <v-row class="mb-6">
        <v-col cols="12" class="text-center">
          <div class="text-h5 font-weight-bold mb-2">为主办方而生的关键能力</div>
          <div class="text-medium-emphasis">覆盖赛事全流程，降低运营成本，提升参赛体验</div>
        </v-col>
      </v-row>
      <v-row>
        <v-col cols="12" md="4">
          <v-card class="feature-card" variant="tonal">
            <v-card-item>
              <div class="d-flex align-center">
                <v-icon color="primary" icon="settings" class="mr-2" />
                <div class="text-subtitle-1 font-weight-medium">创建与配置赛事</div>
              </div>
            </v-card-item>
            <v-card-text class="text-body-2">
              支持报名时间、比赛时间、队伍上限、赛事说明等核心配置，快速发布赛事。
            </v-card-text>
          </v-card>
        </v-col>
        <v-col cols="12" md="4">
          <v-card class="feature-card" variant="tonal">
            <v-card-item>
              <div class="d-flex align-center">
                <v-icon color="primary" icon="how_to_reg" class="mr-2" />
                <div class="text-subtitle-1 font-weight-medium">报名与审核</div>
              </div>
            </v-card-item>
            <v-card-text class="text-body-2">
              队伍在线报名，管理员审核与状态管理，确保参赛资格规范透明。
            </v-card-text>
          </v-card>
        </v-col>
        <v-col cols="12" md="4">
          <v-card class="feature-card" variant="tonal">
            <v-card-item>
              <div class="d-flex align-center">
                <v-icon color="primary" icon="calendar_month" class="mr-2" />
                <div class="text-subtitle-1 font-weight-medium">赛程与沟通</div>
              </div>
            </v-card-item>
            <v-card-text class="text-body-2">
              管理赛程安排、队伍信息与通知发布，提升组织效率与参赛体验。
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>

      <v-divider class="my-10" />

      <!-- How it works -->
      <v-row class="mb-4">
        <v-col cols="12" class="text-center">
          <div class="text-h5 font-weight-bold mb-2">创建赛事 · 四步走</div>
        </v-col>
      </v-row>
      <v-row>
        <v-col cols="12" md="3">
          <v-card variant="outlined">
            <v-card-item>
              <div class="text-subtitle-2 text-medium-emphasis">Step 1</div>
              <div class="text-subtitle-1 font-weight-medium">登录平台</div>
            </v-card-item>
            <v-card-text class="text-body-2">使用主办方账号登录平台。</v-card-text>
            <v-card-actions>
              <v-btn to="/login" variant="text" prepend-icon="login">去登录</v-btn>
            </v-card-actions>
          </v-card>
        </v-col>
        <v-col cols="12" md="3">
          <v-card variant="outlined">
            <v-card-item>
              <div class="text-subtitle-2 text-medium-emphasis">Step 2</div>
              <div class="text-subtitle-1 font-weight-medium">创建赛事</div>
            </v-card-item>
            <v-card-text class="text-body-2">填写赛事信息并发布。</v-card-text>
            <v-card-actions>
              <v-btn @click="goCreateEvent" variant="text" prepend-icon="add">创建赛事</v-btn>
            </v-card-actions>
          </v-card>
        </v-col>
        <v-col cols="12" md="3">
          <v-card variant="outlined">
            <v-card-item>
              <div class="text-subtitle-2 text-medium-emphasis">Step 3</div>
              <div class="text-subtitle-1 font-weight-medium">接受报名</div>
            </v-card-item>
            <v-card-text class="text-body-2">队伍提交报名，你进行审核。</v-card-text>
            <v-card-actions>
              <v-btn to="/events/manage" variant="text" prepend-icon="settings">管理赛事</v-btn>
            </v-card-actions>
          </v-card>
        </v-col>
        <v-col cols="12" md="3">
          <v-card variant="outlined">
            <v-card-item>
              <div class="text-subtitle-2 text-medium-emphasis">Step 4</div>
              <div class="text-subtitle-1 font-weight-medium">发布赛程</div>
            </v-card-item>
            <v-card-text class="text-body-2">维护比赛安排与通知。</v-card-text>
            <v-card-actions>
              <v-btn to="/events" variant="text" prepend-icon="grid_view">查看赛事</v-btn>
            </v-card-actions>
          </v-card>
        </v-col>
      </v-row>

      <!-- Participant Tutorial -->
      <v-row class="mb-4 mt-6">
        <v-col cols="12" class="text-center">
          <div class="text-h5 font-weight-bold mb-2">参赛者新手教程 · 四步走</div>
          <div class="text-medium-emphasis">按流程完成战队准备与赛事报名</div>
        </v-col>
      </v-row>
      <v-row>
        <v-col cols="12" md="3">
          <v-card variant="outlined">
            <v-card-item>
              <div class="text-subtitle-2 text-medium-emphasis">Step 1</div>
              <div class="text-subtitle-1 font-weight-medium">登录/注册平台</div>
            </v-card-item>
            <v-card-text class="text-body-2">使用个人账号登录或完成注册。</v-card-text>
            <v-card-actions>
              <v-btn to="/login" variant="text" prepend-icon="login">去登录</v-btn>
              <v-btn to="/register" variant="text" prepend-icon="person_add">去注册</v-btn>
            </v-card-actions>
          </v-card>
        </v-col>
        <v-col cols="12" md="3">
          <v-card variant="outlined">
            <v-card-item>
              <div class="text-subtitle-2 text-medium-emphasis">Step 2</div>
              <div class="text-subtitle-1 font-weight-medium">创建或绑定战队</div>
            </v-card-item>
            <v-card-text class="text-body-2">创建自己的战队或绑定已存在战队，完善队员信息。</v-card-text>
            <v-card-actions>
              <v-btn @click="goCreateTeam" variant="text" prepend-icon="person_add">创建战队</v-btn>
              <v-btn to="/teams/search" variant="text" prepend-icon="search">绑定战队</v-btn>
            </v-card-actions>
          </v-card>
        </v-col>
        <v-col cols="12" md="3">
          <v-card variant="outlined">
            <v-card-item>
              <div class="text-subtitle-2 text-medium-emphasis">Step 3</div>
              <div class="text-subtitle-1 font-weight-medium">浏览赛事并查看详情</div>
            </v-card-item>
            <v-card-text class="text-body-2">在报名开放期间挑选适合的赛事，查看参赛要求与说明。</v-card-text>
            <v-card-actions>
              <v-btn to="/events" variant="text" prepend-icon="grid_view">浏览赛事</v-btn>
            </v-card-actions>
          </v-card>
        </v-col>
        <v-col cols="12" md="3">
          <v-card variant="outlined">
            <v-card-item>
              <div class="text-subtitle-2 text-medium-emphasis">Step 4</div>
              <div class="text-subtitle-1 font-weight-medium">报名参赛并关注审核</div>
            </v-card-item>
            <v-card-text class="text-body-2">提交报名后留意审核结果与通知，按赛程参加比赛。</v-card-text>
            <v-card-actions>
              <v-btn to="/events" variant="text" prepend-icon="check_circle">前往报名</v-btn>
            </v-card-actions>
          </v-card>
        </v-col>
      </v-row>

      <v-divider class="my-10" />

      <!-- Participants and Vision -->
      <v-row>
        <v-col cols="12" md="6">
          <v-card variant="tonal">
            <v-card-title>参赛队伍</v-card-title>
            <v-card-text class="text-body-2">
              参赛者可创建或绑定自己的战队，完善队员信息，并在报名开放期间一键报名合适赛事。
            </v-card-text>
            <v-card-actions>
              <v-btn @click="goCreateTeam" prepend-icon="person_add" color="tertiary" variant="tonal">创建战队</v-btn>
              <v-btn to="/teams/search" prepend-icon="search" variant="text">搜索战队</v-btn>
            </v-card-actions>
          </v-card>
        </v-col>
        <v-col cols="12" md="6">
          <v-card variant="tonal">
            <v-card-title>平台愿景</v-card-title>
            <v-card-text class="text-body-2">
              聚合全国第五人格赛事，降低组织成本，提升公平与效率，让每一场比赛更简单、更精彩。
            </v-card-text>
            <!-- 了解更多入口移除（无关于页面） -->
          </v-card>
        </v-col>
      </v-row>
    </v-container>
  </v-container>
</template>

<style scoped>
.hero {
  /* 与新主色协调的更柔和靛蓝渐变 */
  background: linear-gradient(135deg, #4F6BED 0%, #1F2A5C 100%);
  color: #fff;
}
.hero .text-medium-emphasis { color: rgba(255, 255, 255, 0.85) !important; }
.hero .text-caption { color: rgba(255, 255, 255, 0.75) !important; }
.hero .text-h4,
.hero .text-md-h3,
.hero .text-subtitle-1 { color: #ffffff !important; }
.hero-icon {
  opacity: 0.9;
}
.hero-logo {
  opacity: 0.95;
  filter: drop-shadow(0 4px 12px rgba(0,0,0,0.25));
}
.feature-card {
  height: 100%;
}
</style>

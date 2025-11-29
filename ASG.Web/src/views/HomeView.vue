<script setup>
import { computed, ref } from 'vue'
import { useDisplay } from 'vuetify'
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
const featureCards = [
  {
    icon: 'settings',
    title: '创建与配置赛事',
    text: '支持报名时间、比赛时间、队伍上限、赛事说明等核心配置，快速发布赛事。'
  },
  {
    icon: 'how_to_reg',
    title: '报名与审核',
    text: '队伍在线报名，管理员审核与状态管理，确保参赛资格规范透明。'
  },
  {
    icon: 'calendar_month',
    title: '赛程与沟通',
    text: '管理赛程安排、队伍信息与通知发布，提升组织效率与参赛体验。'
  }
]

// 合作伙伴（从关于页面迁移）
const partners = [
   {
    logo: 'https://www.swun.edu.cn/images/logonew.png',
    name: '西南民族大学大创',
    description: '本项目是西南民族大学的一个大创项目',
    link: 'https://www.swun.edu.cn/'
  },
  {
    logo: 'https://bpsys.plfjy.top/icon.png',
    name: 'neo-bpsys-wpf',
    description: '基于 .NET WPF 开发的第五人格直播BP软件，与其共同构成第五人格赛事系统大家庭',
    link: 'https://bpsys.plfjy.top/'
  },
  {
    logo: 'https://idvasg.cn/_nuxt/asg-logo.2xBiWjrM.png',
    name: 'ASG赛事组',
    description: '一个第五人格A级赛事，为本项目提供技术指导。',
    link: 'https://idvasg.cn'
  },
  {
    logo: 'https://api.idvasg.cn/loge/friend-ACS%E8%B5%9B%E4%BA%8B.png',
    name: 'ACS赛事组',
    description: '一个有名的第五人格民间赛事',
    link: '#'
  },
  {
    logo: 'http://api.idvevent.cn/uploads/markdown/36019627a8a64ff3a2bd1ba690e8e1af.png',
    name: 'IZL',
    description: '郑州大学第五人格赛事',
    link: '#'
  },
  {
    logo: 'http://api.idvevent.cn/uploads/markdown/cd228fcb5b2842ef8555786afcc313f8.png',
    name: 'IHNU',
    description: '一个面向河南高校的第五人格交流联赛',
    link: 'https://idvasg.cn'
  },
  {
    logo: 'http://api.idvevent.cn/uploads/markdown/b486b1f73dab45389bc89fc7f8b0d0c0.jpg',
    name: 'XRCZ',
    description: '一个已经持续两年的第5人格民间赛事小组,截止到目前为止，举办的比赛奖金规模已经超出了2万,XRCZ致力于为很多处于瓶颈期的人榜屠榜人员组织训练赛的同时，也积极开展相关的竞赛模式,帮不懂比赛的人举办比赛，组织比赛,又称之为新人操作  XRCZ',
    link: '#'
  },
  {
    logo: 'https://idvasg.cn/_nuxt/asg-logo.2xBiWjrM.png',
    name: 'ASG赛事组',
    description: '一个第五人格A级赛事，为本项目提供技术指导。',
    link: 'https://idvasg.cn'
  },
  {
    logo: 'https://idvasg.cn/_nuxt/asg-logo.2xBiWjrM.png',
    name: 'ASG赛事组',
    description: '一个第五人格A级赛事，为本项目提供技术指导。',
    link: 'https://idvasg.cn'
  }
]

// 折叠/展开状态与可见列表（窄屏显示2个，桌面显示6个）
const partnersExpanded = ref(false)
const { smAndDown } = useDisplay()
const initialPartnerCount = computed(() => (smAndDown.value ? 2 : 6))
const visiblePartners = computed(() =>
  partnersExpanded.value ? partners : partners.slice(0, initialPartnerCount.value)
)
const hasMorePartners = computed(() => partners.length > initialPartnerCount.value)
const vTilt = {
  mounted(el, binding) {
    const opt = binding?.value || {}
    const strength = opt.strength ?? 12
    const scale = opt.scale ?? 1.015
    function onEnter() {
      el.classList.add('hovering')
      el.style.transform = `perspective(900px) scale(${scale})`
    }
    function onMove(e) {
      const r = el.getBoundingClientRect()
      const px = (e.clientX - r.left) / r.width
      const py = (e.clientY - r.top) / r.height
      const rx = (py - 0.5) * -2 * strength
      const ry = (px - 0.5) * 2 * strength
      el.style.transform = `perspective(900px) rotateX(${rx}deg) rotateY(${ry}deg) scale(${scale})`
      el.style.setProperty('--gx', px * 100 + '%')
      el.style.setProperty('--gy', py * 100 + '%')
    }
    function onLeave() {
      el.classList.remove('hovering')
      el.style.transform = 'perspective(900px) rotateX(0) rotateY(0) scale(1)'
    }
    el.addEventListener('pointerenter', onEnter)
    el.addEventListener('pointermove', onMove)
    el.addEventListener('pointerleave', onLeave)
    const io = new IntersectionObserver(entries => {
      for (const entry of entries) {
        if (entry.isIntersecting) el.classList.add('in-view')
      }
    }, { threshold: 0.25 })
    io.observe(el)
    el._tilt_cleanup = () => {
      io.disconnect()
      el.removeEventListener('pointerenter', onEnter)
      el.removeEventListener('pointermove', onMove)
      el.removeEventListener('pointerleave', onLeave)
    }
  },
  unmounted(el) {
    el._tilt_cleanup && el._tilt_cleanup()
  }
}
</script>

<template>
  <div class="home-view">
    <!-- Hero -->
    <v-sheet class="hero d-flex align-center position-relative overflow-hidden">
      <div class="hero-bg"></div>
      <v-container class="page-container position-relative" style="z-index: 2;">
        <v-row align="center">
          <v-col cols="12" md="7">
            <div class="text-h3 text-md-h2 font-weight-bold mb-4 text-primary tracking-tight">
              第五人格<br>赛事统一管理平台
            </div>
            <div class="text-h6 text-medium-emphasis mb-8 font-weight-regular" style="max-width: 600px; line-height: 1.6;">
              面向所有主办方的赛事创建、报名审核、赛程管理一体化平台。<br class="d-none d-md-block">
              降低运营成本，提升参赛体验，让每一场比赛更精彩。
            </div>
            <div class="d-flex flex-wrap gap-3">
              <v-btn color="white" size="x-large" class="px-8 text-primary" elevation="4" @click="goCreateEvent" prepend-icon="add">创建赛事</v-btn>
              <v-btn variant="outlined" size="x-large" class="px-8 ml-3" to="/events" prepend-icon="grid_view">浏览赛事</v-btn>
            </div>
            <div class="d-flex align-center mt-8 text-caption text-medium-emphasis">
              <v-icon icon="check_circle" color="success" size="small" class="mr-1"></v-icon>
              <span class="mr-4">免费使用</span>
              <v-icon icon="check_circle" color="success" size="small" class="mr-1"></v-icon>
              <span class="mr-4">一键报名</span>
              <v-icon icon="check_circle" color="success" size="small" class="mr-1"></v-icon>
              <span>高效管理</span>
            </div>
          </v-col>
          <v-col cols="12" md="5" class="d-none d-md-flex justify-center position-relative">
            <img src="/logo.svg" alt="ASG Logo" class="hero-logo" style="width:260px;height:260px; z-index: 2;">
          </v-col>
        </v-row>
      </v-container>
    </v-sheet>

    <!-- Features for Organizers -->
    <v-container class="py-16 page-container">
      <v-row class="mb-6">
        <v-col cols="12" class="text-center">
          <div class="text-h5 font-weight-bold mb-2">为主办方而生的关键能力</div>
          <div class="text-medium-emphasis">覆盖赛事全流程，降低运营成本，提升参赛体验</div>
        </v-col>
      </v-row>
      <v-row>
        <v-col cols="12" md="4" v-for="(card, i) in featureCards" :key="i">
          <v-card class="feature-card animated-card" variant="tonal" v-tilt="{ strength: 10 }" :style="{ '--delay': i * 0.1 + 's' }">
            <v-card-item>
              <div class="d-flex align-center">
                <v-icon color="primary" :icon="card.icon" class="mr-2" />
                <div class="text-subtitle-1 font-weight-medium">{{ card.title }}</div>
              </div>
            </v-card-item>
            <v-card-text class="text-body-2">
              {{ card.text }}
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
      <v-divider class="my-10" />

      <!-- 合作伙伴（从关于页面迁移） -->
      <v-row class="mb-4">
        <v-col cols="12" class="text-center">
          <div class="text-h5 font-weight-bold mb-2">合作伙伴</div>
          <div class="text-medium-emphasis">与我们共同构建更开放与高效的赛事生态</div>
        </v-col>
      </v-row>
      <v-row>
        <v-col v-for="(p, i) in visiblePartners" :key="p.name" cols="12" sm="6" md="4">
          <v-card class="h-100 animated-card" hover v-tilt="{ strength: 14 }" :style="{ '--delay': i * 0.1 + 's' }">
            <v-card-item>
              <div class="d-flex align-center mb-2">
                <v-img
                  :src="p.logo"
                  alt="logo"
                  width="96"
                  aspect-ratio="2"
                  :class="['mr-3', 'partner-logo', { 'blue-filter': i === 0 }]"
                  contain
                >
                  <template #placeholder>
                    <div class="d-flex align-center justify-center" style="width:100%;height:100%">
                      <lottie-player src="/animations/loading.json" background="transparent" speed="1" loop autoplay style="width:64px;height:64px"></lottie-player>
                    </div>
                  </template>
                </v-img>
                <div class="text-subtitle-1 font-weight-bold">{{ p.name }}</div>
              </div>
              <div class="text-body-2 text-medium-emphasis">{{ p.description }}</div>
            </v-card-item>
            <v-card-actions>
              <v-btn :href="p.link" target="_blank" rel="noopener" variant="tonal" prepend-icon="open_in_new">
                访问官网
              </v-btn>
            </v-card-actions>
          </v-card>
        </v-col>
      </v-row>
      <v-row v-if="hasMorePartners" class="mt-4">
        <v-col class="text-center">
          <v-btn @click="partnersExpanded = !partnersExpanded" variant="tonal">
            {{ partnersExpanded ? '收起' : '查看全部' }}
          </v-btn>
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
          <v-card class="animated-card" variant="outlined" v-tilt="{ strength: 8 }">
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
          <v-card class="animated-card" variant="outlined" v-tilt="{ strength: 8 }">
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
          <v-card class="animated-card" variant="outlined" v-tilt="{ strength: 8 }">
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
          <v-card class="animated-card" variant="outlined" v-tilt="{ strength: 8 }">
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
          <v-card class="animated-card" variant="outlined" v-tilt="{ strength: 8 }">
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
          <v-card class="animated-card" variant="outlined" v-tilt="{ strength: 8 }">
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
          <v-card class="animated-card" variant="outlined" v-tilt="{ strength: 8 }">
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
          <v-card class="animated-card" variant="outlined" v-tilt="{ strength: 8 }">
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
          <v-card class="animated-card" variant="tonal" v-tilt="{ strength: 9 }">
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
          <v-card class="animated-card" variant="tonal" v-tilt="{ strength: 9 }">
            <v-card-title>平台愿景</v-card-title>
            <v-card-text class="text-body-2">
              聚合全国第五人格赛事，降低组织成本，提升公平与效率，让每一场比赛更简单、更精彩。
            </v-card-text>
            <!-- 了解更多入口移除（无关于页面） -->
          </v-card>
        </v-col>
      </v-row>
    </v-container>
  </div>
</template>

<style scoped>
.page-container {
  max-width: 1100px;
  margin: 0 auto;
}

.hero {
  min-height: 500px;
  padding-top: 64px; /* Header height */
}

.hero-bg {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: radial-gradient(circle at 50% 0, rgba(var(--v-theme-primary), 0.12), transparent 70%),
              radial-gradient(circle at 85% 30%, rgba(var(--v-theme-tertiary), 0.08), transparent 50%);
  z-index: 1;
}

.blob {
  position: absolute;
  width: 320px;
  height: 320px;
  background: linear-gradient(135deg, rgba(var(--v-theme-primary), 0.3), rgba(var(--v-theme-tertiary), 0.3));
  border-radius: 50%;
  filter: blur(80px);
  animation: float 6s ease-in-out infinite;
  z-index: 1;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
}

@keyframes float {
  0%, 100% { transform: translate(-50%, -50%) scale(1); }
  50% { transform: translate(-50%, -50%) scale(1.1); }
}

.feature-card {
  transition: all 0.3s ease-out;
  position: relative;
  overflow: hidden;
  height: 100%;
}

.feature-card::before {
  content: '';
  position: absolute;
  top: var(--gy, 50%);
  left: var(--gx, 50%);
  width: 0;
  height: 0;
  border-radius: 50%;
  background: radial-gradient(circle, rgba(var(--v-theme-primary), 0.15) 0%, transparent 70%);
  transform: translate(-50%, -50%);
  transition: width 0.4s ease, height 0.4s ease;
}

.feature-card.hovering::before {
  width: 300px;
  height: 300px;
}

.partner-logo {
  filter: none;
  transition: filter 0.3s;
}

.blue-filter { filter: none; }
:deep(.v-theme--light) .blue-filter {
  filter: invert(19%) sepia(93%) saturate(3824%) hue-rotate(198deg) brightness(99%) contrast(101%);
}

.img-skeleton {
  width: 100%;
  height: 100%;
  background-color: rgba(0,0,0,0.1);
}

.animated-card {
  opacity: 0;
  transform: translateY(40px) scale(0.95);
  transition: opacity 0.6s cubic-bezier(0.25, 0.46, 0.45, 0.94), transform 0.6s cubic-bezier(0.25, 0.46, 0.45, 0.94);
  transition-delay: var(--delay, 0s);
}

.animated-card.in-view {
  opacity: 1;
  transform: translateY(0) scale(1);
}
</style>

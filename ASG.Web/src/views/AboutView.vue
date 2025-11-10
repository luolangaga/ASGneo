<script setup>
import PageHero from '../components/PageHero.vue'

// 纯前端：合作伙伴列表（示例数据，可在此处继续补充）
const partners = [
  {
    logo: 'https://docs.bpsys.plfjy.top/images/favicon.ico',
    name: 'neo-bpsys-wpf',
    description: '基于 .NET WPF 开发的第五人格直播BP软件，与其共同构成第五人格赛事系统大家庭',
    link: 'https://bpsys.plfjy.top/'
  },
  {
    logo: 'https://idvasg.cn/_nuxt/asg-logo.2xBiWjrM.png',
    name: 'ASG赛事组',
    description: '一个第五人格A级赛事，为本项目提供技术指导。',
    link: 'https://idvasg.cn'
  }
]
</script>

<template>
  <PageHero title="关于平台" subtitle="了解我们的愿景与合作伙伴" icon="info" />
  <v-container class="py-8" style="max-width: 1100px">
    <v-card class="mb-6" variant="tonal">
      <v-card-title>我们的使命</v-card-title>
      <v-card-text class="text-body-2">
        聚合全国第五人格赛事，降低组织成本，提升公平与效率，打造更易用与开放的赛事生态平台。想要与我们合作可以联系QQ：2667210109
      </v-card-text>
    </v-card>

    <v-card class="mb-4" variant="outlined">
      <v-card-title>合作伙伴</v-card-title>
      <v-card-text>
        <v-row>
          <v-col v-for="p in partners" :key="p.name" cols="12" sm="6" md="4">
            <v-card class="h-100" hover>
              <v-card-item>
                <div class="d-flex align-center mb-2">
                  <v-img :src="p.logo" alt="logo" width="48" height="48" class="mr-3 partner-logo" />
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
      </v-card-text>
    </v-card>
  </v-container>
  
</template>

<style scoped>
.partner-logo {
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0,0,0,0.08);
}
</style>
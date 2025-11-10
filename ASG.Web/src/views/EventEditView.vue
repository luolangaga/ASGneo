<script setup>
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getEvent, updateEvent } from '../services/events'
import MarkdownEditor from '../components/MarkdownEditor.vue'

const route = useRoute()
const router = useRouter()
const id = route.params.id

const loading = ref(false)
const saving = ref(false)
const errorMsg = ref('')

const name = ref('')
const description = ref('')
const registrationStartTime = ref('') // datetime-local
const registrationEndTime = ref('')   // datetime-local
const competitionStartTime = ref('')  // datetime-local
const competitionEndTime = ref('')    // datetime-local, optional
const maxTeams = ref('')
const status = ref(0)

const statusOptions = [
  { title: '草稿', value: 0 },
  { title: '报名中', value: 1 },
  { title: '报名结束', value: 2 },
  { title: '进行中', value: 3 },
  { title: '已结束', value: 4 },
  { title: '已取消', value: 5 },
]

function isoToLocalInput(iso) {
  if (!iso) return ''
  const d = new Date(iso)
  const pad = (n) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function localInputToIso(localStr) {
  if (!localStr) return null
  return new Date(localStr).toISOString()
}

async function load() {
  loading.value = true
  errorMsg.value = ''
  try {
    const ev = await getEvent(id)
    name.value = ev.name
    description.value = ev.description || ''
    registrationStartTime.value = isoToLocalInput(ev.registrationStartTime)
    registrationEndTime.value = isoToLocalInput(ev.registrationEndTime)
    competitionStartTime.value = isoToLocalInput(ev.competitionStartTime)
    competitionEndTime.value = ev.competitionEndTime ? isoToLocalInput(ev.competitionEndTime) : ''
    maxTeams.value = ev.maxTeams ?? ''
    status.value = ev.status
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载赛事失败'
  } finally {
    loading.value = false
  }
}

onMounted(load)

async function onSave() {
  saving.value = true
  errorMsg.value = ''
  try {
    const descLen = (description.value || '').length
    if (descLen >= 1000) {
      errorMsg.value = '赛事描述字数需小于 1000'
      throw new Error('赛事描述超长')
    }
    const dto = {
      name: name.value,
      description: description.value || null,
      registrationStartTime: localInputToIso(registrationStartTime.value),
      registrationEndTime: localInputToIso(registrationEndTime.value),
      competitionStartTime: localInputToIso(competitionStartTime.value),
      competitionEndTime: competitionEndTime.value ? localInputToIso(competitionEndTime.value) : null,
      maxTeams: maxTeams.value ? Number(maxTeams.value) : null,
      status: Number(status.value),
    }
    await updateEvent(id, dto)
    router.push('/events/manage')
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '保存失败'
  } finally {
    saving.value = false
  }
}

function onCancel() {
  router.push('/events/manage')
}
</script>

<template>
  <v-container class="py-8" style="max-width: 900px;">
    <v-card>
      <v-card-title>编辑赛事</v-card-title>
      <v-card-text>
        <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
        <v-progress-linear v-if="loading" indeterminate color="primary" />

        <v-form v-else>
          <v-text-field v-model="name" label="赛事名称" required />
          <MarkdownEditor v-model="description" label="赛事描述" :rows="8" :maxLength="999" />

          <v-row>
            <v-col cols="12" md="6">
              <v-text-field v-model="registrationStartTime" label="报名开始" type="datetime-local" required />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="registrationEndTime" label="报名结束" type="datetime-local" required />
            </v-col>
          </v-row>

          <v-row>
            <v-col cols="12" md="6">
              <v-text-field v-model="competitionStartTime" label="比赛开始" type="datetime-local" required />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="competitionEndTime" label="比赛结束（可选）" type="datetime-local" />
            </v-col>
          </v-row>

          <v-row>
            <v-col cols="12" md="6">
              <v-text-field v-model="maxTeams" label="最大队伍数（可选）" type="number" />
            </v-col>
            <v-col cols="12" md="6">
              <v-select v-model="status" :items="statusOptions" item-title="title" item-value="value" label="赛事状态" />
            </v-col>
          </v-row>
        </v-form>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="onCancel">取消</v-btn>
        <v-btn color="primary" :loading="saving" @click="onSave" prepend-icon="save">保存</v-btn>
      </v-card-actions>
    </v-card>
  </v-container>
</template>

<style scoped>
</style>
<script setup>
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getEvent, updateEvent, deleteEvent } from '../services/events'
import { polishText } from '../services/ai'
import MarkdownEditor from '../components/MarkdownEditor.vue'

const route = useRoute()
const router = useRouter()
const id = route.params.id

const loading = ref(false)
const saving = ref(false)
const deleting = ref(false)
const errorMsg = ref('')

const name = ref('')
const description = ref('')
const registrationStartTime = ref('') // datetime-local
const registrationEndTime = ref('')   // datetime-local
const competitionStartTime = ref('')  // datetime-local
const competitionEndTime = ref('')    // datetime-local, optional
const maxTeams = ref('')
const status = ref(0)
const qqGroup = ref('')
const rulesMarkdown = ref('')
const polishing = ref(false)
const registrationMode = ref(0)
const registrationModeOptions = [
  { title: '战队报名', value: 0 },
  { title: '单人报名', value: 1 },
]

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
    qqGroup.value = ev.qqGroup || ''
    rulesMarkdown.value = ev.rulesMarkdown || ''
    registrationStartTime.value = isoToLocalInput(ev.registrationStartTime)
    registrationEndTime.value = isoToLocalInput(ev.registrationEndTime)
    competitionStartTime.value = isoToLocalInput(ev.competitionStartTime)
    competitionEndTime.value = ev.competitionEndTime ? isoToLocalInput(ev.competitionEndTime) : ''
    maxTeams.value = ev.maxTeams ?? ''
    status.value = ev.status
    registrationMode.value = (ev.registrationMode ?? ev.RegistrationMode ?? 0)
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
      qqGroup: qqGroup.value?.trim() || null,
      rulesMarkdown: rulesMarkdown.value?.trim() || null,
      registrationStartTime: localInputToIso(registrationStartTime.value),
      registrationEndTime: localInputToIso(registrationEndTime.value),
      competitionStartTime: localInputToIso(competitionStartTime.value),
      competitionEndTime: competitionEndTime.value ? localInputToIso(competitionEndTime.value) : null,
      maxTeams: maxTeams.value ? Number(maxTeams.value) : null,
      status: Number(status.value),
      registrationMode: Number(registrationMode.value),
    }
    await updateEvent(id, dto)
    router.push('/events/manage')
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '保存失败'
  } finally {
    saving.value = false
  }
}

async function onPolishDescription() {
  if (!description.value || polishing.value) return
  polishing.value = true
  try {
    const r = await polishText({ scope: 'event', text: description.value })
    if (r?.text || r?.Text) description.value = r.text || r.Text
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || 'AI润色失败'
  } finally {
    polishing.value = false
  }
}

function onCancel() {
  router.push('/events/manage')
}

async function onDelete() {
  const ok = window.confirm(`确定要删除此赛事吗？此操作不可恢复。`)
  if (!ok) return
  deleting.value = true
  errorMsg.value = ''
  try {
    await deleteEvent(id)
    router.push('/events/manage')
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '删除失败'
  } finally {
    deleting.value = false
  }
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
          <div class="d-flex justify-end mt-2">
            <v-btn :loading="polishing" variant="tonal" color="primary" prepend-icon="auto_awesome" @click="onPolishDescription">AI润色</v-btn>
          </div>

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

          <v-text-field v-model="qqGroup" label="QQ群（可选，群号或邀请链接）" />
          <MarkdownEditor v-model="rulesMarkdown" label="赛事规则（Markdown，可选）" :rows="10" :maxLength="0" />

          <v-row>
            <v-col cols="12" md="6">
              <v-text-field v-model="maxTeams" label="最大队伍数（可选）" type="number" />
            </v-col>
            <v-col cols="12" md="6">
              <v-select v-model="status" :items="statusOptions" item-title="title" item-value="value" label="赛事状态" />
            </v-col>
          </v-row>
          <v-row>
            <v-col cols="12" md="6">
              <v-select v-model="registrationMode" :items="registrationModeOptions" item-title="title" item-value="value" label="报名模式" />
            </v-col>
          </v-row>
        </v-form>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="onCancel">取消</v-btn>
        <v-btn color="error" variant="text" :loading="deleting" @click="onDelete" prepend-icon="delete">删除</v-btn>
        <v-btn color="primary" :loading="saving" @click="onSave" prepend-icon="save">保存</v-btn>
      </v-card-actions>
    </v-card>
  </v-container>
</template>

<style scoped>
</style>

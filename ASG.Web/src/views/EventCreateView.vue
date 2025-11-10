<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { createEvent, uploadEventLogo } from '../services/events'
import PageHero from '../components/PageHero.vue'
import MarkdownEditor from '../components/MarkdownEditor.vue'

const router = useRouter()
const saving = ref(false)
const errorMsg = ref('')

const name = ref('')
const description = ref('')
const registrationStartTime = ref('')
const registrationEndTime = ref('')
const competitionStartTime = ref('')
const competitionEndTime = ref('')
const maxTeams = ref(null)
const logoFile = ref(null)
const logoError = ref('')

function toIso(dt) {
  return dt ? new Date(dt).toISOString() : null
}

async function onSubmit() {
  saving.value = true
  errorMsg.value = ''
  logoError.value = ''
  try {
    const descLen = (description.value || '').length
    if (descLen >= 1000) {
      errorMsg.value = '赛事描述字数需小于 1000'
      throw new Error('赛事描述超长')
    }
    if (!logoFile.value) {
      logoError.value = '请先选择赛事Logo（png/jpg/webp，≤5MB）'
      throw new Error('缺少赛事Logo')
    }
    const payload = {
      name: name.value.trim(),
      description: description.value?.trim() || null,
      registrationStartTime: toIso(registrationStartTime.value),
      registrationEndTime: toIso(registrationEndTime.value),
      competitionStartTime: toIso(competitionStartTime.value),
      competitionEndTime: toIso(competitionEndTime.value),
      maxTeams: maxTeams.value ? Number(maxTeams.value) : null,
    }
    const created = await createEvent(payload)
    const id = created?.id || created?.Id
    if (id) {
      try {
        await uploadEventLogo(id, logoFile.value)
      } catch (e) {
        // 赛事已创建，但Logo上传失败，仍然跳转详情页，便于用户稍后补传
        errorMsg.value = e?.payload?.message || e?.message || '赛事已创建，但Logo上传失败'
      }
      router.push(`/events/${id}`)
    } else {
      router.push('/events')
    }
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '创建赛事失败'
  } finally {
    saving.value = false
  }
}

function onLogoSelected(files) {
  logoError.value = ''
  const file = Array.isArray(files) ? files[0] : files
  if (!file) {
    logoFile.value = null
    return
  }
  const okTypes = ['image/png', 'image/jpeg', 'image/jpg', 'image/webp']
  if (!okTypes.includes(file.type)) {
    logoError.value = '文件类型不支持，请选择 png/jpg/webp'
    logoFile.value = null
    return
  }
  if (file.size > 5 * 1024 * 1024) {
    logoError.value = '文件过大，最大 5MB'
    logoFile.value = null
    return
  }
  logoFile.value = file
}
</script>

<template>
  <PageHero title="创建赛事" subtitle="填写基本信息并上传赛事Logo" icon="add_box">
    <template #actions>
      <v-btn variant="text" class="mr-3 mb-3" to="/events" prepend-icon="grid_view">返回看板</v-btn>
      <v-btn variant="text" class="mb-3" to="/events/manage" prepend-icon="settings">我的赛事</v-btn>
    </template>
  </PageHero>
  <v-container class="py-8" style="max-width: 860px">
    <v-card>
      <v-card-title>创建赛事</v-card-title>
      <v-card-text>
        <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
        <v-form @submit.prevent="onSubmit">
          <v-text-field v-model="name" label="赛事名称" prepend-inner-icon="text_fields" required />
          <MarkdownEditor v-model="description" label="赛事描述" :rows="8" :maxLength="999" />
          <div class="mb-2">
            <div class="text-caption mb-2">赛事Logo（必填，png/jpg/webp，≤5MB）：</div>
            <v-file-input
              prepend-icon="add_photo_alternate"
              density="comfortable"
              accept="image/png, image/jpeg, image/jpg, image/webp"
              show-size
              required
              @update:modelValue="onLogoSelected"
              label="选择图片文件"
            />
            <v-alert v-if="logoError" type="error" :text="logoError" class="mt-2" />
          </div>
          <v-row>
            <v-col cols="12" md="6">
              <v-text-field v-model="registrationStartTime" label="报名开始时间" type="datetime-local" prepend-inner-icon="calendar_month" required />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="registrationEndTime" label="报名结束时间" type="datetime-local" prepend-inner-icon="calendar_month" required />
            </v-col>
          </v-row>
          <v-row>
            <v-col cols="12" md="6">
              <v-text-field v-model="competitionStartTime" label="比赛开始时间" type="datetime-local" prepend-inner-icon="calendar_month" required />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field v-model="competitionEndTime" label="比赛结束时间" type="datetime-local" prepend-inner-icon="calendar_month" />
            </v-col>
          </v-row>
          <v-text-field v-model="maxTeams" label="最大队伍数" type="number" min="1" max="1000" prepend-inner-icon="group" />
          <div class="d-flex justify-end">
            <v-btn :loading="saving" color="primary" type="submit" prepend-icon="save">创建</v-btn>
          </div>
        </v-form>
      </v-card-text>
    </v-card>
  </v-container>
</template>

<style scoped>
</style>
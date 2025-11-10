<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { registerTeam } from '../services/teams'
import MarkdownEditor from '../components/MarkdownEditor.vue'
import { updateCurrentUser, currentUser } from '../stores/auth'

const router = useRouter()
const name = ref('')
const password = ref('')
const description = ref('')
const players = ref([
  { name: '', gameId: '', gameRank: '', description: '' },
])
const saving = ref(false)
const errorMsg = ref('')

function addPlayer() {
  players.value.push({ name: '', gameId: '', gameRank: '', description: '' })
}

function removePlayer(index) {
  players.value.splice(index, 1)
}

async function onSubmit() {
  saving.value = true
  errorMsg.value = ''
  try {
    const descLen = (description.value || '').length
    if (descLen >= 1000) {
      errorMsg.value = '战队描述字数需小于 1000'
      throw new Error('战队描述超长')
    }
    const payload = {
      name: name.value.trim(),
      password: password.value,
      description: description.value?.trim() || null,
      players: players.value.map(p => ({
        name: p.name.trim(),
        gameId: p.gameId?.trim() || null,
        gameRank: p.gameRank?.trim() || null,
        description: p.description?.trim() || null,
      })),
    }
    const team = await registerTeam(payload)
    updateCurrentUser({ ...(currentUser.value || {}), teamId: team.id })
    router.push('/events')
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '创建战队失败'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <v-container class="py-8" style="max-width: 860px">
    <v-card>
      <v-card-title>创建战队</v-card-title>
      <v-card-text>
        <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
        <v-form @submit.prevent="onSubmit">
          <v-text-field v-model="name" label="战队名称" prepend-inner-icon="group" required />
          <v-text-field v-model="password" label="战队密码" type="password" prepend-inner-icon="lock" required />
          <MarkdownEditor v-model="description" label="战队描述" :rows="6" :maxLength="999" />

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
      </v-card-text>
    </v-card>
  </v-container>
</template>

<style scoped>
</style>
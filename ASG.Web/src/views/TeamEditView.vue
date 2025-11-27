<script setup>
import { ref, computed, onMounted, watch } from 'vue';
import { useRouter } from 'vue-router';
import PageHero from '../components/PageHero.vue';
import MarkdownEditor from '../components/MarkdownEditor.vue';
import PlayerInfoForm from '../components/PlayerInfoForm.vue';
import { currentUser, isAuthenticated, updateCurrentUser } from '../stores/auth';
import { getTeam, updateTeam, uploadTeamLogo, deleteTeam, generateInvite } from '../services/teams';
import { transferOwner } from '../services/teams';
import { searchUsersByName } from '../services/user';
import { polishText } from '../services/ai';
import { getProfile } from '../services/user';
import ResultDialog from '../components/ResultDialog.vue';
import { extractErrorDetails } from '../services/api';

const router = useRouter();

const me = computed(() => currentUser.value);
const teamId = computed(() => me.value?.teamId || me.value?.TeamId || null);

const loading = ref(false);
const saving = ref(false);
const uploadingLogo = ref(false);
const errorMsg = ref('');
const successMsg = ref('');
const showSuccess = ref(false);
const errorOpen = ref(false);
const errorDetails = ref([]);
const deleting = ref(false);

const name = ref('');
const description = ref('');
const players = ref([]);
const logoUrl = ref('');
const inviteInfo = ref(null);
const generatingInvite = ref(false);
const transferUserId = ref('');
const selectedTargetUserId = ref('');
const userQuery = ref('');
const userOptions = ref([]);
const transferring = ref(false);
const appOrigin = computed(() => (typeof window !== 'undefined' && window.location) ? window.location.origin : '');
const inviteLink = computed(() => {
  const token = inviteInfo.value?.token || inviteInfo.value?.Token;
  return token ? `${appOrigin.value}/join/${token}` : '';
});

const canEdit = computed(() => isAuthenticated.value && !!teamId.value);

const hidePlayers = ref(false);

function addPlayer() {
  players.value.push({ id: undefined, name: '', gameId: '', gameRank: '', description: '', playerType: 2 });
}

function removePlayer(index) {
  players.value.splice(index, 1);
}

async function loadTeam() {
  if (!teamId.value) return;
  loading.value = true;
  errorMsg.value = '';
  try {
    const t = await getTeam(teamId.value);
    name.value = t.name || '';
    description.value = t.description || '';
    logoUrl.value = t.logoUrl || '';
    hidePlayers.value = !!(t.hidePlayers ?? t.HidePlayers);
    players.value = (t.players || []).map(p => ({
      id: p.id,
      name: p.name || '',
      gameId: p.gameId || '',
      gameRank: p.gameRank || '',
      description: p.description || '',
      playerType: p.playerType ?? p.PlayerType ?? 2,
    }));
    if (!players.value.length) addPlayer();
    inviteInfo.value = null;
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '加载战队失败';
  } finally {
    loading.value = false;
  }
}

async function onSubmit() {
  if (!teamId.value) return;
  saving.value = true;
  errorMsg.value = '';
  successMsg.value = '';
  try {
    const descLen = (description.value || '').length;
    if (descLen >= 1000) {
      errorMsg.value = '战队描述字数需小于 1000';
      throw new Error('战队描述超长');
    }
    if (!players.value.length) {
      errorMsg.value = '至少需要一个队员';
      throw new Error('队员为空');
    }
    const payload = {
      name: name.value.trim(),
      description: description.value?.trim() || null,
      hidePlayers: !!hidePlayers.value,
      players: players.value.map(p => ({
        id: p.id || undefined,
        name: (p.name || '').trim(),
        gameId: p.gameId?.trim() || null,
        gameRank: p.gameRank?.trim() || null,
        description: p.description?.trim() || null,
        playerType: p.playerType != null ? Number(p.playerType) : null,
      })),
    };
    const updated = await updateTeam(teamId.value, payload);
    name.value = updated.name || name.value;
    description.value = updated.description || description.value;
    logoUrl.value = updated.logoUrl || logoUrl.value;
    hidePlayers.value = !!(updated.hidePlayers ?? updated.HidePlayers ?? hidePlayers.value);
    players.value = (updated.players || []).map(p => ({
      id: p.id,
      name: p.name || '',
      gameId: p.gameId || '',
      gameRank: p.gameRank || '',
      description: p.description || '',
    }));
    successMsg.value = '保存成功';
  } catch (err) {
    const msg = err?.payload?.message || err?.message || '保存失败';
    errorMsg.value = msg.includes('权限') ? '你没有权限修改此战队' : msg;
    errorDetails.value = extractErrorDetails(err?.payload);
  } finally {
    saving.value = false;
  }
}

const posting = ref(false);

async function onPolishDescription() {
  if (!description.value || posting.value) return;
  posting.value = true;
  try {
    const r = await polishText({ scope: 'team', text: description.value });
    if (r?.text || r?.Text) description.value = r.text || r.Text;
  } catch (e) {
    errorMsg.value = e?.payload?.message || e?.message || 'AI润色失败';
  } finally {
    posting.value = false;
  }
}

async function onUploadLogo(e) {
  if (!teamId.value) return;
  const file = e.target?.files?.[0];
  if (!file) return;
  uploadingLogo.value = true;
  errorMsg.value = '';
  successMsg.value = '';
  try {
    const res = await uploadTeamLogo(teamId.value, file);
    logoUrl.value = res.logoUrl || logoUrl.value;
    successMsg.value = '徽标已更新';
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '上传徽标失败';
    errorDetails.value = extractErrorDetails(err?.payload);
  } finally {
    uploadingLogo.value = false;
    e.target.value = '';
  }
}

watch(successMsg, (v) => { if (v) showSuccess.value = true; });
watch(errorMsg, (v) => { if (v) errorOpen.value = true; });

onMounted(() => {
  if (!canEdit.value) return;
  loadTeam();
});

async function onGenerateInvite() {
  if (!teamId.value) return;
  generatingInvite.value = true;
  errorMsg.value = '';
  successMsg.value = '';
  try {
    const info = await generateInvite(teamId.value, 7);
    inviteInfo.value = info;
    successMsg.value = '邀请链接已生成';
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '生成邀请失败';
  } finally {
    generatingInvite.value = false;
  }
}

async function copyInviteLinkSafe() {
  if (!inviteLink.value) return;
  try {
    await navigator.clipboard?.writeText(inviteLink.value);
    successMsg.value = '邀请链接已复制';
  } catch {
    try {
      const input = document.createElement('input');
      input.value = inviteLink.value;
      document.body.appendChild(input);
      input.select();
      document.execCommand('copy');
      document.body.removeChild(input);
      successMsg.value = '邀请链接已复制';
    } catch {}
  }
}
async function onDeleteTeam() {
  if (!teamId.value) return;
  const ok = window.confirm(`确定要删除战队“${name.value}”吗？此操作不可恢复。`);
  if (!ok) return;
  deleting.value = true;
  errorMsg.value = '';
  successMsg.value = '';
  try {
    await deleteTeam(teamId.value);
    try {
      const profile = await getProfile();
      updateCurrentUser(profile);
    } catch {}
    router.push('/teams/create');
  } catch (err) {
    const msg = err?.payload?.message || err?.message || '删除战队失败';
    errorMsg.value = msg;
    errorDetails.value = extractErrorDetails(err?.payload);
  } finally {
    deleting.value = false;
  }
}

async function onTransferOwner() {
  const targetId = selectedTargetUserId.value || transferUserId.value;
  if (!teamId.value || !targetId) return;
  transferring.value = true;
  errorMsg.value = '';
  successMsg.value = '';
  try {
    await transferOwner(teamId.value, String(targetId).trim());
    successMsg.value = '已转移队长';
  } catch (err) {
    const msg = err?.payload?.message || err?.message || '转移失败';
    errorMsg.value = msg.includes('权限') ? '你不是队长，无法转移' : msg;
    errorDetails.value = extractErrorDetails(err?.payload);
  } finally {
    transferring.value = false;
  }
}

async function onUserSearch(q) {
  userQuery.value = q;
  try {
    const res = await searchUsersByName(q, 10);
    userOptions.value = (res || []).map(u => ({
      id: u.id || u.Id,
      title: `${u.fullName || u.FullName || '未命名'}${u.email ? ' · ' + u.email : ''}`,
    }));
  } catch {}
}
</script>

<template>
  <PageHero title="编辑我的战队" subtitle="修改战队基础信息与队员" icon="group">
    <template #actions>
      <v-btn variant="text" class="mb-3" to="/teams/search" prepend-icon="group">搜索战队</v-btn>
    </template>
  </PageHero>
  <v-container class="py-6" style="max-width: 900px">
    <template v-if="!isAuthenticated">
      <v-alert type="info" text="请先登录以编辑你的战队" class="mb-4" />
      <div class="d-flex gap-2">
        <v-btn color="primary" to="/login" prepend-icon="login">去登录</v-btn>
        <v-btn color="secondary" to="/register" prepend-icon="person_add">注册账号</v-btn>
      </div>
    </template>
    <template v-else-if="!teamId">
      <v-alert type="info" text="你还没有战队，创建或绑定一个战队以编辑。" class="mb-4" />
      <div class="d-flex flex-wrap gap-2 mb-4">
        <v-btn color="primary" to="/teams/create" prepend-icon="group">去创建战队</v-btn>
        <v-btn color="secondary" to="/profile" prepend-icon="link">去绑定已有战队</v-btn>
      </div>
    </template>
    <template v-else>
      <v-form @submit.prevent="onSubmit">
        <v-row>
          <v-col cols="12">
            <v-card>
              <v-card-title>基础信息</v-card-title>
              <v-card-text>
                <v-row>
                  <v-col cols="12">
                    <v-text-field v-model="name" label="战队名称" prepend-inner-icon="group" required />
                  </v-col>
                  <v-col cols="12">
                    <MarkdownEditor v-model="description" label="战队描述" :rows="6" :maxLength="999" />
                    <div class="d-flex justify-end mt-2">
                      <v-btn :loading="posting" variant="tonal" color="primary" prepend-icon="auto_awesome" @click="onPolishDescription">AI润色</v-btn>
                    </div>
                  </v-col>
                  <v-col cols="12">
                    <v-switch v-model="hidePlayers" class="mt-3" color="warning" hide-details :label="'隐藏队员信息（仅主办方/赛事管理员/管理员可见）'" />
                    <v-alert v-if="hidePlayers" type="warning" variant="tonal" text="开启后，他人将无法查看你队员的昵称、ID、段位与简介" class="mt-2" />
                  </v-col>
                </v-row>
              </v-card-text>
            </v-card>
          </v-col>

          <v-col cols="12">
            <v-card>
              <v-card-title>队员管理</v-card-title>
              <v-card-text>
                <div class="d-flex align-center justify-space-between mb-2">
                  <div class="text-subtitle-1">邀请队员加入</div>
                  <v-btn color="secondary" variant="tonal" :loading="generatingInvite" @click="onGenerateInvite" prepend-icon="link">生成邀请链接</v-btn>
                </div>
                <template v-if="inviteInfo">
                  <v-alert type="success" class="mb-2">
                    <div class="d-flex align-center">
                      <div class="flex-grow-1">邀请有效期至：{{ new Date(inviteInfo.expiresAt || inviteInfo.ExpiresAt).toLocaleString() }}</div>
                      <v-btn variant="text" :title="'点击复制邀请链接'" @click="copyInviteLinkSafe" prepend-icon="content_copy">复制链接</v-btn>
                    </div>
                    <div class="mt-2 text-caption">{{ inviteLink }}</div>
                  </v-alert>
                </template>
                <v-divider class="my-4" />
                <div class="d-flex align-center justify-space-between mb-2">
                  <div class="text-subtitle-1">队员信息（至少 1 人）</div>
                  <v-btn color="secondary" variant="tonal" @click="addPlayer" prepend-icon="person_add">添加队员</v-btn>
                </div>
                <PlayerInfoForm 
                  v-for="(p, idx) in players" 
                  :key="idx"
                  v-model="players[idx]"
                  :removable="players.length > 1"
                  @remove="removePlayer(idx)"
                />
              </v-card-text>
            </v-card>
          </v-col>

          <v-col cols="12">
            <v-card>
              <v-card-title>高级设置</v-card-title>
              <v-card-text>
                <div class="text-subtitle-1 mb-2">战队徽标</div>
                <v-row class="mb-3">
                  <v-col cols="12" md="4" class="d-flex align-center">
                    <v-avatar size="64" v-if="logoUrl">
                      <v-img :src="logoUrl" cover />
                    </v-avatar>
                  </v-col>
                  <v-col cols="12" md="8">
                    <v-file-input
                      accept="image/png,image/jpeg,image/jpg,image/webp"
                      label="上传新徽标"
                      prepend-icon="image"
                      :loading="uploadingLogo"
                      @change="onUploadLogo"
                      hide-details
                    />
                  </v-col>
                </v-row>
                <v-divider class="my-4" />
                <div class="text-subtitle-1 mb-2">转移队长</div>
                <v-row>
                  <v-col cols="12" md="8">
                    <v-autocomplete
                      v-model="selectedTargetUserId"
                      v-model:search="userQuery"
                      :items="userOptions"
                      item-title="title"
                      item-value="id"
                      label="搜索用户（姓名/用户名/邮箱）"
                      prepend-inner-icon="search"
                      hide-details
                      clearable
                      @update:search="onUserSearch"
                    />
                  </v-col>
                  <v-col cols="12" md="4" class="d-flex align-end">
                    <v-btn :loading="transferring" color="warning" @click="onTransferOwner" prepend-icon="sync_alt">转移队长</v-btn>
                  </v-col>
                </v-row>
              </v-card-text>
            </v-card>
          </v-col>

          <v-col cols="12" class="mt-4">
            <div class="d-flex justify-end">
              <v-btn color="error" variant="text" :loading="deleting" @click="onDeleteTeam" prepend-icon="delete" class="mr-2">删除战队</v-btn>
              <v-btn :loading="saving" color="primary" type="submit" prepend-icon="save">保存修改</v-btn>
            </div>
          </v-col>
        </v-row>
      </v-form>
    </template>
  </v-container>
  <ResultDialog v-model="showSuccess" :type="'success'" :message="successMsg" />
  <ResultDialog v-model="errorOpen" :type="'error'" :message="errorMsg" :details="errorDetails" />
</template>

<style scoped>
.gap-2 { gap: 8px; }
.gap-3 { gap: 12px; }
</style>

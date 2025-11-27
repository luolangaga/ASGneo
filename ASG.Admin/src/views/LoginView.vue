<script setup>
import { ref, onMounted, onBeforeUnmount, nextTick, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { login } from '../services/auth'
import { getMyRole } from '../services/roles'
import { setRole } from '../stores/auth'

const router = useRouter()
const route = useRoute()
const email = ref('')
const password = ref('')
const loading = ref(false)
const errorMsg = ref('')
const showPassword = ref(false)
const formRef = ref(null)
const isPasswordFocus = ref(false)

const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
const emailRules = [v => !!(v && v.trim()) || '请输入邮箱', v => emailRegex.test(String(v).trim()) || '邮箱格式不正确']
const passwordRules = [v => !!v || '请输入密码', v => String(v).length >= 6 || '密码至少 6 位']

const faceRef = ref(null)
const leftEyeRef = ref(null)
const rightEyeRef = ref(null)
const leftPupil = ref({ x: 0, y: 0 })
const rightPupil = ref({ x: 0, y: 0 })
let rafId = 0
let leftRect = null
let rightRect = null
const faceTransform = ref({ x: 0, y: 0, r: 0 })
function onMouseOut(e) { if (!e.relatedTarget) { onMouseLeave() } }

const mouthPath = computed(() => {
  const len = String(email.value || '').length
  const max = 12
  const t = Math.min(len, max) / max
  const w = 66
  const s = Math.round(t * 16)
  if (s <= 0) return `M 0 0 L ${w} 0`
  const a = w / 2
  const r = (a * a + s * s) / (2 * s)
  return `M 0 ${s} A ${r} ${r} 0 0 1 ${w} ${s}`
})

function updateEyeRects() {
  if (!leftEyeRef.value || !rightEyeRef.value) return
  leftRect = leftEyeRef.value.getBoundingClientRect()
  rightRect = rightEyeRef.value.getBoundingClientRect()
}

function onMouseMove(e) {
  if (!leftRect || !rightRect) updateEyeRects()
  const targetRadius = 7
  const calc = (rect) => {
    const cx = rect.left + rect.width / 2
    const cy = rect.top + rect.height / 2
    const dx = e.clientX - cx
    const dy = e.clientY - cy
    const dist = Math.max(1, Math.hypot(dx, dy))
    const k = Math.min(targetRadius, dist) / dist
    return { x: dx * k, y: dy * k }
  }
  const left = calc(leftRect)
  const right = calc(rightRect)
  leftPupil.value = left
  rightPupil.value = right
  if (faceRef.value) {
    const fr = faceRef.value.getBoundingClientRect()
    const fx = (e.clientX - (fr.left + fr.width / 2)) / fr.width
    const fy = (e.clientY - (fr.top + fr.height / 2)) / fr.height
    faceTransform.value = { x: fx * 6, y: fy * 6, r: fx * 4 }
  }
}

function onMouseLeave() { leftPupil.value = { x: 0, y: 0 }; rightPupil.value = { x: 0, y: 0 } }

onMounted(async () => {
  await nextTick(); updateEyeRects()
  window.addEventListener('resize', updateEyeRects)
  window.addEventListener('mousemove', onMouseMove)
  window.addEventListener('mouseout', onMouseOut)
})

onBeforeUnmount(() => { window.removeEventListener('resize', updateEyeRects); window.removeEventListener('mousemove', onMouseMove); window.removeEventListener('mouseout', onMouseOut); if (rafId) cancelAnimationFrame(rafId) })

async function onSubmit() {
  errorMsg.value = ''
  loading.value = true
  try {
    const res = await formRef.value?.validate?.(); if (res && res.valid === false) { loading.value = false; return }
    await login(email.value.trim(), password.value)
    // 校验管理员角色：Admin(2) 或 SuperAdmin(3)
    try {
      const role = await getMyRole(); setRole(role)
      const roleVal = role?.Value ?? role?.value
      if (typeof roleVal === 'number' && roleVal < 2) {
        errorMsg.value = '非管理员账号，无法进入管理后台'
        return
      }
    } catch {}
    const redirect = route.query?.redirect || '/'
    router.push(String(redirect))
  } catch (err) {
    errorMsg.value = err?.payload?.message || err?.message || '登录失败'
  } finally { loading.value = false }
}
</script>

<template>
  <v-container class="py-10" style="max-width: 1200px">
    <v-card>
      <v-card-title>-------管理员登录------</v-card-title>
      <v-card-text>
      <div class="login-face" ref="faceRef" :class="{ 'eyes-closed': isPasswordFocus }">
        <div class="face" :style="{ transform: `translate(${faceTransform.x}px, ${faceTransform.y}px) rotate(${faceTransform.r}deg)` }">
          <div class="eye" ref="leftEyeRef">
            <div class="pupil" :style="{ transform: `translate(calc(-50% + ${leftPupil.x}px), calc(-50% + ${leftPupil.y}px))` }" />
            <div class="eyelid" />
          </div>
          <div class="brow brow-left" />
          <div class="eye" ref="rightEyeRef">
            <div class="pupil" :style="{ transform: `translate(calc(-50% + ${rightPupil.x}px), calc(-50% + ${rightPupil.y}px))` }" />
            <div class="eyelid" />
          </div>
          <div class="brow brow-right" />
          <svg class="mouth-svg" viewBox="0 0 66 24" preserveAspectRatio="none">
            <path :d="mouthPath" fill="none" stroke="#333" stroke-width="4" stroke-linecap="round" />
          </svg>
        </div>
      </div>
        <v-alert v-if="errorMsg" type="error" :text="errorMsg" class="mb-4" />
        <v-form ref="formRef" @submit.prevent="onSubmit">
          <v-text-field v-model="email" label="邮箱" :rules="emailRules" prepend-inner-icon="mdi-email" clearable hide-details />
          <v-text-field v-model="password" label="密码" :type="showPassword ? 'text' : 'password'" :rules="passwordRules" prepend-inner-icon="mdi-lock" :append-inner-icon="showPassword ? 'mdi-eye-off' : 'mdi-eye'" @click:append-inner="showPassword = !showPassword" @focus="isPasswordFocus = true" @blur="isPasswordFocus = false" hide-details />
          <v-btn type="submit" color="primary" :loading="loading" block class="mt-4">登录</v-btn>
        </v-form>
      </v-card-text>
    </v-card>
  </v-container>
  </template>

<style scoped>
.login-face { display: flex; justify-content: center; align-items: center; margin-bottom: 24px }
.face { position: relative; width: 180px; height: 180px; border-radius: 50%; background: #ffd8b5; box-shadow: 0 6px 12px rgba(0,0,0,0.06); display: flex; justify-content: space-around; align-items: center; padding: 0 24px; will-change: transform }
.eye { position: relative; width: 42px; height: 42px; background: #fff; border: 2px solid #333; border-radius: 50%; overflow: hidden }
.pupil { position: absolute; top: 50%; left: 50%; width: 16px; height: 16px; background: #000; border-radius: 50%; transition: transform 0.06s linear }
.pupil::after { content: ""; position: absolute; top: 2px; left: 4px; width: 5px; height: 5px; border-radius: 50%; background: rgba(255,255,255,0.8) }
.eyelid { position: absolute; top: 0; left: 0; right: 0; height: 0; background: #ffd8b5; transition: height 0.22s ease }
.brow { position: absolute; width: 40px; height: 12px; background: #333; border-radius: 6px; top: 24px }
.brow-left { left: 36px }
.brow-right { right: 36px }
.mouth-svg { position: absolute; bottom: 26px; left: 50%; transform: translateX(-50%); width: 66px; height: 24px }
.eyes-closed .eyelid { height: 100% }
.eyes-closed .pupil { opacity: 0.15 }
.eyes-closed .brow { transform: translateY(2px) }
</style>
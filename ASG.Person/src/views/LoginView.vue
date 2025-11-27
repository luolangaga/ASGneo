<script setup>
import { ref, onMounted, onBeforeUnmount, nextTick, computed } from 'vue'
import { login } from '../services/auth'
import { setAuth } from '../stores/auth'
import { notify } from '../stores/notify'
import { useRoute, useRouter } from 'vue-router'
const route = useRoute(); const router = useRouter()
const email = ref(''); const password = ref(''); const loading = ref(false)
const isPasswordFocus = ref(false)

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
async function onLogin() {
  loading.value = true
  try { const resp = await login({ email: email.value, password: password.value }); setAuth(resp); notify({ text: '登录成功', color: 'success' }); const redirect = route.query.redirect || '/'; router.push(String(redirect)); } catch (e) { notify({ text: e?.payload?.message || e?.message || '登录失败', color: 'error' }) } finally { loading.value = false }
}
</script>

<template>
  <v-container class="py-8 narrow-container">
    <div class="text-h6 mb-3">登录</div>
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
    <v-text-field v-model="email" label="邮箱" prepend-inner-icon="mail" />
    <v-text-field v-model="password" label="密码" type="password" prepend-inner-icon="key" @focus="isPasswordFocus = true" @blur="isPasswordFocus = false" />
    <div class="mt-3"><v-btn :loading="loading" color="primary" prepend-icon="login" @click="onLogin">登录</v-btn></div>
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

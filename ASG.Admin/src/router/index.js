import { createRouter, createWebHistory } from 'vue-router'
import { isAuthenticated, currentRole } from '../stores/auth'

const routes = [
  { path: '/', name: 'dashboard', component: () => import('../views/DashboardView.vue'), meta: { requiresAdmin: true } },
  { path: '/login', name: 'login', component: () => import('../views/LoginView.vue') },
  { path: '/users', name: 'users', component: () => import('../views/UsersView.vue'), meta: { requiresAdmin: true } },
  { path: '/users/:id', name: 'user-detail', component: () => import('../views/UserDetailView.vue'), meta: { requiresAdmin: true } },
  { path: '/roles', name: 'roles', component: () => import('../views/RolesView.vue'), meta: { requiresAdmin: true } },
  { path: '/events', name: 'events', component: () => import('../views/EventsView.vue'), meta: { requiresAdmin: true } },
  { path: '/teams', name: 'teams', component: () => import('../views/TeamsView.vue'), meta: { requiresAdmin: true } },
  { path: '/matches', name: 'matches', component: () => import('../views/MatchesView.vue'), meta: { requiresAdmin: true } },
  { path: '/articles', name: 'articles', component: () => import('../views/ArticlesView.vue'), meta: { requiresAdmin: true } },
  { path: '/files', name: 'files', component: () => import('../views/FilesView.vue'), meta: { requiresAdmin: true } },
  { path: '/about', name: 'about', component: () => import('../views/AboutView.vue') },
  { path: '/:pathMatch(.*)*', name: 'not-found', component: () => import('../views/NotFoundView.vue') },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

// 管理员守卫：需登录且角色为 Admin 或 SuperAdmin
router.beforeEach((to, from, next) => {
  if (to.meta?.requiresAdmin) {
    if (!isAuthenticated.value) {
      next({ name: 'login', query: { redirect: to.fullPath } })
      return
    }
    const roleVal = currentRole.value?.Value ?? currentRole.value?.value
    if (typeof roleVal === 'number' && roleVal >= 2) {
      next()
      return
    }
    // 未加载角色信息时允许进入，视图内会再次校验；若明确非管理员则阻止
    if (!roleVal) {
      next()
      return
    }
    next({ name: 'login', query: { redirect: to.fullPath } })
  } else {
    next()
  }
})

export default router
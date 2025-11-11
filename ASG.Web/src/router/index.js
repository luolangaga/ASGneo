import { createRouter, createWebHistory } from 'vue-router'
import { isAuthenticated } from '../stores/auth'

const routes = [
  {
    path: '/',
    name: 'home',
    component: () => import('../views/HomeView.vue'),
  },
  {
    path: '/teams/search',
    name: 'team-search',
    component: () => import('../views/TeamSearchView.vue'),
  },
  {
    path: '/events',
    name: 'events-board',
    component: () => import('../views/EventBoardView.vue'),
  },
  {
    path: '/events/create',
    name: 'event-create',
    component: () => import('../views/EventCreateView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/events/:id',
    name: 'event-detail',
    component: () => import('../views/EventDetailView.vue'),
  },
  {
    path: '/events/:id/schedule',
    name: 'event-schedule',
    component: () => import('../views/EventScheduleView.vue'),
  },
  {
    path: '/events/manage',
    name: 'my-events-manage',
    component: () => import('../views/MyEventsView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/articles',
    name: 'articles-board',
    component: () => import('../views/ArticleListView.vue'),
  },
  {
    path: '/articles/create',
    name: 'article-create',
    component: () => import('../views/ArticleCreateView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/articles/:id',
    name: 'article-detail',
    component: () => import('../views/ArticleDetailView.vue'),
  },
  {
    path: '/events/:id/edit',
    name: 'event-edit',
    component: () => import('../views/EventEditView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/teams/create',
    name: 'team-create',
    component: () => import('../views/TeamCreateView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/teams/:id',
    name: 'team-detail',
    component: () => import('../views/TeamDetailView.vue'),
  },
  {
    path: '/teams/edit',
    name: 'team-edit',
    component: () => import('../views/TeamEditView.vue'),
    meta: { requiresAuth: true },
  },
  {
    path: '/login',
    name: 'login',
    component: () => import('../views/LoginView.vue'),
  },
  {
    path: '/register',
    name: 'register',
    component: () => import('../views/RegisterView.vue'),
  },
  {
    path: '/profile',
    name: 'profile',
    component: () => import('../views/ProfileView.vue'),
    meta: { requiresAuth: true },
  },
  // 兜底 404（必须放在最后）
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: () => import('../views/NotFoundView.vue'),
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

// 简单的认证守卫：需要登录的路由跳转到登录页
router.beforeEach((to, from, next) => {
  if (to.meta?.requiresAuth && !isAuthenticated.value) {
    next({ name: 'login', query: { redirect: to.fullPath } })
  } else {
    next()
  }
})

export default router
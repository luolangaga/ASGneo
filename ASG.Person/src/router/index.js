import { createRouter, createWebHistory } from 'vue-router'
import RecruitmentBoardView from '../views/RecruitmentBoardView.vue'
import RecruitmentDetailView from '../views/RecruitmentDetailView.vue'
import ApplyView from '../views/ApplyView.vue'
import MyApplicationsView from '../views/MyApplicationsView.vue'
import PayrollView from '../views/PayrollView.vue'
import OrganizerDashboardView from '../views/OrganizerDashboardView.vue'
import LoginView from '../views/LoginView.vue'
import RegisterView from '../views/RegisterView.vue'
import ChatView from '../views/ChatView.vue'
import NotificationsView from '../views/NotificationsView.vue'
import { isAuthenticated } from '../stores/auth'

const routes = [
  { path: '/', name: 'home', component: RecruitmentBoardView },
  { path: '/recruitments', name: 'recruitments', component: RecruitmentBoardView },
  { path: '/recruitments/:id', name: 'recruitment-detail', component: RecruitmentDetailView },
  { path: '/apply/:id', name: 'apply', component: ApplyView },
  { path: '/me/applications', name: 'my-applications', component: MyApplicationsView },
  { path: '/me/payroll', name: 'payroll', component: PayrollView },
  { path: '/organizer', name: 'organizer', component: OrganizerDashboardView },
  { path: '/login', name: 'login', component: LoginView },
  { path: '/register', name: 'register', component: RegisterView },
  { path: '/chat/:userId?', name: 'chat', component: ChatView },
  { path: '/notifications', name: 'notifications', component: NotificationsView },
  { path: '/:pathMatch(.*)*', name: 'not-found', component: { template: '<v-container class="py-8 page-container"><div class="text-h6">页面未找到</div></v-container>' } },
]

const router = createRouter({ history: createWebHistory(), routes })
router.beforeEach((to, from, next) => {
  const needAuth = ['apply','organizer','my-applications','payroll'].includes(String(to.name))
  if (needAuth && !isAuthenticated.value) next({ name: 'login', query: { redirect: to.fullPath } })
  else next()
})
export default router

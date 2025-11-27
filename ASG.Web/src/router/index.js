import { createRouter, createWebHistory } from 'vue-router'
import { isAuthenticated } from '../stores/auth'

const routes = [
  {
    path: '/',
    name: 'home',
    component: () => import('../views/HomeView.vue'),
    meta: { title: '首页', description: '统一赛事平台首页，快速浏览最新赛事与内容' },
  },
  {
    path: '/notifications',
    name: 'notifications',
    component: () => import('../views/NotificationsView.vue'),
    meta: { requiresAuth: true, title: '通知中心', description: '查看系统通知与提醒', noindex: true },
  },
  {
    path: '/messages/:userId?',
    name: 'messages',
    component: () => import('../views/MessagesView.vue'),
    meta: { requiresAuth: true, title: '信息', description: '查看与发送消息', noindex: true },
  },
  {
    path: '/chat/:userId?',
    redirect: (to) => ({ name: 'messages', params: to.params }),
  },
  {
    path: '/teams/search',
    name: 'team-search',
    component: () => import('../views/TeamSearchView.vue'),
    meta: { title: '综合搜索', description: '跨战队、赛事与文章的统一搜索入口' },
  },
  {
    path: '/events',
    name: 'events-board',
    component: () => import('../views/EventBoardView.vue'),
    meta: { title: '赛事广场', description: '浏览与发现热门赛事、赛程与战报' },
  },
  {
    path: '/events/create',
    name: 'event-create',
    component: () => import('../views/EventCreateView.vue'),
    meta: { requiresAuth: true, title: '创建赛事', description: '创建并发布新的赛事信息', noindex: true },
  },
  {
    path: '/events/:id',
    name: 'event-detail',
    component: () => import('../views/EventDetailView.vue'),
    meta: { title: '赛事详情', description: '查看赛事详情、赛制信息与参赛队伍' },
  },
  {
    path: '/events/:id/schedule',
    name: 'event-schedule',
    component: () => import('../views/EventScheduleView.vue'),
    meta: { title: '赛事赛程', description: '查看赛事时间表与对阵安排' },
  },
  {
    path: '/events/:id/bracket',
    name: 'event-bracket',
    component: () => import('../views/EventBracketView.vue'),
    meta: { title: '赛事赛程图', description: '查看与编辑赛事晋级图' },
  },
  {
    path: '/events/manage',
    name: 'my-events-manage',
    component: () => import('../views/MyEventsView.vue'),
    meta: { requiresAuth: true, title: '我的赛事', description: '管理我创建或参与的赛事', noindex: true },
  },
  {
    path: '/articles',
    name: 'articles-board',
    component: () => import('../views/ArticleListView.vue'),
    meta: { title: '文章列表', description: '浏览最新文章与内容更新' },
  },
  {
    path: '/articles/create',
    name: 'article-create',
    component: () => import('../views/ArticleCreateView.vue'),
    meta: { requiresAuth: true, title: '发布文章', description: '撰写并发布新文章', noindex: true },
  },
  {
    path: '/articles/:id',
    name: 'article-detail',
    component: () => import('../views/ArticleDetailView.vue'),
    meta: { title: '文章详情', description: '查看文章内容、点赞与评论互动', ogType: 'article' },
  },
  {
    path: '/stats',
    name: 'stats-analysis',
    component: () => import('../views/StatsAnalysisView.vue'),
    meta: { title: '数据分析', description: '游戏周统计的可视化分析' },
  },
  {
    path: '/events/:id/edit',
    name: 'event-edit',
    component: () => import('../views/EventEditView.vue'),
    meta: { requiresAuth: true, title: '编辑赛事', description: '编辑赛事基本信息', noindex: true },
  },
  {
    path: '/teams/create',
    name: 'team-create',
    component: () => import('../views/TeamCreateView.vue'),
    meta: { requiresAuth: true, title: '创建战队', description: '创建新战队并完善资料', noindex: true },
  },
  {
    path: '/teams/:id',
    name: 'team-detail',
    component: () => import('../views/TeamDetailView.vue'),
    meta: { title: '战队详情', description: '查看战队成员、战绩与相关新闻' },
  },
  {
    path: '/teams/edit',
    name: 'team-edit',
    component: () => import('../views/TeamEditView.vue'),
    meta: { requiresAuth: true, title: '编辑战队', description: '编辑战队资料与成员信息', noindex: true },
  },
  {
    path: '/join/:token',
    name: 'team-invite-join',
    component: () => import('../views/TeamInviteJoinView.vue'),
    meta: { title: '加入战队', description: '通过邀请链接加入战队' },
  },
  {
    path: '/login',
    name: 'login',
    component: () => import('../views/LoginView.vue'),
    meta: { title: '登录', description: '登录统一赛事平台账户', noindex: true },
  },
  {
    path: '/login/callback',
    name: 'login-callback',
    component: () => import('../views/LoginCallbackView.vue'),
    meta: { title: '登录回调', description: '第三方登录回调处理', noindex: true },
  },
  {
    path: '/oauth/new-account',
    name: 'oauth-new-account',
    component: () => import('../views/OAuthNewAccountView.vue'),
    meta: { title: '完善账号信息', description: '第三方登录后完善资料并绑定', noindex: true },
  },
  {
    path: '/forgot-password',
    name: 'forgot-password',
    component: () => import('../views/ForgotPasswordView.vue'),
    meta: { title: '找回密码', description: '通过邮箱重置账户密码', noindex: true },
  },
  {
    path: '/reset-password',
    name: 'reset-password',
    component: () => import('../views/ResetPasswordView.vue'),
    meta: { title: '重置密码', description: '输入验证码重置密码', noindex: true },
  },
  {
    path: '/terms',
    name: 'terms',
    component: () => import('../views/TermsView.vue'),
    meta: { title: '服务条款', description: '平台使用条款与规定' },
  },
  {
    path: '/privacy',
    name: 'privacy',
    component: () => import('../views/PrivacyView.vue'),
    meta: { title: '隐私政策', description: '隐私政策与数据使用说明' },
  },
  {
    path: '/embed',
    name: 'embed',
    component: () => import('../views/WebEmbedView.vue'),
    meta: { title: '网页嵌入', description: '内嵌浏览器打开外部网页' },
  },
  {
    path: '/register',
    name: 'register',
    component: () => import('../views/RegisterView.vue'),
    meta: { title: '注册', description: '创建统一赛事平台账户', noindex: true },
  },
  {
    path: '/profile',
    name: 'profile',
    component: () => import('../views/ProfileView.vue'),
    meta: { requiresAuth: true, title: '个人主页', description: '查看与管理个人信息', noindex: true },
  },
  {
    path: '/users/:id',
    name: 'user-home',
    component: () => import('../views/UserHomeView.vue'),
    meta: { title: '用户主页', description: '查看用户公开资料与动态' },
  },
  // 兜底 404（必须放在最后）
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: () => import('../views/NotFoundView.vue'),
    meta: { title: '页面未找到', description: '抱歉，您访问的页面不存在', noindex: true },
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

import { createRouter, createWebHistory } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  { path: '/login', component: () => import('@/views/Login.vue') },
  {
    path: '/',
    component: () => import('@/views/Layout.vue'),
    children: [
      { path: '', redirect: '/dashboard' },
      { path: 'dashboard', component: () => import('@/views/Dashboard.vue') },
      { path: 'settings/model', component: () => import('@/views/ModelConfig.vue') },
      { path: 'settings/repo', component: () => import('@/views/Repository.vue') },
      { path: 'settings/schedule', component: () => import('@/views/Schedule.vue') },
      { path: 'review', component: () => import('@/views/ReviewTasks.vue') },
      { path: 'review/result/:id', component: () => import('@/views/ReviewResults.vue') },
      { path: 'review/issues', component: () => import('@/views/IssueBoard.vue') },
      { path: 'system/user', component: () => import('@/views/SysUser.vue') },
    ]
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

router.beforeEach((to, from, next) => {
  const token = localStorage.getItem('cr_token')
  if (to.path !== '/login' && !token) {
    next('/login')
  } else {
    next()
  }
})

export default router

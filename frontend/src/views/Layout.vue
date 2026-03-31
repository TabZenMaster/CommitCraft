<template>
  <el-container class="layout">
    <!-- 侧边栏 -->
    <el-aside width="220px">
      <div class="logo">
        <span class="logo-icon">⚡</span>
        <span class="logo-text">CodeReview</span>
      </div>
      <el-menu :default-active="$route.path" router class="nav-menu">
        <el-menu-item index="/dashboard">
          <span class="nav-icon">📊</span><span>数据概览</span>
        </el-menu-item>
        <el-menu-item index="/settings/model">
          <span class="nav-icon">🤖</span><span>模型配置</span>
        </el-menu-item>
        <el-menu-item index="/settings/repo">
          <span class="nav-icon">📦</span><span>仓库管理</span>
        </el-menu-item>
        <el-menu-item index="/review">
          <span class="nav-icon">🔍</span><span>审核任务</span>
        </el-menu-item>
        <el-menu-item index="/review/issues">
          <span class="nav-icon">🗂</span><span>问题处理台</span>
        </el-menu-item>
        <el-menu-item v-if="isAdmin" index="/system/user">
          <span class="nav-icon">👥</span><span>用户管理</span>
        </el-menu-item>
      </el-menu>
      <div class="sidebar-footer">
        <div class="user-avatar">{{ userInitials }}</div>
        <div class="user-detail">
          <div class="user-name">{{ userName }}</div>
          <div class="user-role">{{ isAdmin ? '管理员' : '用户' }}</div>
        </div>
        <el-button link class="logout-btn" @click="logout" title="退出登录">↩</el-button>
      </div>
    </el-aside>

    <!-- 主内容 -->
    <el-container class="main-container">
      <el-header class="top-header">
        <div class="page-title">{{ pageTitle }}</div>
        <div class="header-right">
          <span class="header-time">{{ currentTime }}</span>
        </div>
      </el-header>
      <el-main class="main-content">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'

const router = useRouter()
const route = useRoute()
const user = JSON.parse(localStorage.getItem('cr_user') || '{}')
const userName = computed(() => user.realName || user.username || '用户')
const isAdmin = computed(() => user.role === 'admin')
const userInitials = computed(() => (user.realName || user.username || 'U').slice(0, 1).toUpperCase())

const pageTitles: Record<string, string> = {
  '/dashboard': '数据概览', '/settings/model': '模型配置',
  '/settings/repo': '仓库管理', '/review': '审核任务',
  '/review/issues': '问题处理台', '/system/user': '用户管理',
}
const pageTitle = computed(() => pageTitles[route.path] || '控制台')

const currentTime = ref('')
let timer: ReturnType<typeof setInterval>
const updateTime = () => {
  const now = new Date()
  currentTime.value = now.toLocaleString('zh-CN', { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
}
onMounted(() => { updateTime(); timer = setInterval(updateTime, 60000) })
onUnmounted(() => clearInterval(timer))

const logout = () => {
  localStorage.clear()
  router.push('/login')
}
</script>

<style scoped>
.layout { height: 100vh; display: flex; }

/* ---- 侧边栏 ---- */
.el-aside {
  background: #1e2a38;
  color: #fff;
  display: flex;
  flex-direction: column;
  box-shadow: 2px 0 12px rgba(0,0,0,0.15);
  z-index: 10;
}

.logo {
  padding: 20px 16px;
  display: flex;
  align-items: center;
  gap: 10px;
  border-bottom: 1px solid rgba(255,255,255,0.08);
}
.logo-icon { font-size: 22px; }
.logo-text { font-size: 17px; font-weight: 700; color: #fff; letter-spacing: 0.5px; }

.nav-menu {
  flex: 1;
  border: none;
  background: transparent;
  padding: 8px 0;
}
:deep(.el-menu-item) {
  margin: 2px 8px;
  border-radius: 8px;
  color: rgba(255,255,255,0.65);
  height: 44px;
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 0 12px !important;
  transition: all 0.2s;
}
:deep(.el-menu-item:hover) {
  background: rgba(255,255,255,0.08);
  color: rgba(255,255,255,0.9);
}
:deep(.el-menu-item.is-active) {
  background: rgba(64,158,255,0.25) !important;
  color: #409eff !important;
  border-left: 3px solid #409eff;
}
.nav-icon { font-size: 16px; flex-shrink: 0; }

/* ---- 侧边栏底部 ---- */
.sidebar-footer {
  padding: 12px 16px;
  border-top: 1px solid rgba(255,255,255,0.08);
  display: flex;
  align-items: center;
  gap: 10px;
}
.user-avatar {
  width: 32px; height: 32px;
  border-radius: 50%;
  background: #409eff;
  color: #fff;
  display: flex; align-items: center; justify-content: center;
  font-size: 14px; font-weight: 600; flex-shrink: 0;
}
.user-detail { flex: 1; min-width: 0; }
.user-name { font-size: 13px; color: rgba(255,255,255,0.85); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.user-role { font-size: 11px; color: rgba(255,255,255,0.4); }
.logout-btn { color: rgba(255,255,255,0.4); font-size: 16px; padding: 4px; }
.logout-btn:hover { color: #f56c6c; }

/* ---- 主内容区 ---- */
.main-container { flex: 1; display: flex; flex-direction: column; min-width: 0; }

.top-header {
  background: #fff;
  border-bottom: 1px solid #f0f0f0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 24px !important;
  height: 56px !important;
  box-shadow: 0 1px 4px rgba(0,0,0,0.05);
}
.page-title { font-size: 16px; font-weight: 600; color: #1e2a38; }
.header-time { font-size: 13px; color: #999; }

.main-content { padding: 20px 24px; background: #f5f7fa; overflow-y: auto; }
</style>

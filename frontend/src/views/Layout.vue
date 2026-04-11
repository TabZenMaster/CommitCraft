<template>
  <div class="layout">
    <!-- 移动端顶部栏 -->
    <header class="mobile-header">
      <button class="hamburger-btn" @click="drawerVisible = !drawerVisible" aria-label="打开菜单">
        <Expand v-if="!drawerVisible" class="hamburger-icon" />
        <Fold v-else class="hamburger-icon" />
      </button>
      <img src="/favicon.svg" class="mobile-header-icon" alt="logo" />
      <div class="mobile-header-title">Commit Craft</div>
      <button class="theme-toggle-btn-mobile" @click="toggleTheme" :title="isDark ? '切换亮色模式' : '切换暗色模式'">
        <Sunny v-if="isDark" class="theme-icon" />
        <Moon v-else class="theme-icon" />
      </button>
    </header>

    <!-- 侧边栏 (桌面端) -->
    <aside class="sidebar">
      <div class="sidebar-logo">
        <img src="/favicon.svg" class="sidebar-logo-img" alt="logo" />
        <span class="sidebar-logo-text">Commit Craft</span>
      </div>

      <nav class="sidebar-menu">
        <div
          v-for="item in menuItems"
          :key="item.path"
          :class="['sidebar-menu-item', { active: isActive(item.path) }]"
          @click="navigate(item.path)"
        >
          <component :is="item.icon" class="sidebar-menu-icon" />
          <span class="sidebar-menu-label">{{ item.label }}</span>
        </div>
      </nav>

      <div class="sidebar-footer">
        <div class="sidebar-footer-avatar">{{ userInitials }}</div>
        <div class="sidebar-footer-info" @click="openProfile" style="cursor:pointer">
          <div class="sidebar-footer-name">{{ userName }}</div>
          <div class="sidebar-footer-role">{{ roleName(user.role) }}</div>
        </div>
        <button class="theme-toggle-btn" @click="toggleTheme" :title="isDark ? '切换亮色模式' : '切换暗色模式'">
          <Sunny v-if="isDark" class="theme-icon" />
          <Moon v-else class="theme-icon" />
        </button>
        <button class="logout-btn" @click="logout" title="退出登录">退出</button>
      </div>
    </aside>

    <!-- 移动端抽屉菜单 (自定义，不使用 el-drawer 以避免样式穿透问题) -->
    <Teleport to="body">
      <div v-if="drawerVisible" class="drawer-overlay" @click.self="drawerVisible = false">
        <div class="drawer-panel">
          <div class="drawer-header">
            <img src="/favicon.svg" class="drawer-logo-img" alt="logo" />
            <span class="drawer-logo-text">Commit Craft</span>
            <button class="drawer-close-btn" @click="drawerVisible = false" aria-label="关闭菜单">
              <Fold class="drawer-close-icon" />
            </button>
          </div>
          <nav class="drawer-menu">
            <div
              v-for="item in menuItems"
              :key="item.path"
              :class="['drawer-menu-item', { active: isActive(item.path) }]"
              @click="navigate(item.path)"
            >
              <component :is="item.icon" class="drawer-menu-icon" />
              <span class="drawer-menu-label">{{ item.label }}</span>
            </div>
          </nav>
          <div class="drawer-footer">
            <div class="drawer-footer-avatar">{{ userInitials }}</div>
            <div class="drawer-footer-info" @click="openProfile">
              <div class="drawer-footer-name">{{ userName }}</div>
              <div class="drawer-footer-role">{{ roleName(user.role) }}</div>
            </div>
            <button class="logout-btn" @click="logout" title="退出登录">退出</button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- 主内容 -->
    <main class="main-container">
      <div class="main-content">
        <router-view />
      </div>
    </main>

    <!-- 移动端底部导航 (仅手机) -->
    <nav class="bottom-nav">
      <div
        v-for="item in bottomNavItems"
        :key="item.path"
        :class="['bottom-nav-item', { active: isActive(item.path) }]"
        @click="navigate(item.path)"
      >
        <component :is="item.icon" class="bottom-nav-icon" />
        <span class="bottom-nav-label">{{ item.label }}</span>
      </div>
    </nav>

    <!-- 个人资料弹窗 -->
    <el-dialog v-model="profileVisible" title="个人资料" width="420px">
      <el-form :model="profileForm" label-width="80px">
        <el-form-item label="用户名">
          <el-input :model-value="profileForm.username" disabled />
        </el-form-item>
        <el-form-item label="姓名">
          <el-input v-model="profileForm.realName" />
        </el-form-item>
        <el-form-item label="Git用户名">
          <el-input v-model="profileForm.gitName" placeholder="与 Git 提交作者名一致" />
        </el-form-item>
        <el-form-item label="角色">
          <el-input :model-value="roleName(profileForm.role)" disabled />
        </el-form-item>
        <el-form-item label="状态">
          <el-input :model-value="Number(profileForm.status) === 1 ? '启用' : '停用'" disabled />
        </el-form-item>
        <el-form-item label="密码" class="pwd-action-item" :label-width="isMobile ? 'auto' : '80px'">
          <el-button type="primary" link @click="openPwdDialog">修改密码</el-button>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="profileVisible = false">取消</el-button>
        <el-button type="primary" @click="saveProfile">保存</el-button>
      </template>
    </el-dialog>

    <!-- 修改密码弹窗 -->
    <el-dialog v-model="pwdVisible" title="修改密码" width="400px">
      <el-form :model="pwdForm" label-width="80px">
        <el-form-item label="原密码" required>
          <el-input v-model="pwdForm.oldPwd" type="password" show-password placeholder="输入原密码" />
        </el-form-item>
        <el-form-item label="新密码" required>
          <el-input v-model="pwdForm.newPwd" type="password" show-password placeholder="至少6位" />
        </el-form-item>
        <el-form-item label="确认密码" required>
          <el-input v-model="pwdForm.confirmPwd" type="password" show-password placeholder="再次输入新密码" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="pwdVisible = false">取消</el-button>
        <el-button type="primary" @click="doChangePwd">确认修改</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import {
  DataAnalysis, Search, Tickets, Clock, CircleCheck,
  Cpu, Box, Calendar, User, Sunny, Moon, Fold, Expand
} from '@element-plus/icons-vue'
import { sysUserApi } from '@/api'
import { refreshTheme } from '@/utils/eventBus'
import { useBreakpoint } from '@/composables/useBreakpoint'

const router = useRouter()
const route = useRoute()

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'xs' || breakpoint.value === 'sm')

// Theme
const isDark = ref(true)

const toggleTheme = () => {
  isDark.value = !isDark.value
  const theme = isDark.value ? 'dark' : 'light'
  document.documentElement.setAttribute('data-theme', theme)
  localStorage.setItem('cr_theme', theme)
  refreshTheme()
}

const initTheme = () => {
  const savedTheme = localStorage.getItem('cr_theme')
  if (savedTheme) {
    isDark.value = savedTheme === 'dark'
  }
  document.documentElement.setAttribute('data-theme', isDark.value ? 'dark' : 'light')
}

// User
const user = JSON.parse(localStorage.getItem('cr_user') || '{}')
const userName = computed(() => user.realName || user.username || '用户')
const userInitials = computed(() => (user.realName || user.username || 'U').slice(0, 1).toUpperCase())

function roleName(role: string) {
  return { admin: '管理员', reviewer: '审核员', developer: '开发者' }[role] || '用户'
}

// Drawer
const drawerVisible = ref(false)

function closeDrawer() {
  drawerVisible.value = false
}

// Menu
const menuItems = computed(() => {
  const items = [
    { path: '/dashboard', label: '数据概览', icon: DataAnalysis },
  ]
  if (user.role === 'admin') {
    items.push(
      { path: '/settings/model', label: '模型配置', icon: Cpu },
      { path: '/settings/repo', label: '仓库管理', icon: Box },
      { path: '/settings/schedule', label: '定时计划', icon: Calendar },
    )
  }
  if (user.role === 'reviewer' || user.role === 'admin') {
    items.push({ path: '/review', label: '审核任务', icon: Search })
  }
  items.push(
    { path: '/review/issues', label: '问题处理台', icon: Tickets },
    { path: '/review/claimed', label: '待处理问题', icon: Clock },
    { path: '/review/processed', label: '已处理问题', icon: CircleCheck },
  )
  if (user.role === 'admin') {
    items.push({ path: '/system/user', label: '用户管理', icon: User })
  }
  return items
})

// Bottom nav items (first 4 key pages for phone nav)
const bottomNavItems = computed(() => {
  const items = [
    { path: '/dashboard', label: '数据概览', icon: DataAnalysis },
    { path: '/review/issues', label: '问题处理台', icon: Tickets },
    { path: '/review/claimed', label: '待处理问题', icon: Clock },
  ]
  return items
})

function isActive(path: string) {
  return route.path === path
}

function navigate(path: string) {
  router.push(path)
  closeDrawer()
}

// Profile
const profileVisible = ref(false)
const profileForm = ref<any>({})
const pwdVisible = ref(false)
const pwdForm = ref({ oldPwd: '', newPwd: '', confirmPwd: '' })

async function openProfile() {
  const res: any = await sysUserApi.me()
  if (res.success) {
    profileForm.value = { ...res.data }
  } else {
    profileForm.value = { ...user }
  }
  profileVisible.value = true
}

function openPwdDialog() {
  pwdForm.value = { oldPwd: '', newPwd: '', confirmPwd: '' }
  pwdVisible.value = true
}

async function doChangePwd() {
  if (!pwdForm.value.oldPwd) { ElMessage.warning('请输入原密码'); return }
  if (!pwdForm.value.newPwd || pwdForm.value.newPwd.length < 6) { ElMessage.warning('新密码至少6位'); return }
  if (pwdForm.value.newPwd !== pwdForm.value.confirmPwd) { ElMessage.warning('两次新密码不一致'); return }
  const res: any = await sysUserApi.changePwd(pwdForm.value.oldPwd, pwdForm.value.newPwd)
  if (res.success) {
    pwdVisible.value = false
    ElMessage.success('密码修改成功，请重新登录')
    localStorage.removeItem('cr_token')
    localStorage.removeItem('cr_user')
    location.href = '/login'
  } else {
    ElMessage.error(res.msg)
  }
}

async function saveProfile() {
  if (!profileForm.value.realName) { ElMessage.warning('姓名不能为空'); return }
  const res: any = await sysUserApi.update({ id: profileForm.value.id, realName: profileForm.value.realName, gitName: profileForm.value.gitName })
  if (res.success) {
    ElMessage.success('保存成功')
    user.realName = profileForm.value.realName
    user.gitName = profileForm.value.gitName
    localStorage.setItem('cr_user', JSON.stringify(user))
    profileVisible.value = false
  } else {
    ElMessage.error(res.msg)
  }
}

// Logout
const logout = () => {
  localStorage.clear()
  router.push('/login')
}

onMounted(() => {
  initTheme()
})
</script>

<style scoped>
.layout {
  height: 100vh;
  display: flex;
  background: var(--bg-page);
}

/* ---- 侧边栏 ---- */
.sidebar {
  background: var(--bg-primary);
  color: var(--text-primary);
  display: flex;
  flex-direction: column;
  width: 220px;
  height: 100vh;
  border-right: 1px solid var(--border-default);
  z-index: 10;
  flex-shrink: 0;
}

.sidebar-logo {
  padding: 16px;
  display: flex;
  align-items: center;
  gap: 10px;
  border-bottom: 1px solid var(--border-default);
}

.sidebar-logo-img {
  width: 26px;
  height: 26px;
  flex-shrink: 0;
}

.sidebar-logo-text {
  font-family: var(--font-display);
  font-size: 14px;
  font-weight: 400;
  color: var(--text-primary);
  text-transform: uppercase;
  letter-spacing: 1.4px;
}

.sidebar-menu {
  flex: 1;
  padding: 8px 0;
  overflow-y: auto;
}

.sidebar-menu-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 0 12px;
  height: 44px;
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.15s ease;
  margin: 2px 8px;
  border-radius: 0;
}

.sidebar-menu-item:hover {
  background: var(--bg-surface-hover);
  color: var(--text-primary);
}

.sidebar-menu-item.active {
  background: var(--bg-elevated);
  color: var(--text-primary);
  border-left: 3px solid var(--text-primary);
}

.sidebar-menu-icon {
  width: 18px;
  height: 18px;
  flex-shrink: 0;
  color: inherit;
}

.sidebar-menu-label {
  font-size: 14px;
  white-space: nowrap;
}

/* ---- 侧边栏底部 ---- */
.sidebar-footer {
  padding: 12px 16px;
  border-top: 1px solid var(--border-default);
  display: flex;
  align-items: center;
  gap: 10px;
}

.sidebar-footer-avatar {
  width: 32px;
  height: 32px;
  border-radius: 0;
  background: var(--bg-surface-hover);
  color: var(--text-primary);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  font-weight: 600;
  flex-shrink: 0;
}

.sidebar-footer-info {
  flex: 1;
  min-width: 0;
}

.sidebar-footer-name {
  font-size: 13px;
  color: var(--text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.sidebar-footer-role {
  font-size: 11px;
  color: var(--text-muted);
}

.theme-toggle-btn {
  background: transparent;
  border: none;
  cursor: pointer;
  padding: 4px 6px;
  transition: opacity 0.15s;
  opacity: 0.7;
  display: flex;
  align-items: center;
}

.theme-icon {
  width: 18px;
  height: 18px;
  color: var(--text-secondary);
}

.theme-toggle-btn:hover {
  opacity: 1;
}

.logout-btn {
  background: transparent;
  border: none;
  color: var(--text-muted);
  font-size: 14px;
  cursor: pointer;
  padding: 4px 8px;
  transition: color 0.15s;
  /* Prevent button from being squished in flex layout */
  flex-shrink: 0;
  font-family: var(--font-display);
}

.logout-btn:hover {
  color: var(--text-primary);
}

/* ---- 主内容区 ---- */
.main-container {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  overflow: hidden;
  height: 100%;
}

.main-content {
  padding: 12px 16px;
  overflow-y: auto;
  flex: 1;
  background: var(--bg-page);
}

/* =========================================
   Mobile Header (visible < 1024px)
   ========================================= */
.mobile-header {
  display: none;
  align-items: center;
  gap: 12px;
  padding: 0 12px;
  height: 52px;
  background: var(--bg-primary);
  border-bottom: 1px solid var(--border-default);
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  z-index: 9;
  flex-shrink: 0;
}

.hamburger-btn {
  background: transparent;
  border: none;
  cursor: pointer;
  padding: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: background 0.15s;
}

.hamburger-btn:hover {
  background: var(--bg-surface-hover);
}

.hamburger-icon {
  width: 20px;
  height: 20px;
  color: var(--text-primary);
}

.mobile-header-title {
  flex: 1;
  font-family: var(--font-display);
  font-size: 13px;
  font-weight: 400;
  text-transform: uppercase;
  letter-spacing: 1.2px;
  color: var(--text-primary);
}

.mobile-header-icon {
  width: 20px;
  height: 20px;
  flex-shrink: 0;
}

.theme-toggle-btn-mobile {
  background: transparent;
  border: none;
  cursor: pointer;
  padding: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: background 0.15s;
}

.theme-toggle-btn-mobile:hover {
  background: var(--bg-surface-hover);
}

.theme-toggle-btn-mobile .theme-icon {
  width: 18px;
  height: 18px;
  color: var(--text-secondary);
}

/* =========================================
   Mobile Drawer
   ========================================= */
.drawer-header {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-default);
}

.drawer-logo-img {
  width: 26px;
  height: 26px;
  flex-shrink: 0;
}

.drawer-logo-text {
  font-family: var(--font-display);
  font-size: 14px;
  font-weight: 400;
  color: var(--text-primary);
  text-transform: uppercase;
  letter-spacing: 1.4px;
}

.drawer-close-btn {
  margin-left: auto;
  background: transparent;
  border: none;
  cursor: pointer;
  padding: 6px;
  display: flex;
  align-items: center;
  border-radius: 4px;
  transition: background 0.15s;
}

.drawer-close-btn:hover {
  background: var(--bg-surface-hover);
}

.drawer-close-icon {
  width: 18px;
  height: 18px;
  color: var(--text-secondary);
}

/* Menu nav: takes remaining space, scrolls internally */
.drawer-menu {
  flex: 1;
  padding: 8px 0;
  overflow-y: auto;
  min-height: 0;
}

.drawer-menu-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 0 16px;
  height: 50px;
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.15s ease;
  margin: 2px 8px;
  border-radius: 0;
}

.drawer-menu-item:hover {
  background: var(--bg-surface-hover);
  color: var(--text-primary);
}

.drawer-menu-item.active {
  background: var(--bg-elevated);
  color: var(--text-primary);
  border-left: 3px solid var(--text-primary);
}

.drawer-menu-icon {
  width: 20px;
  height: 20px;
  flex-shrink: 0;
  color: inherit;
}

.drawer-menu-label {
  font-size: 14px;
  white-space: nowrap;
}

/* Footer: fixed at bottom */
.drawer-footer {
  padding: 12px 16px;
  border-top: 1px solid var(--border-default);
  display: flex;
  align-items: center;
  gap: 10px;
  flex-shrink: 0;
  /* Space for bottom-nav on iOS */
  margin-bottom: env(safe-area-inset-bottom);
}

.drawer-footer-avatar {
  width: 36px;
  height: 36px;
  border-radius: 0;
  background: var(--bg-surface-hover);
  color: var(--text-primary);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  font-weight: 600;
  flex-shrink: 0;
}

.drawer-footer-info {
  flex: 1;
  min-width: 0;
  cursor: pointer;
}

.drawer-footer-name {
  font-size: 13px;
  color: var(--text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.drawer-footer-role {
  font-size: 11px;
  color: var(--text-muted);
}

/* =========================================
   Bottom Navigation (phone only < 768px)
   ========================================= */
.bottom-nav {
  display: none;
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  height: 60px;
  background: var(--bg-primary);
  border-top: 1px solid var(--border-default);
  z-index: 9;
  padding-bottom: env(safe-area-inset-bottom);
  flex-shrink: 0;
}

.bottom-nav-item {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 3px;
  cursor: pointer;
  transition: all 0.15s;
  color: var(--text-muted);
  -webkit-tap-highlight-color: transparent;
}

.bottom-nav-item:hover,
.bottom-nav-item.active {
  color: var(--text-primary);
}

.bottom-nav-icon {
  width: 22px;
  height: 22px;
}

.bottom-nav-label {
  font-size: 10px;
  font-family: var(--font-display);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

/* =========================================
   Responsive: Show mobile elements
   ========================================= */
@media (max-width: 1024px) {
  .mobile-header {
    display: flex;
  }

  .sidebar {
    display: none;
  }

  .main-container {
    width: 100%;
    padding-top: 52px; /* account for fixed mobile header */
  }
}

@media (max-width: 768px) {
  .bottom-nav {
    display: flex;
  }
}

/* =========================================
   Custom Mobile Drawer (replaces el-drawer)
   ========================================= */
.drawer-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  z-index: 2000;
  animation: drawer-fade-in 0.15s ease;
}

@keyframes drawer-fade-in {
  from { opacity: 0; }
  to { opacity: 1; }
}

.drawer-panel {
  position: absolute;
  top: 0;
  left: 0;
  width: 280px;
  height: 100vh;
  height: 100dvh; /* Fallback for iOS Safari */
  background: var(--bg-primary);
  border-right: 1px solid var(--border-default);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  animation: drawer-slide-in 0.2s ease;
}

/* iOS Safari: account for bottom-nav + home indicator */
@supports (-webkit-touch-callout: none) {
  @media (max-width: 768px) {
    .drawer-panel {
      height: 100dvh;
      padding-bottom: env(safe-area-inset-bottom);
    }
  }
}

@keyframes drawer-slide-in {
  from { transform: translateX(-100%); }
  to { transform: translateX(0); }
}

.drawer-panel .drawer-header {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 12px 16px;
  border-bottom: 1px solid var(--border-default);
  flex-shrink: 0;
}

.drawer-panel .drawer-logo-img {
  width: 26px;
  height: 26px;
  flex-shrink: 0;
}

.drawer-panel .drawer-logo-text {
  font-family: var(--font-display);
  font-size: 14px;
  font-weight: 400;
  color: var(--text-primary);
  text-transform: uppercase;
  letter-spacing: 1.4px;
}

.drawer-panel .drawer-close-btn {
  margin-left: auto;
  background: transparent;
  border: none;
  cursor: pointer;
  padding: 6px;
  display: flex;
  align-items: center;
  border-radius: 4px;
  transition: background 0.15s;
}

.drawer-panel .drawer-close-btn:hover {
  background: var(--bg-surface-hover);
}

.drawer-panel .drawer-close-icon {
  width: 18px;
  height: 18px;
  color: var(--text-secondary);
}

/* Menu: takes remaining space, scrolls internally */
.drawer-panel .drawer-menu {
  flex: 1;
  padding: 8px 0;
  overflow-y: auto;
  min-height: 0;
}

.drawer-panel .drawer-menu-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 0 16px;
  height: 50px;
  color: var(--text-secondary);
  cursor: pointer;
  transition: all 0.15s ease;
  margin: 2px 8px;
  border-radius: 0;
}

.drawer-panel .drawer-menu-item:hover {
  background: var(--bg-surface-hover);
  color: var(--text-primary);
}

.drawer-panel .drawer-menu-item.active {
  background: var(--bg-elevated);
  color: var(--text-primary);
  border-left: 3px solid var(--text-primary);
}

.drawer-panel .drawer-menu-icon {
  width: 20px;
  height: 20px;
  flex-shrink: 0;
  color: inherit;
}

.drawer-panel .drawer-menu-label {
  font-size: 14px;
  white-space: nowrap;
}

/* Footer: fixed at bottom */
.drawer-panel .drawer-footer {
  padding: 12px 16px;
  border-top: 1px solid var(--border-default);
  display: flex;
  align-items: center;
  gap: 10px;
  flex-shrink: 0;
  /* Space for bottom-nav on iOS */
  margin-bottom: env(safe-area-inset-bottom);
}

.drawer-panel .drawer-footer-avatar {
  width: 36px;
  height: 36px;
  border-radius: 0;
  background: var(--bg-surface-hover);
  color: var(--text-primary);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  font-weight: 600;
  flex-shrink: 0;
}

.drawer-panel .drawer-footer-info {
  flex: 1;
  min-width: 0;
  cursor: pointer;
}

.drawer-panel .drawer-footer-name {
  font-size: 13px;
  color: var(--text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.drawer-panel .drawer-footer-role {
  font-size: 11px;
  color: var(--text-muted);
}
</style>

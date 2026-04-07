<template>
  <el-container class="layout">
    <!-- 侧边栏 -->
    <el-aside width="220px">
      <div class="logo">
        <span class="logo-icon">⚡</span>
        <span class="logo-text">CommitCraft</span>
      </div>
      <el-menu :default-active="$route.path" router class="nav-menu">
        <el-menu-item index="/dashboard">
          <span class="nav-icon">📊</span><span>数据概览</span>
        </el-menu-item>
        <el-menu-item v-if="isAdmin" index="/settings/model">
          <span class="nav-icon">🤖</span><span>模型配置</span>
        </el-menu-item>
        <el-menu-item v-if="isAdmin" index="/settings/repo">
          <span class="nav-icon">📦</span><span>仓库管理</span>
        </el-menu-item>
        <el-menu-item v-if="isReviewer" index="/settings/schedule">
          <span class="nav-icon">📅</span><span>定时计划</span>
        </el-menu-item>
        <el-menu-item v-if="isReviewer" index="/review">
          <span class="nav-icon">🔍</span><span>审核任务</span>
        </el-menu-item>
        <el-menu-item index="/review/issues">
          <span class="nav-icon">🗂</span><span>问题处理台</span>
        </el-menu-item>
        <el-menu-item index="/review/claimed">
          <span class="nav-icon">⏳</span><span>待处理问题</span>
        </el-menu-item>
        <el-menu-item index="/review/processed">
          <span class="nav-icon">✅</span><span>已处理问题</span>
        </el-menu-item>
        <el-menu-item v-if="isAdmin" index="/system/user">
          <span class="nav-icon">👥</span><span>用户管理</span>
        </el-menu-item>
      </el-menu>
      <div class="sidebar-footer">
        <div class="user-avatar">{{ userInitials }}</div>
        <div class="user-detail" @click="openProfile" style="cursor:pointer">
          <div class="user-name">{{ userName }}</div>
          <div class="user-role">{{ roleName(user.role) }}</div>
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
        <el-form-item label="密码">
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
  </el-container>
</template>

<script setup lang="ts">
import { computed, ref, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { sysUserApi } from '@/api'

const router = useRouter()
const route = useRoute()
const user = JSON.parse(localStorage.getItem('cr_user') || '{}')
const userName = computed(() => user.realName || user.username || '用户')
const isAdmin = computed(() => user.role === 'admin')
const isReviewer = computed(() => user.role === 'reviewer' || user.role === 'admin')
const userInitials = computed(() => (user.realName || user.username || 'U').slice(0, 1).toUpperCase())

function roleName(role: string) {
  return { admin: '管理员', reviewer: '审核员', developer: '开发者' }[role] || '用户'
}

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

const pageTitles: Record<string, string> = {
  '/dashboard': '数据概览', '/settings/model': '模型配置',
  '/settings/repo': '仓库管理', '/settings/schedule': '定时计划', '/review': '审核任务',
  '/review/issues': '问题处理台', '/review/claimed': '待处理问题', '/review/processed': '已处理问题', '/system/user': '用户管理',
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

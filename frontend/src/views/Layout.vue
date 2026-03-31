<template>
  <el-container class="layout">
    <el-aside width="200px">
      <div class="logo">CodeReview</div>
      <el-menu :default-active="$route.path" router>
        <el-menu-item index="/dashboard">
          <span>📊 首页</span>
        </el-menu-item>
        <el-menu-item index="/settings/model">
          <span>🤖 模型配置</span>
        </el-menu-item>
        <el-menu-item index="/settings/repo">
          <span>📦 仓库管理</span>
        </el-menu-item>
        <el-menu-item index="/review">
          <span>🔍 审核任务</span>
        </el-menu-item>
        <el-menu-item index="/review/issues">
          <span>🗂 问题处理台</span>
        </el-menu-item>
        <el-menu-item v-if="isAdmin" index="/system/user">
          <span>👥 用户管理</span>
        </el-menu-item>
      </el-menu>
      <div class="user-info">
        <span>{{ userName }}</span>
        <el-button link @click="logout">退出</el-button>
      </div>
    </el-aside>
    <el-main>
      <router-view />
    </el-main>
  </el-container>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
const router = useRouter()
const user = JSON.parse(localStorage.getItem('cr_user') || '{}')
const userName = computed(() => user.realName || user.username || '')
const isAdmin = computed(() => user.role === 'admin')
const logout = () => {
  localStorage.clear()
  router.push('/login')
}
</script>

<style scoped>
.layout { height: 100vh; }
.el-aside { background: #304156; color: #fff; display: flex; flex-direction: column; }
.logo { padding: 16px; text-align: center; font-size: 18px; font-weight: bold; border-bottom: 1px solid #3a4a5a; }
.el-menu { border: none; background: #304156; }
:deep(.el-menu-item) { color: #bfcbd9; }
:deep(.el-menu-item:hover) { background: #263445; color: #fff; }
:deep(.el-menu-item.is-active) { background: #409EFF !important; color: #fff; }
.user-info { margin-top: auto; padding: 12px; display: flex; align-items: center; justify-content: space-between; font-size: 12px; color: #888; border-top: 1px solid #3a4a5a; }
.el-main { padding: 16px; background: #f0f2f5; overflow-y: auto; }
</style>

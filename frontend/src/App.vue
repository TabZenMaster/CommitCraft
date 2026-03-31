<template>
  <router-view />
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { startSignalR, stopSignalR } from '@/utils/signalr'

const router = useRouter()

// 登录页不连接
const token = localStorage.getItem('cr_token')
if (token) startSignalR()

// 路由切换时：如果切到登录页则断开，否则保持连接
router.afterEach(to => {
  if (to.path === '/login') {
    stopSignalR()
  } else if (localStorage.getItem('cr_token')) {
    startSignalR()
  }
})
</script>

<template>
  <router-view />
</template>

<script setup lang="ts">
import { watch } from 'vue'
import { useRouter } from 'vue-router'
import { startSignalR, stopSignalR } from '@/utils/signalr'

const router = useRouter()

// 监听 token 变化：登录时连接，登出时断开
watch(
  () => localStorage.getItem('cr_token'),
  (token) => {
    if (token) startSignalR()
    else stopSignalR()
  },
  { immediate: true }
)
</script>

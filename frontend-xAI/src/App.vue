<template>
  <router-view />
</template>

<script setup lang="ts">
import { watch } from 'vue'
import { useRouter } from 'vue-router'
import { startSignalR, stopSignalR } from '@/utils/signalr'

const router = useRouter()

// Initialize theme from localStorage or system preference
const initTheme = () => {
  const savedTheme = localStorage.getItem('cr_theme')
  if (savedTheme) {
    document.documentElement.setAttribute('data-theme', savedTheme)
  } else {
    // Default to dark theme
    document.documentElement.setAttribute('data-theme', 'dark')
  }
}

// Listen for token changes: connect on login, disconnect on logout
watch(
  () => localStorage.getItem('cr_token'),
  (token) => {
    if (token) startSignalR()
    else stopSignalR()
  },
  { immediate: true }
)

// Initialize theme on mount
initTheme()
</script>

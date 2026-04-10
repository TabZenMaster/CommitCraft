<template>
  <div class="login-page">
    <div class="login-container">
      <!-- Brand -->
      <div class="login-brand">
        <img src="/favicon.svg" class="brand-icon" alt="logo" />
        <div class="brand-text">
          <h1 class="brand-name">Commit Craft</h1>
          <p class="brand-tagline">AI Code Review Platform</p>
        </div>
      </div>

      <!-- Login Card -->
      <div class="login-card">
        <div class="card-header">
          <span class="card-title">SIGN IN</span>
        </div>

        <form class="login-form" @submit.prevent="handleLogin">
          <div class="form-group">
            <label class="form-label">Username</label>
            <input
              v-model="form.username"
              type="text"
              class="form-input"
              placeholder="Enter your username"
              autocomplete="username"
            />
          </div>

          <div class="form-group">
            <label class="form-label">Password</label>
            <input
              v-model="form.password"
              type="password"
              class="form-input"
              placeholder="Enter your password"
              autocomplete="current-password"
              @keyup.enter="handleLogin"
            />
          </div>

          <button
            type="submit"
            class="btn btn-primary login-btn"
            :class="{ loading: loading }"
            :disabled="loading"
          >
            <span v-if="!loading">LOGIN</span>
            <span v-else>LOADING...</span>
          </button>
        </form>
      </div>

      <!-- Footer -->
      <div class="login-footer">
        <span>Powered by Commit Craft</span>
      </div>
    </div>

    <!-- Theme toggle -->
    <button class="theme-toggle" @click="toggleTheme">
      <Sunny v-if="isDark" class="theme-icon" />
      <Moon v-else class="theme-icon" />
      <span class="theme-text">{{ isDark ? 'LIGHT MODE' : 'DARK MODE' }}</span>
    </button>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { authApi } from '@/api'
import { Sunny, Moon } from '@element-plus/icons-vue'

const router = useRouter()
const form = ref({ username: '', password: '' })
const loading = ref(false)
const isDark = ref(true)

const toggleTheme = () => {
  isDark.value = !isDark.value
  document.documentElement.setAttribute('data-theme', isDark.value ? 'dark' : 'light')
  localStorage.setItem('cr_theme', isDark.value ? 'dark' : 'light')
}

const initTheme = () => {
  const savedTheme = localStorage.getItem('cr_theme')
  if (savedTheme) {
    isDark.value = savedTheme === 'dark'
  }
  document.documentElement.setAttribute('data-theme', isDark.value ? 'dark' : 'light')
}

const handleLogin = async () => {
  if (!form.value.username || !form.value.password) {
    ElMessage.warning('Please enter username and password')
    return
  }
  loading.value = true
  try {
    const res: any = await authApi.login(form.value)
    if (res.success) {
      localStorage.setItem('cr_token', res.data.token)
      localStorage.setItem('cr_user', JSON.stringify(res.data))
      router.push('/')
    } else {
      ElMessage.error(res.msg || 'Login failed')
    }
  } catch {
    ElMessage.error('Login error')
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  initTheme()
})
</script>

<style scoped>
.login-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--bg-primary);
  position: relative;
  padding: 20px 0;
}

@media (max-width: 768px) {
  .login-page {
    align-items: flex-start;
    padding-top: 40px;
  }
}

.login-container {
  width: 100%;
  max-width: 480px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 24px;
  padding: 40px 24px;
}

@media (max-width: 480px) {
  .login-container {
    padding: 32px 16px;
    gap: 20px;
  }

  .login-brand {
    gap: 12px;
  }

  .brand-icon {
    width: 56px;
    height: 56px;
  }

  .brand-name {
    font-size: 20px;
  }

  .login-form {
    padding: 20px 16px;
    gap: 16px;
  }

  .form-input {
    padding: 14px 12px;
    font-size: 16px; /* Prevent iOS zoom on focus */
  }

  .login-btn {
    height: 44px;
  }
}

/* Brand */
.login-brand {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
  text-align: center;
}

.brand-icon {
  width: 72px;
  height: 72px;
  flex-shrink: 0;
}

.brand-text {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.brand-name {
  font-family: var(--font-display);
  font-size: 24px;
  font-weight: 400;
  text-transform: uppercase;
  letter-spacing: 2px;
  color: var(--text-primary);
  margin: 0;
}

.brand-tagline {
  font-family: var(--font-display);
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 1px;
  color: var(--text-muted);
  margin: 0;
}

/* Card */
.login-card {
  width: 100%;
  background: var(--bg-surface);
  border: 1px solid var(--border-default);
}

.card-header {
  padding: 16px 20px;
  border-bottom: 1px solid var(--border-default);
}

.card-title {
  font-family: var(--font-display);
  font-size: 12px;
  font-weight: 400;
  text-transform: uppercase;
  letter-spacing: 1.4px;
  color: var(--text-primary);
}

/* Form */
.login-form {
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.form-label {
  font-family: var(--font-display);
  font-size: 11px;
  font-weight: 400;
  text-transform: uppercase;
  letter-spacing: 1px;
  color: var(--text-muted);
}

.form-input {
  width: 100%;
  padding: 12px 14px;
  font-family: var(--font-body);
  font-size: 14px;
  color: var(--text-primary);
  background: transparent;
  border: 1px solid var(--border-strong);
  outline: none;
  transition: border-color 0.15s, box-shadow 0.15s;
}

.form-input:focus {
  border-color: var(--ring-blue);
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.3);
}

.form-input::placeholder {
  color: var(--text-disabled);
}

/* Button */
.login-btn {
  width: 100%;
  height: 38px;
  font-size: 14px;
  letter-spacing: 1.4px;
  margin-top: 8px;
  border: none;
  cursor: pointer;
  transition: opacity 0.15s;
}

.login-btn:hover:not(:disabled) {
  opacity: 0.85;
}

.login-btn:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

/* Footer */
.login-footer {
  font-family: var(--font-display);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--text-disabled);
}

/* Theme toggle */
.theme-toggle {
  position: fixed;
  top: 20px;
  right: 20px;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 14px;
  background: var(--bg-surface);
  border: 1px solid var(--border-default);
  cursor: pointer;
  transition: border-color 0.15s;
}

.theme-toggle:hover {
  border-color: var(--border-strong);
}

.theme-icon {
  width: 16px;
  height: 16px;
}

.theme-text {
  font-family: var(--font-display);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--text-muted);
}
</style>

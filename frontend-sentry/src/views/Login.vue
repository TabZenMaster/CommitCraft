<template>
  <div class="login-page">
    <div class="login-bg">
      <div class="bg-orb orb1" />
      <div class="bg-orb orb2" />
      <div class="bg-orb orb3" />
    </div>
    <div class="login-wrap">
      <div class="login-brand">
        <div class="brand-icon">⚡</div>
        <h1 class="brand-name">CommitCraft</h1>
        <p class="brand-sub">AI Code Review Platform</p>
      </div>
      <div class="login-card">
        <el-form @submit.prevent="handleLogin" size="large">
          <el-form-item>
            <el-input
              v-model="form.username"
              name="username"
              placeholder="用户名"
              prefix-icon="User"
              clearable
              autocomplete="username"
            />
          </el-form-item>
          <el-form-item>
            <el-input
              v-model="form.password"
              name="password"
              type="password"
              placeholder="密码"
              prefix-icon="Lock"
              show-password
              autocomplete="current-password"
              @keyup.enter="handleLogin"
            />
          </el-form-item>
          <el-button
            type="primary"
            :loading="loading"
            class="login-btn"
            @click="handleLogin"
          >
            登 入
          </el-button>
        </el-form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { authApi } from '@/api'

const router = useRouter()
const form = ref({ username: '', password: '' })
const loading = ref(false)

const handleLogin = async () => {
  if (!form.value.username || !form.value.password) {
    ElMessage.warning('请输入用户名和密码')
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
      ElMessage.error(res.msg || '登录失败')
    }
  } catch {
    ElMessage.error('登录异常')
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login-page {
  height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #0f1623;
  position: relative;
  overflow: hidden;
}

/* 背景光球 */
.login-bg { position: absolute; inset: 0; pointer-events: none; }
.bg-orb {
  position: absolute;
  border-radius: 50%;
  filter: blur(80px);
  opacity: 0.15;
  animation: orbFloat 8s ease-in-out infinite;
}
.orb1 { width: 500px; height: 500px; background: var(--purple-sentry); top: -150px; left: -100px; animation-delay: 0s; }
.orb2 { width: 400px; height: 400px; background: var(--lime); bottom: -100px; right: -80px; animation-delay: -3s; opacity: 0.08; }
.orb3 { width: 300px; height: 300px; background: var(--purple-violet); top: 50%; left: 60%; animation-delay: -5s; }

@keyframes orbFloat {
  0%, 100% { transform: translate(0, 0) scale(1); }
  33% { transform: translate(30px, -20px) scale(1.05); }
  66% { transform: translate(-20px, 15px) scale(0.95); }
}

/* 内容 */
.login-wrap {
  position: relative;
  z-index: 1;
  width: 360px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 24px;
}

.login-brand { text-align: center; color: #fff; }
.brand-icon { font-size: 40px; margin-bottom: 8px; }
.brand-name { font-size: 28px; font-weight: 800; letter-spacing: 1px; margin: 0 0 4px; color: #fff; }
.brand-sub { font-size: 14px; color: rgba(255,255,255,0.45); margin: 0; }

.login-card {
  width: 100%;
  border-radius: 16px;
  border: 1px solid rgba(106, 95, 193, 0.25);
  background: rgba(54, 45, 89, 0.35);
  backdrop-filter: blur(24px) saturate(200%);
  padding: 8px 0;
  box-shadow: 0 8px 40px rgba(0, 0, 0, 0.4), 0 0 0 1px rgba(106, 95, 193, 0.08);
}

:deep(.el-form-item) {
  margin-bottom: 16px;
}

.login-btn {
  width: 100%;
  height: 46px;
  border-radius: var(--radius-xl);
  font-size: 14px;
  font-weight: 700;
  letter-spacing: 1.5px;
  text-transform: uppercase;
  margin-top: 10px;
  background: var(--purple-mid);
  border: 1px solid #584674;
  color: #ffffff;
  box-shadow: var(--shadow-inset);
  transition: box-shadow 0.2s ease, background 0.2s ease, transform 0.15s ease;
}
.login-btn:hover:not(.is-loading) {
  box-shadow: var(--shadow-elevated), 0 0 20px rgba(106, 95, 193, 0.3);
  background: #8b74a8;
  transform: translateY(-2px);
}
.login-btn:active:not(.is-loading) {
  transform: translateY(0);
  box-shadow: var(--shadow-inset);
}
</style>

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
      <el-card class="login-card" shadow="never">
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
      </el-card>
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
  opacity: 0.12;
  animation: orbFloat 8s ease-in-out infinite;
}
.orb1 { width: 500px; height: 500px; background: #409eff; top: -150px; left: -100px; animation-delay: 0s; }
.orb2 { width: 400px; height: 400px; background: #53d1a6; bottom: -100px; right: -80px; animation-delay: -3s; }
.orb3 { width: 300px; height: 300px; background: #764ba2; top: 50%; left: 60%; animation-delay: -5s; }

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
  gap: 20px;
}

.login-brand { text-align: center; color: #fff; }
.brand-icon { font-size: 40px; margin-bottom: 8px; }
.brand-name { font-size: 28px; font-weight: 800; letter-spacing: 1px; margin: 0 0 4px; color: #fff; }
.brand-sub { font-size: 14px; color: rgba(255,255,255,0.45); margin: 0; }

.login-card {
  width: 100%;
  border-radius: 16px;
  border: 1px solid rgba(255,255,255,0.08);
  background: rgba(255,255,255,0.04);
  backdrop-filter: blur(20px);
  padding: 8px 0;
}
:deep(.el-card__body) { padding: 20px 24px; }
:deep(.el-input__wrapper) {
  border-radius: 10px;
  background: rgba(255,255,255,0.06);
  border: 1px solid rgba(255,255,255,0.1);
  box-shadow: none;
  height: 44px;
}
:deep(.el-input__inner) { color: rgba(255,255,255,0.9); }
:deep(.el-input__inner::placeholder) { color: rgba(255,255,255,0.3); }
:deep(.el-input__prefix .el-icon) { color: rgba(255,255,255,0.35); }

.login-btn {
  width: 100%;
  height: 44px;
  border-radius: 10px;
  font-size: 15px;
  font-weight: 600;
  margin-top: 4px;
  background: #409eff;
  border: none;
  transition: opacity 0.2s, transform 0.15s;
}
.login-btn:hover { opacity: 0.88; transform: translateY(-1px); }
.login-btn:active { transform: translateY(0); }
</style>

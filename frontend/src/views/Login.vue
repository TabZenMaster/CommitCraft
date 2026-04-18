<template>
  <div class="login-page">
    <div class="login-container">
      <!-- Brand -->
      <div class="login-brand">
        <img src="/favicon.svg" class="brand-icon" alt="logo" />
        <div class="brand-text">
          <h1 class="brand-name">Commit Craft</h1>
          <p class="brand-tagline">基于 AI 的代码审查工具</p>
        </div>
      </div>

      <!-- Login Card -->
      <div class="login-card">
        <!-- Tabs -->
        <div class="card-tabs">
          <button
            class="tab-btn"
            :class="{ active: activeTab === 'login' }"
            @click="activeTab = 'login'"
          >账号登录</button>
          <button
            class="tab-btn"
            :class="{ active: activeTab === 'quick' }"
            @click="activeTab = 'quick'"
          >快速审核</button>
        </div>

        <!-- Login Form -->
        <div v-if="activeTab === 'login'" class="tab-content">
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

        <!-- Quick Review Form -->
        <div v-if="activeTab === 'quick'" class="tab-content">
          <form class="login-form" @submit.prevent>
            <div class="form-group">
              <label class="form-label">模型API地址</label>
              <input
                v-model="quickForm.modelApiUrl"
                type="text"
                class="form-input"
                placeholder="https://api.example.com/v1/chat/completions"
              />
            </div>

            <div class="form-group">
              <label class="form-label">模型名称</label>
              <input
                v-model="quickForm.modelName"
                type="text"
                class="form-input"
                placeholder="glm-4-flash / gpt-4o-mini / ..."
              />
            </div>

            <div class="form-group">
              <label class="form-label">ApiKey</label>
              <input
                v-model="quickForm.apiKey"
                type="password"
                class="form-input"
                placeholder="AI 接口密钥"
              />
            </div>

            <div class="form-group">
              <label class="form-label">仓库地址</label>
              <input
                v-model="quickForm.repoUrl"
                type="text"
                class="form-input"
                placeholder="https://gitee.com/owner/repo"
              />
            </div>

            <div class="form-group">
              <label class="form-label">Access Token</label>
              <input
                v-model="quickForm.accessToken"
                type="password"
                class="form-input"
                placeholder="Gitee 私人令牌"
              />
            </div>

            <div class="form-actions">
              <button
                type="button"
                class="btn btn-secondary"
                :disabled="testingAi || !canTestAi"
                @click="handleTestAi"
              >
                {{ testingAi ? '测试中...' : '测试接口' }}
              </button>
              <button
                type="button"
                class="btn btn-primary"
                :disabled="reviewing || !canReview"
                @click="handleOpenBranches"
              >
                {{ reviewing ? '审核中...' : '审核' }}
              </button>
            </div>
          </form>

          <!-- Review Result -->
          <div v-if="filesLoading" class="review-loading">
            <el-icon class="is-loading"><Loading /></el-icon> 加载文件列表...
          </div>
          <div v-else-if="filesReviewList.length > 0" class="review-files">
            <div class="review-files-header">
              <span class="review-files-title">审核结果 ({{ filesReviewList.length }} 个文件)</span>
              <span class="review-files-progress">
                已完成: {{ filesReviewList.filter(f => f.status === 'done').length }}
                / {{ filesReviewList.length }}
              </span>
            </div>
            <div
              v-for="(file, idx) in filesReviewList"
              :key="idx"
              class="review-file-item"
            >
              <div class="review-file-header">
                <div class="review-file-info">
                  <span class="review-file-name">{{ file.filename }}</span>
                  <span v-if="file.status === 'reviewing'" class="review-file-status">
                    <el-icon class="is-loading"><Loading /></el-icon> 审核中...
                  </span>
                  <span v-else-if="file.status === 'done'" class="review-file-status done">✓</span>
                  <span v-else-if="file.status === 'error'" class="review-file-status error">✗</span>
                  <span v-else class="review-file-status pending">...</span>
                </div>
              </div>
              <div
                v-if="file.status === 'done'"
                class="review-file-result"
              >
                <!-- 无问题 -->
                <div v-if="file.noIssues" class="review-no-issue">✓ 未发现明显问题</div>
                <!-- 有问题：结构化展示 -->
                <div v-else-if="file.issues.length > 0" class="review-issues">
                  <div
                    v-for="(issue, iIdx) in file.issues"
                    :key="iIdx"
                    class="review-issue-row"
                  >
                    <div class="issue-row-top">
                      <span class="issue-sev" :class="sevClass(issue.severity)">{{ sevName(issue.severity) }}</span>
                      <span class="issue-type" :class="typeClass(issue.issue_type)">{{ typeName(issue.issue_type) }}</span>
                      <span class="issue-lines">{{ issue.line_start || '-' }}~{{ issue.line_end || '-' }}</span>
                      <span class="issue-file">{{ issue.file_path }}</span>
                    </div>
                    <div class="issue-row-body">
                      <div class="issue-desc"><strong>描述：</strong>{{ issue.description }}</div>
                      <div class="issue-suggestion"><strong>修复：</strong>{{ issue.suggestion }}</div>
                    </div>
                  </div>
                </div>
                <!-- 解析失败，显示原文 -->
                <div v-else-if="file.rawResult" class="review-raw" v-html="renderMarkdown(file.rawResult)"></div>
              </div>
              <!-- 错误信息 -->
              <div v-else-if="file.status === 'error'" class="review-file-error">{{ file.rawResult }}</div>
            </div>
          </div>
        </div>
      </div>

      <!-- Footer -->
      <div class="login-footer">
        <span>Powered by Commit Craft</span>
      </div>
    </div>

    <!-- Branch Select Dialog -->
    <el-dialog v-model="branchDialogVisible" title="选择分支" width="400px" destroy-on-close>
      <div v-if="branchLoading" class="branch-loading">
        <el-icon class="is-loading"><Loading /></el-icon> 审核中...
      </div>
      <div v-else-if="branchError" class="branch-error">{{ branchError }}</div>
      <div v-else class="branch-list">
        <div
          v-for="b in branchList"
          :key="b.name"
          class="branch-item"
          @click="handleSelectBranch(b.name)"
        >
          <span class="branch-name">{{ b.name }}</span>
        </div>
      </div>
    </el-dialog>

    <!-- Commit Select Dialog -->
    <el-dialog v-model="commitDialogVisible" :title="'选择提交 - ' + selectedBranch" width="600px" destroy-on-close>
      <div v-if="commitLoading" class="branch-loading">
        <el-icon class="is-loading"><Loading /></el-icon> 审核中...
      </div>
      <div v-else-if="commitError" class="branch-error">{{ commitError }}</div>
      <div v-else class="branch-list">
        <div
          v-for="c in commitList"
          :key="c.fullSha"
          class="branch-item commit-item"
          @click="handleSelectCommit(c)"
        >
          <span class="commit-sha">[{{ c.sha }}]</span>
          <span class="commit-msg">{{ c.shortMsg }}</span>
          <span class="commit-meta">{{ c.author }} · {{ c.date }}</span>
        </div>
      </div>
    </el-dialog>

    <!-- Theme toggle -->
    <button class="theme-toggle" @click="toggleTheme">
      <Sunny v-if="isDark" class="theme-icon" />
      <Moon v-else class="theme-icon" />
      <span class="theme-text">{{ isDark ? 'LIGHT MODE' : 'DARK MODE' }}</span>
    </button>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { Loading } from '@element-plus/icons-vue'
import { authApi } from '@/api'
import { quickApi, type BranchItem, type CommitItem, type FileItem, type QuickReviewResult } from '@/api/quick'
import { Sunny, Moon } from '@element-plus/icons-vue'

const router = useRouter()

// Login
const activeTab = ref<'login' | 'quick'>('login')
const form = ref({ username: '', password: '' })
const loading = ref(false)

// Quick Review
const quickForm = ref({
  modelApiUrl: '',
  modelName: '',
  apiKey: '',
  repoUrl: '',
  accessToken: '',
})
const testingAi = ref(false)
const reviewing = ref(false)
const reviewResult = ref<QuickReviewResult | null>(null)

// Files review state
interface ReviewIssue {
  file_path: string
  line_start: number
  line_end: number
  issue_type: string
  severity: string
  description: string
  suggestion: string
}

interface FileReviewItem {
  filename: string
  patch: string
  status: 'pending' | 'reviewing' | 'done' | 'error'
  rawResult: string
  issues: ReviewIssue[]
  noIssues: boolean
}
const filesReviewList = ref<FileReviewItem[]>([])
const filesLoading = ref(false)

// Branch dialog
const branchDialogVisible = ref(false)
const branchList = ref<BranchItem[]>([])
const branchLoading = ref(false)
const branchError = ref('')

// Commit dialog
const commitDialogVisible = ref(false)
const commitList = ref<CommitItem[]>([])
const commitLoading = ref(false)
const commitError = ref('')
const selectedBranch = ref('')

const isDark = ref(true)

const canTestAi = computed(() =>
  quickForm.value.modelApiUrl.trim() && quickForm.value.modelName.trim()
)

const canReview = computed(() =>
  quickForm.value.modelApiUrl.trim() &&
  quickForm.value.modelName.trim() &&
  quickForm.value.repoUrl.trim()
)

const toggleTheme = () => {
  isDark.value = !isDark.value
  document.documentElement.setAttribute('data-theme', isDark.value ? 'dark' : 'light')
  localStorage.setItem('cr_theme', isDark.value ? 'dark' : 'light')
}

const initTheme = () => {
  const savedTheme = localStorage.getItem('cr_theme')
  if (savedTheme) isDark.value = savedTheme === 'dark'
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

const handleTestAi = async () => {
  testingAi.value = true
  try {
    const res: any = await quickApi.testAi({
      modelApiUrl: quickForm.value.modelApiUrl,
      modelName: quickForm.value.modelName,
      apiKey: quickForm.value.apiKey,
    })
    if (res.success) ElMessage.success('接口可用 ✅')
    else ElMessage.error(res.msg || '接口不可用')
  } catch (e: any) {
    ElMessage.error('连接失败: ' + (e.message || '未知错误'))
  } finally {
    testingAi.value = false
  }
}

const handleOpenBranches = async () => {
  if (!quickForm.value.repoUrl.trim()) {
    ElMessage.warning('请填写仓库地址')
    return
  }
  branchDialogVisible.value = true
  branchLoading.value = true
  branchError.value = ''
  branchList.value = []
  try {
    const res: any = await quickApi.getBranches({
      repoUrl: quickForm.value.repoUrl,
      accessToken: quickForm.value.accessToken,
    })
    if (res.success) {
      branchList.value = res.data || []
      if (branchList.value.length === 0) ElMessage.warning('未找到分支')
    } else {
      branchError.value = res.msg || '获取分支失败'
    }
  } catch (e: any) {
    branchError.value = '获取分支失败: ' + (e.message || '未知错误')
  } finally {
    branchLoading.value = false
  }
}

const handleSelectBranch = async (branchName: string) => {
  branchDialogVisible.value = false
  selectedBranch.value = branchName
  commitDialogVisible.value = true
  commitLoading.value = true
  commitError.value = ''
  commitList.value = []
  try {
    const res: any = await quickApi.getCommits({
      repoUrl: quickForm.value.repoUrl,
      accessToken: quickForm.value.accessToken,
      branchName,
    })
    if (res.success) {
      commitList.value = res.data || []
      if (commitList.value.length === 0) ElMessage.warning('未找到提交记录')
    } else {
      commitError.value = res.msg || '获取提交记录失败'
    }
  } catch (e: any) {
    commitError.value = '获取提交记录失败: ' + (e.message || '未知错误')
  } finally {
    commitLoading.value = false
  }
}

const handleSelectCommit = async (commit: CommitItem) => {
  commitDialogVisible.value = false
  filesLoading.value = true
  reviewing.value = true
  filesReviewList.value = []
  reviewResult.value = null

  // 1. 获取文件列表
  const res: any = await quickApi.getFiles({
    repoUrl: quickForm.value.repoUrl,
    accessToken: quickForm.value.accessToken,
    commitSha: commit.fullSha,
  })

  if (!res.success) {
    ElMessage.error(res.msg || '获取文件列表失败')
    filesLoading.value = false
    reviewing.value = false
    return
  }

  // 初始化文件列表
  filesReviewList.value = (res.data.files || []).map((f: FileItem) => ({
    filename: f.filename,
    patch: f.patch,
    status: 'pending' as const,
    rawResult: '',
    issues: [],
    noIssues: false,
  }))
  filesLoading.value = false

  // 2. 逐个文件审核
  for (const file of filesReviewList.value) {
    file.status = 'reviewing'
    try {
      const r: any = await quickApi.reviewFile({
        modelApiUrl: quickForm.value.modelApiUrl,
        modelName: quickForm.value.modelName,
        apiKey: quickForm.value.apiKey,
        filename: file.filename,
        status: file.status,
        patch: file.patch,
      })
      if (r.success) {
        file.rawResult = r.data.result
        // 解析 pipe 格式
        file.issues = parsePipeResult(r.data.result, file.filename)
        file.noIssues = r.data.result.includes('未发现明显问题')
        file.status = 'done'
      } else {
        file.rawResult = '审核失败: ' + (r.msg || '')
        file.status = 'error'
      }
    } catch (e: any) {
      file.rawResult = '审核失败: ' + (e.message || '未知错误')
      file.status = 'error'
    }
  }
  reviewing.value = false
}

// Simple markdown renderer (basic)
const renderMarkdown = (text: string) => {
  if (!text) return ''
  return text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/\*(.+?)\*/g, '<em>$1</em>')
    .replace(/\n/g, '<br>')
}

// 解析 pipe 分隔格式: 文件名|起始行|结束行|类型|严重程度|问题描述|修复建议
const parsePipeResult = (text: string, fallbackFile: string): ReviewIssue[] => {
  if (!text || text.includes('未发现明显问题')) return []
  const issues: ReviewIssue[] = []
  const lines = text.split('\n')
  for (const line of lines) {
    const trimmed = line.trim()
    if (!trimmed || trimmed === '[]' || trimmed.startsWith('|') || trimmed.startsWith('-')) continue
    const parts = trimmed.split('|')
    if (parts.length < 7) continue
    issues.push({
      file_path: parts[0].trim() || fallbackFile,
      line_start: parseInt(parts[1].trim()) || 0,
      line_end: parseInt(parts[2].trim()) || 0,
      issue_type: parts[3].trim(),
      severity: parts[4].trim(),
      description: parts[5].trim(),
      suggestion: parts[6].trim(),
    })
  }
  return issues
}

const sevName = (s: string) => ({ critical: '致命', major: '严重', minor: '一般', suggestion: '建议' }[s] || s)
const sevClass = (s: string) => ({ critical: 'critical', major: 'major', minor: 'minor', suggestion: 'suggestion' }[s] || '')
const typeName = (t: string) => ({ security: '安全', correctness: '正确性', performance: '性能', maintainability: '可维护性', best_practice: '最佳实践', other: '其他' }[t] || t)
const typeClass = (t: string) => ({ security: 'security', correctness: 'correctness', performance: 'performance', maintainability: 'maintainability', best_practice: 'best-practice', other: 'other' }[t] || '')

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

/* Tabs */
.card-tabs {
  display: flex;
  border-bottom: 1px solid var(--border-default);
}

.tab-btn {
  flex: 1;
  padding: 14px 16px;
  font-family: var(--font-display);
  font-size: 12px;
  font-weight: 400;
  text-transform: uppercase;
  letter-spacing: 1px;
  color: var(--text-muted);
  background: none;
  border: none;
  border-bottom: 2px solid transparent;
  cursor: pointer;
  transition: color 0.15s, border-color 0.15s;
}

.tab-btn:hover {
  color: var(--text-primary);
}

.tab-btn.active {
  color: var(--color-primary);
  border-bottom-color: var(--color-primary);
}

.tab-content {
  padding: 24px;
}

/* Form */
.login-form {
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

.form-actions {
  display: flex;
  gap: 12px;
  margin-top: 8px;
}

.form-actions .btn {
  flex: 1;
}

/* Buttons */
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

.btn {
  height: 38px;
  font-size: 13px;
  letter-spacing: 1px;
  border: none;
  cursor: pointer;
  transition: opacity 0.15s;
}

.btn:disabled {
  cursor: not-allowed;
  opacity: 0.5;
}

.btn-primary {
  background: var(--color-primary);
  color: #fff;
}

.btn-primary:hover:not(:disabled) {
  opacity: 0.85;
}

.btn-secondary {
  background: var(--bg-surface);
  color: var(--text-primary);
  border: 1px solid var(--border-strong);
}

.btn-secondary:hover:not(:disabled) {
  opacity: 0.75;
}

/* Review Result */
.review-result {
  margin-top: 20px;
  padding: 16px;
  background: var(--bg-primary);
  border: 1px solid var(--border-default);
}

.result-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  flex-wrap: wrap;
  gap: 6px;
}

.result-info {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
}

.result-commit {
  font-family: monospace;
  color: var(--color-primary);
  font-weight: 600;
}

.result-branch {
  color: var(--text-primary);
}

.result-committer {
  color: var(--text-muted);
}

.result-time {
  font-size: 12px;
  color: var(--text-muted);
}

.result-body {
  font-size: 13px;
  line-height: 1.6;
  color: var(--text-secondary);
  max-height: 400px;
  overflow-y: auto;
}

/* Branch Dialog */
.branch-loading,
.branch-error {
  padding: 20px;
  text-align: center;
  color: var(--text-muted);
  font-size: 14px;
}

.branch-error {
  color: var(--color-critical);
}

.branch-list {
  max-height: 360px;
  overflow-y: auto;
}

.branch-item {
  padding: 10px 12px;
  cursor: pointer;
  border-bottom: 1px solid var(--border-default);
  transition: background 0.1s;
}

.branch-item:last-child {
  border-bottom: none;
}

.branch-item:hover {
  background: var(--bg-primary);
}

.branch-name {
  font-size: 14px;
  color: var(--text-primary);
}

.review-loading {
  padding: 20px;
  text-align: center;
  color: var(--text-muted);
  font-size: 14px;
}

.review-files {
  margin-top: 20px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.review-files-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 0;
  border-bottom: 1px solid var(--border-default);
}

.review-files-title {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-primary);
}

.review-files-progress {
  font-size: 12px;
  color: var(--text-muted);
}

.review-file-item {
  border: 1px solid var(--border-default);
  background: var(--bg-primary);
}

.review-file-header {
  padding: 8px 12px;
}

.review-file-info {
  display: flex;
  align-items: center;
  gap: 8px;
}

.review-file-name {
  font-family: monospace;
  font-size: 12px;
  color: var(--text-primary);
}

.review-file-status {
  font-size: 12px;
  color: var(--text-muted);
}

.review-file-status.done { color: var(--color-success); }
.review-file-status.error { color: var(--color-critical); }

.review-file-result {
  padding: 8px 12px;
  border-top: 1px solid var(--border-default);
}

.review-no-issue {
  font-size: 13px;
  color: var(--color-success);
  padding: 6px 0;
}

.review-issues {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.review-issue-row {
  border: 1px solid var(--border-default);
  background: var(--bg-surface);
}

.issue-row-top {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 10px;
  border-bottom: 1px solid var(--border-default);
  flex-wrap: wrap;
}

.issue-sev {
  font-size: 11px;
  font-weight: 600;
  padding: 2px 6px;
  border-radius: 3px;
}
.issue-sev.critical { background: rgba(239, 68, 68, 0.2); color: var(--color-critical); }
.issue-sev.major { background: rgba(249, 115, 22, 0.2); color: var(--color-major); }
.issue-sev.minor { background: rgba(234, 179, 8, 0.2); color: #ca8a04; }
.issue-sev.suggestion { background: rgba(59, 130, 246, 0.2); color: var(--color-primary); }

.issue-type {
  font-size: 11px;
  padding: 2px 6px;
  border-radius: 3px;
}
.issue-type.security { background: rgba(239, 68, 68, 0.15); color: #ef4444; }
.issue-type.correctness { background: rgba(249, 115, 22, 0.15); color: #f97316; }
.issue-type.performance { background: rgba(234, 179, 8, 0.15); color: #ca8a04; }
.issue-type.maintainability { background: rgba(59, 130, 246, 0.15); color: #3b82f6; }
.issue-type.best-practice { background: rgba(168, 85, 247, 0.15); color: #a855f7; }

.issue-lines {
  font-family: monospace;
  font-size: 11px;
  color: var(--text-muted);
}

.issue-file {
  font-family: monospace;
  font-size: 11px;
  color: var(--text-secondary);
}

.issue-row-body {
  padding: 6px 10px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.issue-desc, .issue-suggestion {
  font-size: 12px;
  line-height: 1.5;
  color: var(--text-secondary);
}

.issue-desc strong, .issue-suggestion strong {
  color: var(--text-primary);
}

.review-raw {
  font-size: 13px;
  line-height: 1.5;
  color: var(--text-secondary);
}

.review-file-error {
  padding: 8px 12px;
  border-top: 1px solid var(--border-default);
  font-size: 13px;
  color: var(--color-critical);
}

.commit-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 10px 12px;
}

.commit-sha {
  font-family: monospace;
  font-size: 12px;
  color: var(--color-primary);
  font-weight: 600;
}

.commit-msg {
  font-size: 13px;
  color: var(--text-primary);
}

.commit-meta {
  font-size: 11px;
  color: var(--text-muted);
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

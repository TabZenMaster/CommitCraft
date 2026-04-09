<template>
  <el-dialog
    v-model="visible"
    title="AI 分析"
    width="900px"
    :close-on-click-modal="false"
    @close="onClose"
  >
    <div class="ask-layout">
      <!-- 左侧：问题概要 + 提问 -->
      <div class="left-panel">
        <div class="issue-summary">
          <div class="summary-row">
            <span class="label">文件：</span>
            <span class="value file-path">{{ issue.filePath }}</span>
          </div>
          <div class="summary-row" v-if="issue.lineStart">
            <span class="label">行号：</span>
            <span class="value">{{ issue.lineStart }}{{ issue.lineEnd && issue.lineEnd !== issue.lineStart ? `-${issue.lineEnd}` : '' }}</span>
          </div>
          <div class="summary-row">
            <span class="label">类型：</span>
            <el-tag size="small" type="warning">{{ issueTypeLabel }}</el-tag>
            <span class="label" style="margin-left:12px">严重：</span>
            <el-tag size="small" :type="severityType">{{ issue.severity }}</el-tag>
          </div>
          <div class="summary-row">
            <span class="label">描述：</span>
            <span class="value desc">{{ issue.description }}</span>
          </div>
          <div class="code-block" v-if="issue.diffContent">
            <pre><code>{{ diffPreview }}</code></pre>
          </div>
        </div>

        <!-- 询问输入 -->
        <div class="ask-section">
          <el-input
            v-model="question"
            type="textarea"
            :rows="4"
            placeholder="可以追问修复方法、最佳实践、根因分析等..."
            :disabled="loading"
            @keydown.ctrl.enter="handleAsk"
            @keydown.meta.enter="handleAsk"
          />
          <div class="ask-actions">
            <span class="ask-tip">按 Ctrl+Enter 发送</span>
            <el-button type="primary" size="small" @click="handleAsk" :disabled="loading || !question.trim()">
              {{ loading ? '分析中...' : '发送' }}
            </el-button>
          </div>
        </div>
      </div>

      <!-- 右侧：思考区 + 回答区 -->
      <div class="right-panel">
        <!-- AI 思考区 -->
        <div class="answer-section thinking-area" v-if="isThinking" ref="thinkingRef">
          <div class="answer-label loading">
            <span class="el-icon-loading"></span> 思考中...
          </div>
          <div class="thinking-content" v-html="renderedThinking"></div>
        </div>

        <!-- AI 回答 -->
        <div class="answer-section" v-if="answer || loading">
          <div class="answer-label" v-if="!isThinking">AI 回答</div>
          <div class="answer-content markdown-body" ref="answerRef" v-html="renderedAnswer"></div>
        </div>

        <!-- 空状态 -->
        <div class="empty-hint" v-if="!answer && !loading">
          发送问题后 AI 回答将显示在这里
        </div>
      </div>
    </div>


  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onUnmounted } from 'vue'
import { reviewApi } from '@/api'
import { ElMessage } from 'element-plus'
import hljs from 'highlight.js'
import { connection } from '@/utils/signalr'

interface Issue {
  id: number
  filePath: string
  lineStart?: number
  lineEnd?: number
  issueType: string
  severity: string
  description: string
  suggestion: string
  diffContent?: string
}

const props = defineProps<{ issue: Issue; modelValue: boolean }>()
const emit = defineEmits(['update:modelValue'])

const visible = computed({
  get: () => props.modelValue,
  set: v => emit('update:modelValue', v)
})

const question = ref('')
const answer = ref('')
const loading = ref(false)
const isThinking = ref(false)
const thinkingContent = ref('')
const thinkingRef = ref<HTMLElement>()
const answerRef = ref<HTMLElement>()
let wheelHandler: ((e: WheelEvent) => void) | null = null

const issueTypeMap: Record<string, string> = {
  security: '安全',
  correctness: '正确性',
  performance: '性能',
  maintainability: '可维护性',
  best_practice: '最佳实践',
  code_style: '代码风格',
  other: '其他'
}

const severityMap: Record<string, string> = {
  critical: 'danger',
  major: 'warning',
  minor: 'info',
  suggestion: 'info'
}

const issueTypeLabel = computed(() => issueTypeMap[props.issue.issueType] || props.issue.issueType)
const severityType = computed(() => severityMap[props.issue.severity] || 'info')

const diffPreview = computed(() => {
  const d = props.issue.diffContent || ''
  return d.length > 600 ? d.slice(0, 600) + '\n... (已截断)' : d
})

const renderedThinking = computed(() => {
  if (!thinkingContent.value) return ''
  return thinkingContent.value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/\n/g, '<br>')
})

const renderedAnswer = computed(() => {
  if (!answer.value) return ''
  let html = answer.value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/```(\w*)\n?([\s\S]*?)```/g, (_, lang, code) => {
      try {
        if (lang && hljs.getLanguage(lang)) {
          return `<pre><code class="hljs language-${lang}">${hljs.highlight(code.trim(), { language: lang, ignoreIllegals: true }).value}</code></pre>`
        }
        return `<pre><code>${code.trim()}</code></pre>`
      } catch {
        return `<pre><code>${code.trim()}</code></pre>`
      }
    })
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
    .replace(/^### (.+)$/gm, '<h4>$1</h4>')
    .replace(/^## (.+)$/gm, '<h3>$1</h3>')
    .replace(/^# (.+)$/gm, '<h2>$1</h2>')
    .replace(/^---$/gm, '<hr>')
    .replace(/^(\d+)\. (.+)$/gm, '<li><span class="list-num">$1.</span> $2</li>')
    .replace(/^- (.+)$/gm, '<li>$1</li>')
    .replace(/\n/g, '<br>')
  return html
})

async function handleAsk() {
  if (!question.value.trim() || loading.value) return
  loading.value = true
  answer.value = ''

  // 流式：监听 SignalR token 推送
  const conn = connection
  if (!conn) {
    ElMessage.error('SignalR 未连接，请刷新页面后重试')
    loading.value = false
    return
  }

  let inThinking = false
  let thinkBuffer = ''
  let done = false

  const cleanup = () => {
    if (done) return
    done = true
    conn.off('ReceiveAiStreamToken', onToken)
    conn.off('ReceiveAiStreamEnd', onEnd)
    loading.value = false
    // 延迟清理 thinking 状态，等待 pending token 处理完毕
    setTimeout(() => { isThinking.value = false }, 0)
  }

    const onToken = (token: string) => {
    if (done) return

    // 分支1：正在累积 thinking 内容
    if (inThinking) {
      if (token.startsWith('<think>\n')) {
        // 新 thinking 块开始（上一个已闭合但 inThinking 未重置）
        if (thinkBuffer) {
          answer.value += thinkBuffer
        }
        thinkBuffer = token.slice(8)
        thinkingContent.value = thinkBuffer
      } else if (token.includes('</think>')) {
        // thinking 结束
        const idx = token.indexOf('</think>')
        thinkBuffer += token.slice(0, idx)
        thinkingContent.value = thinkBuffer
        inThinking = false
        thinkBuffer = ''
        isThinking.value = false
        const afterEnd = token.slice(idx + 8)
        if (afterEnd) answer.value += afterEnd
      } else {
        // 继续累积 thinking
        thinkBuffer += token
        thinkingContent.value = thinkBuffer
      }
      return
    }

    // 分支2：不在 thinking 状态
    if (token.startsWith('<think>\n')) {
      const content = token.slice(8)
      thinkBuffer = content
      thinkingContent.value = thinkBuffer
      inThinking = true
      isThinking.value = true
    } else if (token.includes('</think>')) {
      const idx = token.indexOf('</think>')
      const beforeEnd = token.slice(0, idx)
      const afterEnd = token.slice(idx + 8)
      if (beforeEnd) answer.value += beforeEnd
      if (afterEnd) answer.value += afterEnd
    } else {
      answer.value += token
    }
  }
const onEnd = () => {
    // 如果 thinking 从未收到 </think> 就结束了，buffer 里是未闭合的 thinking 内容，转为 answer
    if (inThinking && thinkBuffer) {
      answer.value += thinkBuffer
    }
    cleanup()
  }

  conn.on('ReceiveAiStreamToken', onToken)
  conn.on('ReceiveAiStreamEnd', onEnd)

  try {
    const res: any = await reviewApi.ask(props.issue.id, question.value, conn.connectionId || '')
    if (res.code !== 0 && !res.success) {
      ElMessage.error(res.msg || 'AI 分析失败')
      cleanup()
    }
  } catch {
    ElMessage.error('网络错误')
    cleanup()
  }
}

function onClose() {
  question.value = ''
  answer.value = ''
  loading.value = false
  isThinking.value = false
  thinkingContent.value = ''
}

watch(visible, v => {
  if (!v) onClose()
})

// 阻止用户滚轮滚动 thinking 区域（wheel.isTrusted = false 时放过，保证 JS scrollTop 正常）
function attachWheelHandler(el: HTMLElement) {
  if (wheelHandler) el.removeEventListener('wheel', wheelHandler)
  wheelHandler = (e: WheelEvent) => { e.isTrusted && e.preventDefault() }
  el.addEventListener('wheel', wheelHandler, { passive: false })
}

onUnmounted(() => {
  if (wheelHandler && thinkingRef.value) {
    thinkingRef.value.removeEventListener('wheel', wheelHandler)
    wheelHandler = null
  }
})

watch(thinkingContent, () => {
  nextTick(() => {
    if (thinkingRef.value) {
      attachWheelHandler(thinkingRef.value)
      thinkingRef.value.scrollTop = thinkingRef.value.scrollHeight
    }
  })
})

// 回答区自动滚动到底部
watch(answer, () => {
  nextTick(() => {
    if (answerRef.value) {
      answerRef.value.scrollTop = answerRef.value.scrollHeight
    }
  })
})
</script>

<style scoped>
.ask-layout {
  display: flex;
  gap: 16px;
  height: 520px;
}

.left-panel {
  width: 320px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 12px;
  overflow-y: auto;
}

.right-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 8px;
  min-width: 0;
  height: 100%;
}

.empty-hint {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #c0c4cc;
  font-size: 13px;
  border: 1px dashed #dcdfe6;
  border-radius: 6px;
  min-height: 80px;
}

.issue-summary {
  background: #f5f7fa;
  border-radius: 6px;
  padding: 12px 16px;
  font-size: 13px;
}

.summary-row {
  display: flex;
  align-items: flex-start;
  margin-bottom: 6px;
  flex-wrap: wrap;
  gap: 4px;
}

.summary-row:last-child { margin-bottom: 0; }

.label {
  color: #909399;
  min-width: 42px;
}

.value { color: #303133; }
.file-path { font-family: monospace; font-size: 12px; }
.desc { color: #e6a23c; font-weight: 500; }

.code-block {
  margin-top: 8px;
  background: #1e1e1e;
  border-radius: 4px;
  padding: 8px 12px;
  max-height: 160px;
  overflow-y: auto;
}

.code-block pre {
  margin: 0;
  font-size: 12px;
  color: #abb2bf;
  font-family: 'Fira Code', monospace;
  white-space: pre-wrap;
  word-break: break-all;
}

.ask-section { margin-bottom: 0; }
.ask-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 8px;
}
.ask-tip {
  font-size: 12px;
  color: #c0c4cc;
}

.answer-section {
  background: #f0f9eb;
  border-radius: 6px;
  padding: 12px 16px;
  min-height: 80px;
  display: flex;
  flex-direction: column;
  flex: 1;
  overflow: hidden;
}

.thinking-area {
  background: #fdf6ec;
  max-height: 120px;
  overflow-y: auto;
  scrollbar-width: thin;
  scrollbar-color: #dcdfe6 transparent;
  flex-shrink: 0;
}

.thinking-content {
  font-size: 12px;
  color: #909399;
  font-family: monospace;
  white-space: pre-wrap;
  word-break: break-all;
}

.answer-label {
  font-size: 12px;
  color: #67c23a;
  font-weight: 600;
  margin-bottom: 8px;
  flex-shrink: 0;
}
.answer-label.loading { color: #909399; font-weight: 400; }

.answer-content {
  flex: 1;
  font-size: 13px;
  color: #303133;
  line-height: 1.8;
  overflow-y: auto;
  min-height: 0;
}

.answer-content :deep(pre) {
  background: #1e1e1e;
  border-radius: 4px;
  padding: 10px 14px;
  margin: 8px 0;
  overflow-x: auto;
}

.answer-content :deep(code) {
  font-family: 'Fira Code', monospace;
  font-size: 12px;
  color: #abb2bf;
}

.answer-content :deep(pre code) {
  color: #abb2bf;
}

/* Markdown 渲染样式 */
.answer-content :deep(h2) {
  font-size: 16px;
  font-weight: 700;
  color: #303133;
  margin: 12px 0 8px;
  border-bottom: 1px solid #ebeef5;
  padding-bottom: 4px;
}

.answer-content :deep(h3) {
  font-size: 14px;
  font-weight: 600;
  color: #303133;
  margin: 10px 0 6px;
}

.answer-content :deep(h4) {
  font-size: 13px;
  font-weight: 600;
  color: #606266;
  margin: 8px 0 4px;
}

.answer-content :deep(hr) {
  border: none;
  border-top: 1px solid #dcdfe6;
  margin: 12px 0;
}

.answer-content :deep(li) {
  margin: 4px 0 4px 20px;
  line-height: 1.8;
}

.answer-content :deep(.list-num) {
  color: #67c23a;
  font-weight: 600;
  margin-right: 4px;
}

.answer-content :deep(blockquote) {
  border-left: 3px solid #67c23a;
  padding-left: 12px;
  color: #606266;
  margin: 8px 0;
}

.answer-content :deep(table) {
  border-collapse: collapse;
  width: 100%;
  margin: 8px 0;
  font-size: 12px;
}

.answer-content :deep(th),
.answer-content :deep(td) {
  border: 1px solid #dcdfe6;
  padding: 6px 10px;
  text-align: left;
}

.answer-content :deep(th) {
  background: #f5f7fa;
  font-weight: 600;
}

.answer-content :deep(p) {
  margin: 6px 0;
}

/* 列表用 <br> 分隔时伪装成段落 */
.answer-content :deep(li + li) {
  margin-top: 2px;
}
</style>

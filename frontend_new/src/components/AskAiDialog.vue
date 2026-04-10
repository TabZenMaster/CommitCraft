<template>
  <el-dialog
    v-model="visible"
    title="AI 分析"
    :width="isMobile ? '100vw' : '900px'"
    :close-on-click-modal="false"
    @close="onClose"
  >
    <!-- 桌面端布局 -->
    <div v-if="!isMobile" class="ask-layout">
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
            <el-tag size="small" :type="severityType">{{ severityLabel }}</el-tag>
          </div>
          <div class="summary-row">
            <span class="label">描述：</span>
            <span class="value desc">{{ issue.description }}</span>
          </div>
          <div class="code-block" v-if="issue.diffContent">
            <pre><code>{{ diffPreview }}</code></pre>
          </div>
        </div>

        <div class="ask-section">
          <el-input
            v-model="question"
            type="textarea"
            :rows="4"
            placeholder="可以追问修复方法，最佳实践、根因分析等..."
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

      <div class="right-panel">
        <div class="answer-section thinking-area" v-if="isThinking" ref="thinkingRef">
          <div class="answer-label loading">
            <span class="el-icon-loading"></span> 思考中...
          </div>
          <div class="thinking-content" v-html="renderedThinking"></div>
        </div>

        <div class="answer-section" v-if="answer || loading">
          <div class="answer-label">AI 回答</div>
          <div class="answer-content markdown-body" ref="answerRef" v-html="renderedAnswer"></div>
        </div>

        <div class="empty-hint" v-if="!answer && !loading">
          发送问题后 AI 回答将显示在这里
        </div>
      </div>
    </div>

    <!-- 移动端布局：单列 -->
    <div v-else class="ask-layout-mobile">
      <!-- 问题概要 -->
      <div class="mobile-issue-summary">
        <div class="mobile-issue-row">
          <span class="mobile-issue-value file-path">{{ issue.filePath }}</span>
        </div>
        <div class="mobile-issue-row" v-if="issue.lineStart">
          <span class="mobile-issue-label">行号</span>
          <span class="mobile-issue-value">{{ issue.lineStart }}{{ issue.lineEnd && issue.lineEnd !== issue.lineStart ? `-${issue.lineEnd}` : '' }}</span>
          <span style="margin-left:auto;display:flex;gap:4px;flex-shrink:0">
            <el-tag size="small" type="warning">{{ issueTypeLabel }}</el-tag>
            <el-tag size="small" :type="severityType">{{ severityLabel }}</el-tag>
          </span>
        </div>
        <div class="mobile-issue-row">
          <span class="mobile-issue-label">描述</span>
          <span class="mobile-issue-value desc">{{ issue.description }}</span>
        </div>
      </div>

      <!-- 思考区 -->
      <div class="mobile-thinking" v-if="isThinking" ref="thinkingRef">
        <div class="mobile-thinking-header">
          <span class="el-icon-loading"></span> 思考中...
        </div>
        <div class="thinking-content" v-html="renderedThinking"></div>
      </div>

      <!-- 回答区 -->
      <div class="mobile-answer" v-show="answer || loading">
        <div class="mobile-answer-header">AI 回答</div>
        <div class="answer-content markdown-body" ref="answerRef" v-html="renderedAnswer"></div>
      </div>

      <!-- 空状态 -->
      <div class="mobile-empty" v-show="!answer && !loading">
        发送问题后 AI 回答将显示在这里
      </div>

      <!-- 输入区 -->
      <div class="mobile-input-area">
        <el-input
          v-model="question"
          type="textarea"
          :rows="3"
          placeholder="可以追问修复方法..."
          :disabled="loading"
          @keydown.ctrl.enter="handleAsk"
          @keydown.meta.enter="handleAsk"
        />
        <el-button type="primary" class="mobile-send-btn" @click="handleAsk" :disabled="loading || !question.trim()">
          {{ loading ? '分析中...' : '发送' }}
        </el-button>
      </div>
    </div>
  </el-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick, onUnmounted } from 'vue'
import { reviewApi } from '@/api'
import { HubConnectionState } from '@microsoft/signalr'
import { ElMessage } from 'element-plus'
import hljs from 'highlight.js'
import { connection } from '@/utils/signalr'
import { useBreakpoint } from '@/composables/useBreakpoint'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'xs' || breakpoint.value === 'sm')

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

const issueTypeLabel = computed(() => {
  const map: Record<string, string> = { security: '安全', correctness: '正确性', performance: '性能', maintainability: '可维护性', best_practice: '最佳实践', code_style: '代码风格', other: '其他' }
  return map[props.issue.issueType] || props.issue.issueType
})

const severityType = computed(() => {
  const map: Record<string, string> = { critical: 'danger', major: 'warning', minor: 'info', suggestion: 'info' }
  return map[props.issue.severity] || 'info'
})

const severityLabel = computed(() => {
  const map: Record<string, string> = { critical: '严重', major: '重要', minor: '一般', suggestion: '建议' }
  return map[props.issue.severity] || props.issue.severity
})

const diffPreview = computed(() => {
  const content = props.issue.diffContent || ''
  const lines = content.split('\n')
  return lines.slice(0, 8).join('\n') + (lines.length > 8 ? '\n...' : '')
})

const renderedThinking = computed(() => {
  const content = thinkingContent.value
  if (!content) return ''
  return content
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/\n/g, '<br>')
})

const renderedAnswer = computed(() => {
  const raw = answer.value
  if (!raw) return ''

  // Step 1: protect code blocks — extract and replace with placeholder, process later
  const codeBlocks: string[] = []
  let html = raw.replace(/```[\w]*\n?([\s\S]*?)```/g, (_, code) => {
    try {
      const highlighted = hljs.highlightAuto(code).value
      const placeholder = `CODEBLOCK_PLACEHOLDER_${codeBlocks.length}_END`
      codeBlocks.push(`<pre><code>${highlighted}</code></pre>`)
      return placeholder
    } catch {
      const placeholder = `CODEBLOCK_PLACEHOLDER_${codeBlocks.length}_END`
      codeBlocks.push(`<pre><code>${code}</code></pre>`)
      return placeholder
    }
  })

  // Step 2: escape HTML (except inside code blocks which are now placeholders)
  html = html
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')

  // Step 3: inline code
  html = html.replace(/`([^`]+)`/g, '<code>$1</code>')

  // Step 4: headings
  html = html.replace(/^### (.+)$/gm, '<h4>$1</h4>')
  html = html.replace(/^## (.+)$/gm, '<h3>$1</h3>')
  html = html.replace(/^# (.+)$/gm, '<h2>$1</h2>')

  // Step 5: bold
  html = html.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')

  // Step 6: blockquotes
  html = html.replace(/^&gt; (.+)$/gm, '<blockquote>$1</blockquote>')

  // Step 7: HR
  html = html.replace(/^---$/gm, '<hr>')

  // Step 7b: tables — process whole table at once (multiline)
  html = html.replace(/^(\|.+\|)\n(\|[-:| ]+\|)\n((?:\|.+\|\n?)*)/gm, (_, headerRow, separatorRow, bodyRows) => {
    const header = headerRow.split('|').slice(1, -1).map((c: string) => `<th>${c.trim()}</th>`).join('')
    const body = bodyRows.trim().split('\n').map((row: string) => {
      const cells = row.split('|').slice(1, -1).map((c: string) => `<td>${c.trim()}</td>`).join('')
      return `<tr>${cells}</tr>`
    }).join('')
    return `<table><thead><tr>${header}</tr></thead><tbody>${body}</tbody></table>`
  })

  // Step 8: list items
  html = html.replace(/^[-*] (.+)$/gm, '<li>$1</li>')
  html = html.replace(/^\d+\. (.+)$/gm, '<li class="list-num">$1</li>')

  // Step 9: restore code blocks
  codeBlocks.forEach((block, i) => {
    html = html.replace(`CODEBLOCK_PLACEHOLDER_${i}_END`, block)
  })

  // Step 10: wrap remaining lines in <p>
  const lines = html.split(/\n/)
  const paragraphs = lines.map(line => {
    if (
      line.startsWith('<h') ||
      line.startsWith('<pre') ||
      line.startsWith('<li') ||
      line.startsWith('<blockquote') ||
      line.startsWith('<hr') ||
      line.startsWith('<table') ||
      line.startsWith('<tr')
    ) {
      return line
    }
    return line.trim() ? `<p>${line}</p>` : ''
  })

  return paragraphs.join('')
})

// 保证 v-html 永远有内容，不会返回 undefined
const safeAnswer = computed(() => renderedAnswer.value || '<p style="color:var(--text-muted);font-size:13px">正在接收回答...</p>')

let wheelHandler: ((e: WheelEvent) => void) | null = null

async function handleAsk() {
  if (!question.value.trim() || loading.value) return

  let conn = connection
  // 如果连接还未建立，等待一下
  if (!conn || conn.state !== HubConnectionState.Connected) {
    ElMessage.warning('正在建立 AI 连接...')
    // 等待最多 3 秒
    for (let i = 0; i < 30; i++) {
      await new Promise(r => setTimeout(r, 100))
      conn = connection
      if (conn && conn.state === HubConnectionState.Connected) break
    }
    if (!conn || conn.state !== HubConnectionState.Connected) {
      ElMessage.error('AI 连接未建立，请刷新页面后重试')
      return
    }
  }

  loading.value = true
  answer.value = ''
  isThinking.value = false
  thinkingContent.value = ''

  let inThinking = false
  let thinkBuffer = ''

  const onToken = (_: any, ...args: any[]) => {
    // SignalR 传参：可能是 on(token) 或 on(arg1, arg2)，统一取第一个参数
    const raw = args[0]
    console.log('[AI Stream] raw type:', typeof raw, ', value:', JSON.stringify(raw).slice(0, 100))
    const text: string = typeof raw === 'string'
      ? raw
      : Array.isArray(raw)
        ? String(raw[0] ?? '')
        : typeof raw === 'object' && raw !== null
          ? String(raw.arguments?.[0] ?? '')
          : String(raw ?? '')

    console.log('[AI Stream] text:', JSON.stringify(text).slice(0, 80))

    if (text.startsWith('<think>')) {
      inThinking = true
      thinkBuffer = text.slice(8)
      thinkingContent.value = thinkBuffer
      isThinking.value = true
      console.log('[AI Stream] thinking started, buffer:', thinkBuffer.slice(0, 50))
    } else if (text.includes('</think>')) {
      const idx = text.indexOf('</think>')
      thinkBuffer += text.slice(0, idx)
      thinkingContent.value = thinkBuffer
      inThinking = false
      thinkBuffer = ''
      isThinking.value = false
      const afterEnd = text.slice(idx + 8)
      if (afterEnd) answer.value += afterEnd
      console.log('[AI Stream] thinking ended, answer now:', answer.value.slice(0, 50))
    } else if (inThinking) {
      thinkBuffer += text
      thinkingContent.value = thinkBuffer
    } else {
      answer.value += text
      console.log('[AI Stream] answer appended, total:', answer.value.length)
    }
  }

  const onEnd = () => {
    console.log('[AI Stream] onEnd called, answer:', answer.value.slice(0, 100))
    if (inThinking && thinkBuffer) {
      answer.value += thinkBuffer
    }
    cleanup()
  }

  const cleanup = () => {
    loading.value = false
    conn.off('ReceiveAiStreamToken', traceToken)
    conn.off('ReceiveAiStreamEnd', traceEnd)
  }

  const traceToken = (...args: any[]) => {
    console.log('[SignalR Trace] ReceiveAiStreamToken invoked, args length:', args.length, 'first:', JSON.stringify(args[0]).slice(0, 100))
    onToken(undefined, ...args)
  }
  const traceEnd = (...args: any[]) => {
    console.log('[SignalR Trace] ReceiveAiStreamEnd invoked, args length:', args.length)
    onEnd()
  }

  conn.on('ReceiveAiStreamToken', traceToken)
  conn.on('ReceiveAiStreamEnd', traceEnd)

  console.log('[AI Ask] conn state:', conn.state, 'connId:', conn.connectionId)
  console.log('[AI Ask] registered handlers, now calling reviewApi.ask')

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
  color: var(--text-muted);
  font-size: 13px;
  border: 1px dashed var(--border-default);
  border-radius: 6px;
  min-height: 80px;
}

.issue-summary {
  background: var(--bg-surface);
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
  color: var(--text-muted);
  min-width: 42px;
}

.value { color: var(--text-primary); }
.file-path { font-family: monospace; font-size: 12px; color: var(--text-primary); }
.desc { color: var(--color-major); font-weight: 500; }

.code-block {
  margin-top: 8px;
  background: var(--bg-primary);
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
  color: var(--text-muted);
}

.answer-section {
  background: var(--bg-surface);
  border-radius: 6px;
  padding: 12px 16px;
  min-height: 80px;
  display: flex;
  flex-direction: column;
  flex: 1;
  overflow: hidden;
}

.thinking-area {
  background: var(--bg-elevated);
  max-height: 120px;
  overflow-y: auto;
  scrollbar-width: thin;
  scrollbar-color: var(--border-default) transparent;
  flex-shrink: 0;
}

.thinking-content {
  font-size: 12px;
  color: var(--text-muted);
  font-family: monospace;
  white-space: pre-wrap;
  word-break: break-all;
}

.answer-label {
  font-size: 12px;
  color: var(--color-success);
  font-weight: 600;
  margin-bottom: 8px;
  flex-shrink: 0;
}
.answer-label.loading { color: var(--text-muted); font-weight: 400; }

.answer-content {
  flex: 1;
  font-size: 13px;
  color: var(--text-primary);
  line-height: 1.8;
  overflow-y: auto;
  min-height: 0;
}

.answer-content :deep(pre) {
  background: var(--bg-primary);
  border-radius: 4px;
  padding: 10px 14px;
  margin: 8px 0;
  overflow-x: auto;
}

.answer-content :deep(code) {
  font-family: 'Fira Code', monospace;
  font-size: 12px;
  color: var(--text-primary);
}

.answer-content :deep(pre code) {
  color: var(--text-primary);
}

.answer-content :deep(h2) {
  font-size: 16px;
  font-weight: 700;
  color: var(--text-primary);
  margin: 12px 0 8px;
  border-bottom: 1px solid var(--border-default);
  padding-bottom: 4px;
}

.answer-content :deep(h3) {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
  margin: 10px 0 6px;
}

.answer-content :deep(h4) {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-secondary);
  margin: 8px 0 4px;
}

.answer-content :deep(hr) {
  border: none;
  border-top: 1px solid var(--border-default);
  margin: 12px 0;
}

.answer-content :deep(li) {
  margin: 4px 0 4px 20px;
  line-height: 1.8;
}

.answer-content :deep(.list-num) {
  color: var(--color-success);
  font-weight: 600;
  margin-right: 4px;
}

.answer-content :deep(blockquote) {
  border-left: 3px solid var(--color-success);
  padding-left: 12px;
  color: var(--text-secondary);
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
  border: 1px solid var(--border-default);
  padding: 6px 10px;
  text-align: left;
}

.answer-content :deep(th) {
  background: var(--bg-surface);
  font-weight: 600;
  color: var(--text-primary);
}

.answer-content :deep(td) {
  color: var(--text-primary);
}

.answer-content :deep(p) {
  margin: 6px 0;
}

.answer-content :deep(li + li) {
  margin-top: 2px;
}

/* =========================================
   Mobile Layout
   ========================================= */
.ask-layout-mobile {
  display: flex;
  flex-direction: column;
  gap: 8px;
  max-height: 75vh;
  overflow-y: auto;
  width: 100%;
}

.mobile-issue-summary {
  background: var(--bg-surface);
  padding: 8px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  flex-shrink: 0;
}

.mobile-issue-row {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.mobile-issue-label {
  color: var(--text-muted);
  font-size: 12px;
  min-width: 36px;
  flex-shrink: 0;
}

.mobile-issue-value {
  font-size: 13px;
  color: var(--text-primary);
  word-break: break-all;
}

.mobile-issue-value.desc {
  color: var(--color-major);
  font-weight: 500;
}

.mobile-issue-value.file-path {
  font-family: monospace;
  font-size: 12px;
}

.mobile-thinking {
  background: var(--bg-elevated);
  padding: 6px 8px;
  max-height: 48px;
  overflow-y: auto;
  flex-shrink: 0;
  scrollbar-width: none;
}
.mobile-thinking::-webkit-scrollbar { display: none; }

.mobile-thinking-header {
  font-size: 12px;
  color: var(--text-muted);
  margin-bottom: 6px;
  font-weight: 600;
}

.mobile-answer {
  background: var(--bg-surface);
  padding: 8px;
  flex: 1;
  min-height: 80px;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  scrollbar-width: none;
}
.mobile-answer::-webkit-scrollbar { display: none; }

.mobile-answer-header {
  font-size: 12px;
  color: var(--color-success);
  font-weight: 600;
  margin-bottom: 4px;
  flex-shrink: 0;
}

.mobile-empty {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--text-muted);
  font-size: 13px;
  min-height: 80px;
  border: 1px dashed var(--border-default);
}

.mobile-input-area {
  display: flex;
  gap: 8px;
  align-items: flex-end;
  flex-shrink: 0;
  padding: 4px;
}

.mobile-input-area :deep(.el-textarea__inner) {
  resize: none;
}

.mobile-send-btn {
  align-self: stretch;
  flex-shrink: 0;
}
</style>

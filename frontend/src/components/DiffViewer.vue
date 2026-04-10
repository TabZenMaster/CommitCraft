<template>
  <Teleport to="body">
    <div v-if="modelValue" class="dv-overlay" @click.self="close">
      <div class="dv-panel">
        <!-- 顶部栏 -->
        <div class="dv-topbar">
          <div class="dv-stats">
            <span v-if="stats" class="dv-add">+{{ stats.additions }}</span>
            <span v-if="stats" class="dv-del">-{{ stats.deletions }}</span>
            <span v-if="fileName" class="dv-filename">{{ fileName }}</span>
          </div>
          <div class="dv-topbar-right">
            <!-- 移动端：切换视图模式 -->
            <button
              v-if="isMobile"
              class="dv-mode-btn"
              @click="unified = !unified"
              :title="unified ? '切换并排视图' : '切换统一视图'"
            >
              {{ unified ? '▮▮ 并排' : '☰ 统一' }}
            </button>
            <button class="dv-close" @click="close">✕</button>
          </div>
        </div>

        <!-- Diff 内容 -->
        <div ref="diffEl" class="dv-diff" :class="{ 'dv-unified': unified }" />

        <!-- 移动端底部留白 -->
        <div v-if="isMobile" class="dv-mobile-footer" />
      </div>
    </div>
  </Teleport>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue'
import { html } from 'diff2html'
import 'diff2html/bundles/css/diff2html.min.css'

defineOptions({ inheritAttrs: false })

const props = defineProps<{
  modelValue: boolean
  content: string
  fileName?: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', v: boolean): void
}>()

const diffEl = ref<HTMLElement>()
const stats = ref<{ additions: number; deletions: number } | null>(null)
const unified = ref(true) // 手机默认统一视图，更省空间

// Breakpoint detection (inline, no composable dependency)
const isMobile = computed(() => {
  if (typeof window === 'undefined') return false
  return window.innerWidth < 480
})

function close() {
  emit('update:modelValue', false)
}

function computeStats(content: string) {
  const lines = content.split('\n')
  let a = 0, d = 0
  for (const l of lines) {
    if (l.startsWith('+') && !l.startsWith('+++')) a++
    else if (l.startsWith('-') && !l.startsWith('---')) d++
  }
  stats.value = { additions: a, deletions: d }
}

function renderDiff() {
  if (!diffEl.value) return
  if (!props.content.trim()) {
    stats.value = null
    diffEl.value.innerHTML = '<p class="dv-empty">无差异内容</p>'
    return
  }
  computeStats(props.content)
  diffEl.value.innerHTML = html(props.content, {
    drawFileList: false,
    fileContentToggle: false,
    highlight: true,
    synchronizedScroll: true,
    matching: 'lines',
    outputFormat: 'side-by-side',
    colorScheme: 'dark'
  }) as string

  nextTick(() => {
    if (!diffEl.value) return
    const containers = diffEl.value.querySelectorAll('.d2h-files-diff')
    containers.forEach(container => {
      const panes = container.querySelectorAll('.d2h-file-side-diff')
      if (panes.length === 2 && !isMobile.value) {
        const leftPane = panes[0] as HTMLElement
        const rightPane = panes[1] as HTMLElement
        let syncingLeft = false
        let syncingRight = false
        leftPane.addEventListener('scroll', () => {
          if (!syncingLeft) { syncingRight = true; rightPane.scrollTop = leftPane.scrollTop; rightPane.scrollLeft = leftPane.scrollLeft }
          syncingLeft = false
        })
        rightPane.addEventListener('scroll', () => {
          if (!syncingRight) { syncingLeft = true; leftPane.scrollTop = rightPane.scrollTop; leftPane.scrollLeft = rightPane.scrollLeft }
          syncingRight = false
        })
      }
    })
  })
}

watch([() => props.modelValue, () => props.content], ([v]) => {
  document.documentElement.classList[v ? 'add' : 'remove']('dv-lock')
  if (v) {
    unified.value = true
    nextTick(() => renderDiff())
  }
})
</script>

<style>
/* Global — diff2html injected content needs unscoped styles */

/* Overlay */
.dv-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.88);
  z-index: 9999;
  display: flex;
  flex-direction: column;
}

.dv-panel {
  position: relative;
  width: 100%;
  height: 100%;
  display: flex;
  flex-direction: column;
  background: #0d1117;
  overflow: hidden;
}

/* Top bar */
.dv-topbar {
  height: 44px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 12px;
  background: #161b22;
  border-bottom: 1px solid rgba(255,255,255,0.08);
  gap: 8px;
}

.dv-stats {
  display: flex;
  align-items: center;
  gap: 8px;
}

.dv-add {
  color: #3fb950;
  font-size: 13px;
  font-weight: 700;
  font-family: monospace;
}

.dv-del {
  color: #f85149;
  font-size: 13px;
  font-weight: 700;
  font-family: monospace;
}

.dv-filename {
  color: rgba(255,255,255,0.6);
  font-size: 12px;
  font-family: monospace;
  max-width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.dv-topbar-right {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.dv-mode-btn {
  background: rgba(255,255,255,0.06);
  border: 1px solid rgba(255,255,255,0.12);
  color: rgba(255,255,255,0.7);
  font-size: 11px;
  font-family: monospace;
  padding: 4px 10px;
  cursor: pointer;
  white-space: nowrap;
  transition: background 0.15s;
}

.dv-mode-btn:hover {
  background: rgba(255,255,255,0.12);
  color: #fff;
}

.dv-close {
  background: none;
  border: none;
  color: #8b949e;
  font-size: 16px;
  cursor: pointer;
  padding: 4px 8px;
  line-height: 1;
  transition: color 0.15s;
}

.dv-close:hover {
  color: #e6edf3;
}

/* Diff area */
.dv-diff {
  flex: 1;
  overflow: auto;
  display: flex;
  flex-direction: column;
  min-height: 0;
  -webkit-overflow-scrolling: touch;
}

.dv-empty {
  color: #6e7681;
  padding: 40px;
  text-align: center;
  font-size: 14px;
}

/* diff2html structure overrides */
.dv-diff .d2h-wrapper,
.dv-diff .d2h-file-wrapper {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  margin: 0 !important;
  border: none !important;
  border-radius: 0 !important;
}

.dv-diff .d2h-files-diff {
  flex: 1;
  overflow: auto;
  display: flex;
  min-height: 0;
}

.dv-diff .d2h-file-side-diff {
  min-height: 0;
  overflow: auto !important;
  flex: 1;
}

.dv-diff .d2h-code-linenumber {
  position: sticky !important;
  left: 0;
  z-index: 1;
  background: inherit;
}

.dv-diff .d2h-code-side-linenumber {
  position: sticky !important;
  left: 0;
  z-index: 1;
}

.dv-diff .d2h-code-row {
  display: flex;
}

.dv-diff .d2h-code-side {
  flex: 1;
  overflow-x: auto;
}

/* Mobile unified view: force single-column */
@media (max-width: 767px) {
  .dv-panel {
    background: #0d1117;
  }

  .dv-topbar {
    height: 48px;
    padding: 0 10px;
  }

  /* Hide the right pane in unified mode, show only left */
  .dv-diff.dv-unified .d2h-files-diff {
    display: flex !important;
    flex-direction: column;
    overflow: auto !important;
    -webkit-overflow-scrolling: touch;
  }

  .dv-diff.dv-unified .d2h-file-side-diff:last-child {
    display: none !important;
  }

  .dv-diff.dv-unified .d2h-file-side-diff:first-child {
    flex: none !important;
    width: 100% !important;
    height: auto !important;
    min-height: 0;
    overflow: visible !important;
    display: flex;
    flex-direction: column;
  }

  /* Allow horizontal scroll within code lines */
  .dv-diff.dv-unified .d2h-code-side {
    overflow-x: auto !important;
    -webkit-overflow-scrolling: touch;
  }

  /* Make code table expand beyond viewport width for horizontal scroll */
  .dv-diff.dv-unified .d2h-diff-table {
    min-width: max-content;
  }

  /* Make code font more legible on narrow screens */
  .dv-diff.dv-unified .d2h-code,
  .dv-diff.dv-unified .d2h-code-linenumber,
  .dv-diff.dv-unified .d2h-code-side-linenumber {
    font-size: 13px !important;
    line-height: 1.6 !important;
    min-width: max-content;
  }

  /* Line number column: sticky left */
  .dv-diff.dv-unified .d2h-code-linenumber,
  .dv-diff.dv-unified .d2h-code-side-linenumber {
    position: sticky !important;
    left: 0;
    min-width: 44px;
    text-align: right;
    padding-right: 8px !important;
    background: #161b22;
    z-index: 2;
  }

  /* Add a subtle left border to the line number column */
  .dv-diff.dv-unified .d2h-code-linenumber,
  .dv-diff.dv-unified .d2h-code-side-linenumber {
    border-right: 1px solid rgba(255,255,255,0.06);
  }

  /* Add bottom padding so last lines aren't cut off */
  .dv-diff.dv-unified .d2h-files-diff {
    padding-bottom: 16px;
  }

  .dv-mobile-footer {
    height: 16px;
    flex-shrink: 0;
  }
}

/* Page scroll lock */
html.dv-lock,
html.dv-lock body {
  overflow: hidden !important;
  height: 100% !important;
}
</style>

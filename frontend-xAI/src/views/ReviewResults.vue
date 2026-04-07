<template>
  <div>
    <div class="page-header">
      <div>
        <el-button @click="router.push('/review')">← 返回</el-button>
        <span style="margin-left:12px">审核任务 #{{ taskId }} 结果</span>
      </div>
      <span v-if="taskInfo" style="color:var(--text-muted);font-size:13px">
        {{ taskInfo.commitSha?.slice(0, 8) }} | {{ taskInfo.commitMessage }} | {{ taskInfo.committer }}
      </span>
    </div>

    <el-table v-if="results.length" :data="results" stripe style="width:100%">
      <el-table-column type="index" width="50" align="center" />
      <el-table-column prop="filePath" label="文件" min-width="200" show-overflow-tooltip />
      <el-table-column label="行号" width="110" align="center">
        <template #default="{ row }">{{ row.lineStart }}~{{ row.lineEnd }}</template>
      </el-table-column>
      <el-table-column prop="issueType" label="类型" width="100" align="center">
        <template #default="{ row }">
          <span class="table-tag" :class="typeTag(row.issueType)">{{ typeName(row.issueType) }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="severity" label="严重程度" width="100" align="center">
        <template #default="{ row }">
          <span class="table-tag" :class="sevTag(row.severity)">{{ sevName(row.severity) }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="description" label="问题描述" min-width="220" show-overflow-tooltip />
      <el-table-column prop="suggestion" label="修复建议" min-width="180" show-overflow-tooltip />
      <el-table-column label="代码" width="80" align="center">
        <template #default="{ row }">
          <button v-if="row.diffContent" class="action-link" @click="openCode(row.diffContent)">查看</button>
          <span v-else class="text-muted">-</span>
        </template>
      </el-table-column>
      <el-table-column prop="status" label="状态" width="90" align="center">
        <template #default="{ row }">
          <span class="table-tag" :class="statusTag(row.status)">{{ statusName(row.status) }}</span>
        </template>
      </el-table-column>
    </el-table>

    <el-empty v-else-if="!loading" description="暂无审核结果" />

    <el-pagination
      v-if="total > 0"
      v-model:current-page="pageIndex"
      v-model:page-size="pageSize"
      :page-sizes="[20, 50, 100]"
      :total="total"
      layout="total, sizes, prev, pager, next"
      @size-change="loadData"
      @current-change="loadData"
      style="margin-top:16px;justify-content:center" />

    <!-- 全屏代码查看器 -->
    <Teleport to="body">
      <div v-if="overlayVisible" class="cv-overlay" @click.self="closeOverlay">
        <div class="cv-panel">
          <div class="cv-topbar">
            <span v-if="diffStats" class="cv-add">+{{ diffStats.additions }}</span>
            <span v-if="diffStats" class="cv-del">-{{ diffStats.deletions }}</span>
            <button class="cv-close" @click="closeOverlay">✕</button>
          </div>
          <div ref="diffRef" class="cv-diff"></div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, nextTick, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { reviewApi } from '@/api'
import { html } from 'diff2html'
import 'diff2html/bundles/css/diff2html.min.css'

const route = useRoute()
const router = useRouter()
const taskId = Number(route.params.id)

const taskInfo = ref<any>(null)
const results = ref<any[]>([])
const loading = ref(false)
const pageIndex = ref(1)
const pageSize = ref(50)
const total = ref(0)

const overlayVisible = ref(false)
const diffRef = ref<HTMLElement>()
const diffStats = ref<{ additions: number; deletions: number } | null>(null)

watch(overlayVisible, (v) => {
  const method = v ? 'add' : 'remove'
  document.documentElement.classList[method]('cv-lock')
})

function openCode(diffContent: string) {
  overlayVisible.value = true
  nextTick(() => renderDiff(diffContent))
}

function closeOverlay() {
  overlayVisible.value = false
}

function renderDiff(content: string) {
  if (!diffRef.value) return
  if (!content.trim()) {
    diffStats.value = null
    diffRef.value.innerHTML = '<p style="color:#999;padding:40px">无差异内容</p>'
    return
  }
  const lines = content.split('\n')
  let a = 0, d = 0
  for (const l of lines) {
    if (l.startsWith('+') && !l.startsWith('+++')) a++
    else if (l.startsWith('-') && !l.startsWith('---')) d++
  }
  diffStats.value = { additions: a, deletions: d }
  diffRef.value.innerHTML = html(content, {
    drawFileList: false,
    fileContentToggle: false,
    highlight: true,
    synchronizedScroll: true,
    matching: 'lines',
    outputFormat: 'side-by-side',
    colorScheme: 'dark'
  }) as string

  nextTick(() => {
    if (!diffRef.value) return
    const diffContainers = diffRef.value.querySelectorAll('.d2h-files-diff')
    diffContainers.forEach(container => {
      const panes = container.querySelectorAll('.d2h-file-side-diff')
      if (panes.length === 2) {
        const leftPane = panes[0] as HTMLElement
        const rightPane = panes[1] as HTMLElement
        
        let isSyncingLeftScroll = false
        let isSyncingRightScroll = false

        leftPane.addEventListener('scroll', () => {
          if (!isSyncingLeftScroll) {
            isSyncingRightScroll = true
            rightPane.scrollTop = leftPane.scrollTop
            rightPane.scrollLeft = leftPane.scrollLeft
          }
          isSyncingLeftScroll = false
        })

        rightPane.addEventListener('scroll', () => {
          if (!isSyncingRightScroll) {
            isSyncingLeftScroll = true
            leftPane.scrollTop = rightPane.scrollTop
            leftPane.scrollLeft = rightPane.scrollLeft
          }
          isSyncingRightScroll = false
        })
      }
    })
  })
}

const sevTag = (s: string) => ({ critical: 'danger', major: 'warning', minor: 'info', suggestion: 'info' }[s] || 'info')
const sevName = (s: string) => ({ critical: '致命', major: '严重', minor: '一般', suggestion: '建议' }[s] || s)
const typeTag = (s: string) => ({ security: 'danger', correctness: 'danger', performance: 'success', maintainability: 'info', best_practice: 'warning', code_style: 'info', other: 'info' }[s] || 'info')
const typeName = (s: string) => ({ security: '安全', correctness: '正确性', performance: '性能', maintainability: '可维护性', best_practice: '最佳实践', code_style: '代码风格', other: '其他' }[s] || s)
const statusTag = (s: number) => ['', 'warning', 'success', 'info'][s] || 'info'
const statusName = (s: number) => ['待处理', '已认领', '已修复', '已忽略'][s] || '-'

async function loadData() {
  loading.value = true
  const res: any = await reviewApi.results({ reviewCommitId: taskId, pageIndex: pageIndex.value, pageSize: pageSize.value })
  loading.value = false
  if (res.success) {
    const paged = res.data as any
    const order: Record<string, number> = { critical: 0, major: 1, minor: 2, suggestion: 3 }
    results.value = (paged?.data ?? []).sort((a: any, b: any) => (order[a.severity] ?? 99) - (order[b.severity] ?? 99))
    total.value = paged?.total ?? 0
  }
}

onMounted(async () => {
  const t = await reviewApi.task(taskId)
  if (t.success) taskInfo.value = t.data
  loadData()
})
</script>

<style scoped>
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  background: var(--bg-surface);
  border: 1px solid var(--border-default);
  padding: 12px 16px;
}

.page-title {
  font-family: var(--font-display);
  font-size: 14px;
  font-weight: 400;
  text-transform: uppercase;
  letter-spacing: 1px;
  color: var(--text-primary);
}

/* 全屏覆盖层 - always dark for code viewer */
.cv-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.85);
  z-index: 9999;
  overflow: hidden;
}

/* 面板 */
.cv-panel {
  position: absolute;
  inset: 0;
  background: #0d1117;
  display: flex;
  flex-direction: column;
}

/* 顶栏 */
.cv-topbar {
  height: 40px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 12px;
  background: #161b22;
}
.cv-add { color: #3fb950; font-size: 13px; font-weight: 700; }
.cv-del { color: #f85149; font-size: 13px; font-weight: 700; }
.cv-close {
  margin-left: auto;
  background: none;
  border: none;
  color: #8b949e;
  font-size: 15px;
  cursor: pointer;
  padding: 4px 8px;
  line-height: 1;
}
.cv-close:hover { color: #e6edf3; }

/* diff 区域 */
.cv-diff {
  flex: 1;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  min-height: 0;
}
.cv-diff > .d2h-wrapper,
.cv-diff .d2h-file-wrapper {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
  margin: 0 !important;
  border-radius: 0;
  border: none;
}
.cv-diff .d2h-files-diff {
  flex: 1;
  overflow: hidden;
  display: flex;
  min-height: 0;
}
.cv-diff .d2h-file-side-diff {
  height: 100%;
  overflow: auto !important;
}
.cv-diff .d2h-code-side-linenumber {
  position: sticky !important;
  left: 0;
  z-index: 1;
}

/* 弹窗打开时锁定背景滚动 */
html.cv-lock,
html.cv-lock body {
  overflow: hidden !important;
  height: 100% !important;
}
</style>

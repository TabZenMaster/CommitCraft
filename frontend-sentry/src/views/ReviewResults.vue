<template>
  <div class="page-container">
    <div class="page-header">
      <div>
        <el-button @click="router.push('/review')" class="btn-back">← 返回</el-button>
        <span style="margin-left:12px">审核任务 #{{ taskId }} 结果</span>
      </div>
      <span v-if="taskInfo" style="color:var(--text-muted);font-size:13px">
        {{ taskInfo.commitSha?.slice(0, 8) }} | {{ taskInfo.commitMessage }} | {{ taskInfo.committer }}
      </span>
    </div>

    <div class="sentry-card table-card">
      <el-table v-if="results.length" :data="results" stripe>
        <el-table-column type="index" width="50" />
        <el-table-column prop="filePath" label="文件" width="200" show-overflow-tooltip />
        <el-table-column label="行号" width="90">
          <template #default="{ row }">{{ row.lineStart }}~{{ row.lineEnd }}</template>
        </el-table-column>
        <el-table-column prop="issueType" label="类型" width="120">
          <template #default="{ row }">
            <el-tag size="small" :type="typeTag(row.issueType)">{{ typeName(row.issueType) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="severity" label="严重程度" width="100">
          <template #default="{ row }">
            <el-tag size="small" :type="sevTag(row.severity)" effect="dark">{{ sevName(row.severity) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="description" label="问题描述" />
        <el-table-column prop="suggestion" label="修复建议" />
        <el-table-column label="代码" width="90">
          <template #default="{ row }">
            <el-button v-if="row.diffContent" size="small" @click="openCode(row.diffContent)">查看代码</el-button>
            <span v-else style="color:var(--text-muted);font-size:12px">-</span>
          </template>
        </el-table-column>
        <el-table-column prop="status" label="状态" width="90">
          <template #default="{ row }">
            <el-tag size="small" :type="statusTag(row.status)">{{ statusName(row.status) }}</el-tag>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-empty v-if="!results.length && !loading" description="暂无审核结果" />

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

const sevTag = (s: string) => ({ critical: 'danger', major: 'warning', minor: '', suggestion: 'info' }[s] || '')
const sevName = (s: string) => ({ critical: '致命', major: '严重', minor: '一般', suggestion: '建议' }[s] || s)
const typeTag = (s: string) => ({ security: 'danger', correctness: 'danger', performance: 'success', maintainability: 'info', best_practice: 'warning', code_style: '', other: 'info' }[s] || '')
const typeName = (s: string) => ({ security: '安全', correctness: '正确性', performance: '性能', maintainability: '可维护性', best_practice: '最佳实践', code_style: '代码风格', other: '其他' }[s] || s)
const statusTag = (s: number) => [, 'warning', 'success', 'info'][s] || 'info'
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
.page-container { padding: 20px 24px; background: var(--bg-primary); min-height: 100vh; }
.page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; flex-wrap: wrap; gap: 12px; }

.table-card { overflow: visible; }

.btn-back {
  background: var(--purple-mid);
  border: 1px solid #584674;
  color: #fff;
  border-radius: var(--radius-md);
  padding: 8px 14px;
  font-size: 13px;
  font-weight: 600;
  letter-spacing: 0.3px;
  text-transform: uppercase;
  cursor: pointer;
  box-shadow: var(--shadow-inset);
  transition: box-shadow 0.2s ease, background 0.2s ease;
}
.btn-back:hover { box-shadow: var(--shadow-elevated); background: #8b74a8; transform: translateY(-1px); }
</style>

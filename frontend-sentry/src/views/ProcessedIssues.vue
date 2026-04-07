<template>
  <div class="page-container">
    <div class="page-header">
      <div class="page-title">✅ 已处理问题</div>
      <div style="display:flex;gap:8px;flex-wrap:wrap">
        <el-select v-model="filterRepo" clearable placeholder="仓库" style="width:140px" @change="onFilterChange">
          <el-option label="全部仓库" value="" />
          <el-option v-for="r in repos" :key="r.id" :label="r.repoName" :value="r.id" />
        </el-select>
        <el-select v-model="filterSev" clearable placeholder="严重程度" style="width:120px" @change="onFilterChange">
          <el-option label="全部" value="" />
          <el-option label="致命" value="critical" />
          <el-option label="严重" value="major" />
          <el-option label="警告" value="minor" />
          <el-option label="建议" value="suggestion" />
        </el-select>
        <el-select v-model="filterType" clearable placeholder="问题类型" style="width:120px" @change="onFilterChange">
          <el-option label="全部" value="" />
          <el-option label="安全" value="security" />
          <el-option label="正确性" value="correctness" />
          <el-option label="性能" value="performance" />
          <el-option label="可维护性" value="maintainability" />
          <el-option label="最佳实践" value="best_practice" />
          <el-option label="代码风格" value="code_style" />
          <el-option label="其他" value="other" />
        </el-select>
        <el-select v-model="filterStatus" clearable placeholder="处理结果" style="width:120px" @change="onFilterChange">
          <el-option label="全部" value="" />
          <el-option label="已修复" :value="2" />
          <el-option label="已忽略" :value="3" />
        </el-select>
        <el-button @click="loadData" class="btn-primary-sm">刷新</el-button>
      </div>
    </div>

    <div class="sentry-card table-card">
      <el-table :data="results" stripe>
        <el-table-column type="index" width="50" />
        <el-table-column prop="filePath" label="文件" width="200" show-overflow-tooltip />
        <el-table-column label="行号" width="90">
          <template #default="{ row }">{{ row.lineStart || '-' }}~{{ row.lineEnd || '-' }}</template>
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
        <el-table-column prop="description" label="问题描述" min-width="200" show-overflow-tooltip />
        <el-table-column prop="suggestion" label="修复建议" min-width="180" show-overflow-tooltip />
        <el-table-column prop="status" label="状态" width="90">
          <template #default="{ row }">
            <el-tag size="small" :type="statusTag(row.status)" effect="dark">{{ statusName(row.status) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="handlerName" label="处理人" width="100" />
        <el-table-column label="代码" width="90">
          <template #default="{ row }">
            <el-button v-if="row.diffContent" size="small" @click="openCode(row.diffContent)">查看代码</el-button>
            <span v-else style="color:var(--text-muted);font-size:12px">-</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="90" fixed="right">
          <template #default="{ row }">
            <el-button size="small" type="info" plain @click="openDetail(row)">详情</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-pagination
      v-model:current-page="pageIndex"
      v-model:page-size="pageSize"
      :page-sizes="[20, 50, 100, 200]"
      :total="total"
      layout="total, sizes, prev, pager, next, jumper"
      @size-change="loadData"
      @current-change="loadData"
      style="margin-top:16px;justify-content:center" />

    <!-- 详情弹窗 -->
    <el-dialog v-model="detailVisible" title="处理详情" width="480px">
      <el-descriptions :column="1" border>
        <el-descriptions-item label="处理人">{{ detailData.handlerName || '-' }}</el-descriptions-item>
        <el-descriptions-item label="处理时间">{{ detailData.handledAt || '-' }}</el-descriptions-item>
        <el-descriptions-item label="处理备注">{{ detailData.handlerMemo || '-' }}</el-descriptions-item>
      </el-descriptions>
      <template #footer>
        <el-button @click="detailVisible = false">关闭</el-button>
      </template>
    </el-dialog>

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
import { ElMessage } from 'element-plus'
import { reviewApi, repositoryApi } from '@/api'
import { html } from 'diff2html'
import 'diff2html/bundles/css/diff2html.min.css'

const results = ref<any[]>([])
const repos = ref<any[]>([])
const filterRepo = ref('')
const filterSev = ref('')
const filterType = ref('')
const filterStatus = ref('')
const pageIndex = ref(1)
const pageSize = ref(50)
const total = ref(0)

const detailVisible = ref(false)
const detailData = ref<any>({})

const overlayVisible = ref(false)
const diffRef = ref<HTMLElement>()
const diffStats = ref<{ additions: number; deletions: number } | null>(null)

watch(overlayVisible, (v) => {
  document.documentElement.classList[v ? 'add' : 'remove']('cv-lock')
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
          if (!isSyncingLeftScroll) { isSyncingRightScroll = true; rightPane.scrollTop = leftPane.scrollTop; rightPane.scrollLeft = leftPane.scrollLeft }
          isSyncingLeftScroll = false
        })
        rightPane.addEventListener('scroll', () => {
          if (!isSyncingRightScroll) { isSyncingLeftScroll = true; leftPane.scrollTop = rightPane.scrollTop; leftPane.scrollLeft = rightPane.scrollLeft }
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
const statusName = (s: number) => ['', '', '已修复', '已忽略'][s] || '-'

onMounted(async () => {
  const r2 = await repositoryApi.list()
  if (r2.success) repos.value = r2.data
  loadData()
})

function onFilterChange() {
  pageIndex.value = 1
  loadData()
}

async function loadData() {
  const res: any = await reviewApi.results({
    repositoryId: filterRepo.value || undefined,
    severity: filterSev.value || undefined,
    issueType: filterType.value || undefined,
    status: filterStatus.value !== '' ? Number(filterStatus.value) : undefined,
    pageIndex: pageIndex.value,
    pageSize: pageSize.value
  })
  if (res.success) {
    const paged = res.data as any
    results.value = (paged?.data ?? []).filter((r: any) => r.status === 2 || r.status === 3)
    total.value = paged?.total ?? 0
  }
}

function openDetail(row: any) {
  detailData.value = row
  detailVisible.value = true
}
</script>

<style scoped>
.page-container { padding: 20px 24px; background: var(--bg-primary); min-height: 100vh; }
.page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; flex-wrap: wrap; gap: 12px; }
.page-title { font-size: 18px; font-weight: 600; color: var(--text-primary); }

.table-card { overflow: visible; }

.btn-primary-sm {
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
.btn-primary-sm:hover { box-shadow: var(--shadow-elevated); background: #8b74a8; transform: translateY(-1px); }
</style>

<template>
  <div>
    <div class="page-header">
      <div class="page-title">已处理问题</div>
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
        <el-button @click="loadData">刷新</el-button>
      </div>
    </div>

    <!-- 桌面端表格 -->
    <el-table v-if="!isMobile" v-loading="loading" :data="results" stripe style="width:100%">
      <el-table-column type="index" width="60" align="center" />
      <el-table-column prop="filePath" label="文件" min-width="200" show-overflow-tooltip />
      <el-table-column label="行号" width="110" align="center">
        <template #default="{ row }">{{ row.lineStart || '-' }}~{{ row.lineEnd || '-' }}</template>
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
      <el-table-column prop="status" label="状态" width="90" align="center">
        <template #default="{ row }">
          <span class="table-tag" :class="statusTag(row.status)">{{ statusName(row.status) }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="handlerName" label="处理人" width="100" align="center" />
      <el-table-column label="代码" width="80" align="center">
        <template #default="{ row }">
          <button v-if="row.diffContent" class="action-link" @click="openCode(row.diffContent)">查看</button>
          <span v-else class="text-muted">-</span>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="80" fixed="right">
        <template #default="{ row }">
          <div class="action-btns">
            <button class="action-link" @click="openDetail(row)">详情</button>
          </div>
        </template>
      </el-table-column>
    </el-table>

    <!-- 移动端卡片列表 -->
    <div v-else class="card-list">
      <TableCard
        v-for="row in results"
        :key="row.id"
        :row="row"
        :columns="cardColumns"
      >
        <template #header>
          <div class="card-file-path">{{ row.filePath }}</div>
          <div class="card-header-right">
            <span class="table-tag" :class="sevTag(row.severity)">{{ sevName(row.severity) }}</span>
            <span class="table-tag" :class="statusTag(row.status)">{{ statusName(row.status) }}</span>
          </div>
        </template>
        <template #lineRange="{ row }">
          <span class="mono-text">{{ row.lineStart || '-' }}{{ row.lineEnd && row.lineEnd !== row.lineStart ? ` ~ ${row.lineEnd}` : '' }}</span>
        </template>
        <template #description="{ row }"><span class="cell-text">{{ row.description }}</span></template>
        <template #suggestion="{ row }"><span class="cell-text text-muted">{{ row.suggestion || '-' }}</span></template>
        <template #handlerName="{ row }"><span class="text-muted">{{ row.handlerName || '-' }}</span></template>
        <template #footer>
          <button class="action-link" @click="openDetail(row)">详情</button>
          <button v-if="row.diffContent" class="action-link" @click="openCode(row.diffContent)">代码</button>
        </template>
      </TableCard>
      <el-empty v-if="results.length === 0" description="暂无数据" :image-size="60" />
    </div>

    <el-pagination
      v-model:current-page="pageIndex"
      v-model:page-size="pageSize"
      :page-sizes="isMobile ? [] : [20, 50, 100, 200]"
      :total="total"
      :layout="pageLayout"
      :pager-count="isMobile ? 5 : 7"
      :small="isMobile"
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
    <DiffViewer v-model="overlayVisible" :content="currentDiffContent" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import { ElMessage } from 'element-plus'
import { reviewApi, repositoryApi } from '@/api'
import TableCard from '@/components/TableCard.vue'
import DiffViewer from '@/components/DiffViewer.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'xs' || breakpoint.value === 'sm')
const pageLayout = computed(() => isMobile.value ? 'total, prev, pager, next' : 'total, sizes, prev, pager, next, jumper')

const results = ref<any[]>([])
const repos = ref<any[]>([])
const loading = ref(false)
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
const currentDiffContent = ref('')

function openCode(diffContent: string) {
  currentDiffContent.value = diffContent
  overlayVisible.value = true
}

const sevTag = (s: string) => ({ critical: 'danger', major: 'warning', minor: 'info', suggestion: 'info' }[s] || 'info')
const sevName = (s: string) => ({ critical: '致命', major: '严重', minor: '一般', suggestion: '建议' }[s] || s)
const typeTag = (s: string) => ({ security: 'danger', correctness: 'danger', performance: 'success', maintainability: 'info', best_practice: 'purple', code_style: 'info', other: 'info' }[s] || 'info')
const typeName = (s: string) => ({ security: '安全', correctness: '正确性', performance: '性能', maintainability: '可维护性', best_practice: '最佳实践', code_style: '代码风格', other: '其他' }[s] || s)
const statusTag = (s: number) => [, 'warning', 'success', 'info'][s] || 'info'
const statusName = (s: number) => ['', '', '已修复', '已忽略'][s] || '-'

const cardColumns = [
  { key: 'lineRange', label: '行号' },
  { key: 'description', label: '问题描述' },
  { key: 'suggestion', label: '修复建议' },
  { key: 'handlerName', label: '处理人' },
]

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
  loading.value = true
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
  loading.value = false
}

function openDetail(row: any) {
  detailData.value = row
  detailVisible.value = true
}
</script>

<style scoped>
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  flex-wrap: wrap;
  gap: 8px;
  background: var(--bg-surface);
  border: 1px solid var(--border-default);
  padding: 12px 16px;
}

.page-title {
  font-family: var(--font-body);
  font-size: 14px;
  font-weight: 400;
  text-transform: none;
  letter-spacing: normal;
  color: var(--text-primary);
  display: flex;
  align-items: center;
  gap: 8px;
}

.page-title-icon {
  width: 16px;
  height: 16px;
  flex-shrink: 0;
}

.card-list {
  padding: 4px 0;
}

.card-file-path {
  flex: 1;
  font-family: var(--font-display);
  font-size: 11px;
  color: var(--text-secondary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  min-width: 0;
}

.card-header-right {
  display: flex;
  gap: 4px;
  flex-shrink: 0;
}

.mono-text {
  font-family: var(--font-display);
  font-size: 12px;
  color: var(--ring-blue);
}

.cell-text {
  font-size: 13px;
  line-height: 1.5;
  color: var(--text-primary);
  word-break: break-all;
}
</style>

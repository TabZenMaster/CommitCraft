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

    <!-- 桌面端表格 -->
    <el-table v-if="!isMobile && results.length" :data="results" stripe style="width:100%">
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

    <!-- 移动端卡片列表 -->
    <div v-if="isMobile && results.length" class="card-list">
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
          <span class="mono-text">{{ row.lineStart }}~{{ row.lineEnd }}</span>
        </template>
        <template #description="{ row }"><span class="cell-text">{{ row.description }}</span></template>
        <template #suggestion="{ row }"><span class="cell-text text-muted">{{ row.suggestion || '-' }}</span></template>
        <template #footer>
          <button v-if="row.diffContent" class="action-link" @click="openCode(row.diffContent)">查看代码</button>
        </template>
      </TableCard>
    </div>

    <el-empty v-else-if="!loading" description="暂无审核结果" />

    <el-pagination
      v-if="total > 0"
      v-model:current-page="pageIndex"
      v-model:page-size="pageSize"
      :page-sizes="isMobile ? [] : [20, 50, 100]"
      :total="total"
      :layout="pageLayout"
      :pager-count="isMobile ? 5 : 7"
      :small="isMobile"
      @size-change="loadData"
      @current-change="loadData"
      style="margin-top:16px;justify-content:center" />

    <!-- 全屏代码查看器 -->
    <DiffViewer v-model="overlayVisible" :content="currentDiffContent" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { reviewApi } from '@/api'
import TableCard from '@/components/TableCard.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'
import DiffViewer from '@/components/DiffViewer.vue'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'xs' || breakpoint.value === 'sm')
const pageLayout = computed(() => isMobile.value ? 'total, prev, pager, next' : 'total, sizes, prev, pager, next')

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
const currentDiffContent = ref('')

function openCode(diffContent: string) {
  currentDiffContent.value = diffContent
  overlayVisible.value = true
}

const sevTag = (s: string) => ({ critical: 'danger', major: 'warning', minor: 'info', suggestion: 'info' }[s] || 'info')
const sevName = (s: string) => ({ critical: '致命', major: '严重', minor: '一般', suggestion: '建议' }[s] || s)
const typeTag = (s: string) => ({ security: 'danger', correctness: 'danger', performance: 'success', maintainability: 'info', best_practice: 'warning', code_style: 'info', other: 'info' }[s] || 'info')
const typeName = (s: string) => ({ security: '安全', correctness: '正确性', performance: '性能', maintainability: '可维护性', best_practice: '最佳实践', code_style: '代码风格', other: '其他' }[s] || s)
const statusTag = (s: number) => ['', 'warning', 'success', 'info'][s] || 'info'
const statusName = (s: number) => ['待处理', '已认领', '已修复', '已忽略'][s] || '-'

const cardColumns = [
  { key: 'lineRange', label: '行号' },
  { key: 'description', label: '问题描述' },
  { key: 'suggestion', label: '修复建议' },
]

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
  font-family: var(--font-body);
  font-size: 14px;
  font-weight: 400;
  text-transform: none;
  letter-spacing: normal;
  color: var(--text-primary);
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

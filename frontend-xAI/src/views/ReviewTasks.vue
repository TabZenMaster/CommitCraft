<template>
  <div>
    <div class="page-header">
      <div class="page-title">审核任务</div>
      <div style="display:flex;gap:8px;align-items:center">
        <span style="font-size:13px;color:var(--text-muted)">仓库筛选：</span>
        <el-select v-model="filterRepo" clearable style="width:180px" @change="loadData">
          <el-option label="全部仓库" value="" />
          <el-option v-for="r in repos" :key="r.id" :label="r.repoName" :value="r.id" />
        </el-select>
        <el-button @click="loadData">刷新</el-button>
      </div>
    </div>

    <el-table :data="tasks" stripe style="width:100%">
      <el-table-column prop="id" label="ID" width="60" align="center" />
      <el-table-column prop="repoName" label="仓库" min-width="150" show-overflow-tooltip />
      <el-table-column prop="commitSha" label="Commit" width="100" align="center">
        <template #default="{ row }">{{ row.commitSha.slice(0, 8) }}</template>
      </el-table-column>
      <el-table-column prop="commitMessage" label="Commit 信息" min-width="200" show-overflow-tooltip />
      <el-table-column prop="committer" label="提交人" min-width="100" show-overflow-tooltip />
      <el-table-column prop="committedAt" label="提交时间" width="150" align="center">
        <template #default="{ row }">{{ row.committedAt?.replace('T', ' ').slice(0, 16) }}</template>
      </el-table-column>
      <el-table-column prop="status" label="状态" width="90" align="center">
        <template #default="{ row }">
          <span class="table-tag" :class="statusType(row.status)">{{ statusName(row.status) }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="criticalCount" label="致命" width="70" align="center">
        <template #default="{ row }">
          <span v-if="row.criticalCount > 0" class="text-critical">{{ row.criticalCount }}</span>
          <span v-else class="text-muted">-</span>
        </template>
      </el-table-column>
      <el-table-column prop="resultCount" label="问题数" width="80" align="center" />
      <el-table-column prop="errorMsg" label="失败原因" min-width="160" show-overflow-tooltip>
        <template #default="{ row }">
          <span v-if="row.status === 3 && row.errorMsg"
            class="copy-error"
            :title="'点击复制：' + row.errorMsg"
            @click="copyError(row.errorMsg)">{{ row.errorMsg }}</span>
          <span v-else class="text-muted">-</span>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="120" fixed="right">
        <template #default="{ row }">
          <div class="action-btns">
            <button class="action-link" @click="router.push(`/review/result/${row.id}`)">结果</button>
            <button v-if="row.status === 3" class="action-link warning" @click="retry(row)">重试</button>
          </div>
        </template>
      </el-table-column>
    </el-table>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { reviewApi, repositoryApi } from '@/api'
import { on, off } from '@/utils/eventBus'

const router = useRouter()
const tasks = ref<any[]>([])
const repos = ref<any[]>([])
const filterRepo = ref('')

const statusName = (s: number) => ['待审核', '审核中', '已完成', '失败'][s] || '-'
const statusType = (s: number) => ['', 'warning', 'success', 'danger'][s] || 'info'

onMounted(async () => {
  loadData()
  on('review-updated', loadData)
})

onUnmounted(() => {
  off('review-updated', loadData)
})

async function loadData() {
  const res: any = await reviewApi.tasks(filterRepo.value || 0)
  if (res.success) tasks.value = res.data
}

async function retry(row: any) {
  const res: any = await reviewApi.retry(row.id)
  if (res.success) {
    ElMessage.success(res.msg || '已重新加入队列')
    loadData()
  } else {
    ElMessage.error(res.msg || '重试失败')
  }
}

function copyError(msg: string) {
  navigator.clipboard.writeText(msg).then(() => {
    ElMessage.success('已复制失败原因')
  })
}
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
  display: flex;
  align-items: center;
  gap: 8px;
}

.page-title-icon {
  width: 16px;
  height: 16px;
  flex-shrink: 0;
}

.copy-error { color: var(--color-critical); font-size: 12px; cursor: pointer; text-decoration: underline dotted; }
.copy-error:hover { opacity: 0.8; }
.text-critical { color: var(--color-critical); font-weight: bold; }
.text-muted { color: var(--text-muted); }
</style>

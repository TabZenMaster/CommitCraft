<template>
  <div class="page-container">
    <div class="page-header">
      <div class="page-title">🔍 审核任务</div>
      <div style="display:flex;gap:8px;align-items:center">
        <span style="font-size:13px;color:var(--text-muted)">仓库筛选：</span>
        <el-select v-model="filterRepo" clearable style="width:180px" @change="loadData">
          <el-option label="全部仓库" value="" />
          <el-option v-for="r in repos" :key="r.id" :label="r.repoName" :value="r.id" />
        </el-select>
        <el-button @click="loadData" class="btn-primary-sm">刷新</el-button>
      </div>
    </div>

    <div class="sentry-card table-card">
      <el-table :data="tasks" stripe>
        <el-table-column prop="id" label="ID" width="60" />
        <el-table-column prop="repoName" label="仓库" width="140" />
        <el-table-column prop="commitSha" label="Commit" width="120">
          <template #default="{ row }"><code style="color:#c2ef4e;font-size:12px">{{ row.commitSha.slice(0, 8) }}</code></template>
        </el-table-column>
        <el-table-column prop="commitMessage" label="Commit 信息" show-overflow-tooltip />
        <el-table-column prop="committer" label="提交人" width="100" />
        <el-table-column prop="committedAt" label="提交时间" width="160">
          <template #default="{ row }">{{ row.committedAt?.replace('T', ' ').slice(0, 16) }}</template>
        </el-table-column>
        <el-table-column prop="status" label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="statusType(row.status)" size="small">{{ statusName(row.status) }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="criticalCount" label="致命" width="60">
          <template #default="{ row }">
            <span v-if="row.criticalCount > 0" style="color:#f56c6c;font-weight:bold">{{ row.criticalCount }}</span>
            <span v-else>-</span>
          </template>
        </el-table-column>
        <el-table-column prop="resultCount" label="问题数" width="80" />
        <el-table-column prop="errorMsg" label="失败原因" width="180">
          <template #default="{ row }">
            <span v-if="row.status === 3 && row.errorMsg"
              class="copy-error"
              :title="'点击复制：' + row.errorMsg"
              @click="copyError(row.errorMsg)">{{ row.errorMsg }}</span>
            <span v-else>-</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="180">
          <template #default="{ row }">
            <el-button size="small" @click="router.push(`/review/result/${row.id}`)">查看结果</el-button>
            <el-button v-if="row.status === 3" size="small" type="warning" @click="retry(row)">重试</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>
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

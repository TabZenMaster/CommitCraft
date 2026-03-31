<template>
  <div>
    <div class="page-header">
      <h3>🔍 审核任务</h3>
      <div style="display:flex;gap:8px;align-items:center">
        <span style="font-size:13px;color:#999">仓库筛选：</span>
        <el-select v-model="filterRepo" clearable style="width:180px" @change="loadData">
          <el-option label="全部仓库" value="" />
          <el-option v-for="r in repos" :key="r.id" :label="r.repoName" :value="r.id" />
        </el-select>
        <el-button @click="loadData">刷新</el-button>
      </div>
    </div>

    <el-table :data="tasks" stripe>
      <el-table-column prop="id" label="ID" width="60" />
      <el-table-column prop="repoName" label="仓库" width="140" />
      <el-table-column prop="commitSha" label="Commit" width="120">
        <template #default="{ row }">{{ row.commitSha.slice(0, 8) }}</template>
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
      <el-table-column prop="errorMsg" label="失败原因" width="180" show-overflow-tooltip>
        <template #default="{ row }">
          <span v-if="row.status === 3 && row.errorMsg" style="color:#f56c6c;font-size:12px">{{ row.errorMsg }}</span>
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
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { reviewApi, repositoryApi } from '@/api'

const router = useRouter()
const tasks = ref<any[]>([])
const repos = ref<any[]>([])
const filterRepo = ref('')

const statusName = (s: number) => ['待审核', '审核中', '已完成', '失败'][s] || '-'
const statusType = (s: number) => ['', 'warning', 'success', 'danger'][s] || 'info'

onMounted(async () => {
  const [t, r] = await Promise.all([
    reviewApi.tasks(),
    repositoryApi.list()
  ])
  if (t.success) tasks.value = t.data
  if (r.success) repos.value = r.data
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
</script>

<style scoped>
.page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
</style>

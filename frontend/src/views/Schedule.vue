<template>
  <div class="schedule-page">
    <div class="page-header">
      <span class="page-title">📅 定时审核计划</span>
      <el-button type="primary" @click="openAddDialog">
        + 添加计划
      </el-button>
    </div>

    <el-table :data="list" v-loading="loading" stripe>
      <el-table-column prop="id" label="ID" width="60" />
      <el-table-column prop="repoName" label="仓库" min-width="140">
        <template #default="{ row }">
          <span>{{ row.repoName || `仓库#${row.repositoryId}` }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="branchName" label="分支" width="120" />
      <el-table-column prop="cronExpr" label="Cron 表达式" width="140">
        <template #default="{ row }">
          <code style="font-size:12px">{{ row.cronExpr }}</code>
        </template>
      </el-table-column>
      <el-table-column prop="enabled" label="状态" width="80">
        <template #default="{ row }">
          <el-tag :type="row.enabled === 1 ? 'success' : 'info'" size="small">
            {{ row.enabled === 1 ? '启用' : '停用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="lastTriggerAt" label="最后触发" width="160">
        <template #default="{ row }">
          {{ row.lastTriggerAt ? formatTime(row.lastTriggerAt) : '从未触发' }}
        </template>
      </el-table-column>
      <el-table-column label="操作" width="200">
        <template #default="{ row }">
          <el-button size="small" type="primary" link @click="openEditDialog(row)">编辑</el-button>
          <el-button size="small" type="success" link @click="handleTrigger(row)">立即触发</el-button>
          <el-button size="small" type="danger" link @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 添加/编辑弹窗 -->
    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑计划' : '添加计划'" width="480px">
      <el-form :model="form" label-width="90px">
        <el-form-item label="仓库">
          <el-select v-model="form.repositoryId" placeholder="请选择仓库" style="width:100%">
            <el-option v-for="r in repos" :key="r.id" :label="r.repoName" :value="r.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="分支">
          <el-input v-model="form.branchName" placeholder="如 master、main" />
        </el-form-item>
        <el-form-item label="Cron 表达式">
          <el-input v-model="form.cronExpr" placeholder="0 9 * * *" />
          <div class="form-tip">
            格式：秒 分 时 日 月 周<br />
            如 0 9 * * * 表示每天9点<br />
            0 8 * * 1-5 表示工作日8点<br />
            0 */2 * * * 表示每2小时
          </div>
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="form.enabled" :active-value="1" :inactive-value="0" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave" :loading="saveLoading">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { scheduleApi, repositoryApi } from '@/api'

const list = ref<any[]>([])
const repos = ref<any[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const saveLoading = ref(false)

const form = ref({
  id: 0,
  repositoryId: 0,
  branchName: 'master',
  cronExpr: '0 9 * * *',
  enabled: 1
})

onMounted(async () => {
  const [r1, r2] = await Promise.all([
    scheduleApi.list() as Promise<any>,
    repositoryApi.list() as Promise<any>
  ])
  if (r1.success) list.value = r1.data || []
  if (r2.success) repos.value = r2.data || []
})

function openAddDialog() {
  form.value = { id: 0, repositoryId: repos.value[0]?.id || 0, branchName: 'master', cronExpr: '0 9 * * *', enabled: 1 }
  dialogVisible.value = true
}

function openEditDialog(row: any) {
  form.value = { ...row, enabled: row.enabled }
  dialogVisible.value = true
}

async function handleSave() {
  if (!form.value.repositoryId) { ElMessage.warning('请选择仓库'); return }
  if (!form.value.branchName) { ElMessage.warning('请填写分支名'); return }
  if (!form.value.cronExpr) { ElMessage.warning('请填写 Cron 表达式'); return }
  saveLoading.value = true
  try {
    const api = form.value.id ? scheduleApi.update : scheduleApi.add
    const res: any = await api(form.value)
    if (res.success) {
      ElMessage.success('保存成功')
      dialogVisible.value = false
      const r1: any = await scheduleApi.list()
      if (r1.success) list.value = r1.data || []
    } else {
      ElMessage.error(res.msg || '保存失败')
    }
  } finally {
    saveLoading.value = false
  }
}

async function handleTrigger(row: any) {
  const res: any = await scheduleApi.trigger(row.id)
  if (res.success) ElMessage.success('触发成功，请查看审核任务列表')
  else ElMessage.error(res.msg || '触发失败')
}

async function handleDelete(row: any) {
  try {
    await ElMessageBox.confirm(`确定删除计划「${row.repoName || row.branchName}」？`, '确认删除')
    const res: any = await scheduleApi.delete(row.id)
    if (res.success) {
      ElMessage.success('删除成功')
      list.value = list.value.filter(x => x.id !== row.id)
    } else {
      ElMessage.error(res.msg || '删除失败')
    }
  } catch {}
}

function formatTime(t: string) {
  if (!t) return ''
  return new Date(t).toLocaleString('zh-CN', { timeZone: 'Asia/Shanghai' })
}
</script>

<style scoped>
.schedule-page { padding: 20px; }
.page-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; }
.page-title { font-size: 16px; font-weight: 600; }
.form-tip { font-size: 12px; color: #909399; line-height: 1.6; margin-top: 4px; }
</style>

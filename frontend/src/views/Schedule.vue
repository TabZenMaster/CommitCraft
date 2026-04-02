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
      <el-table-column label="执行时间" width="200">
        <template #default="{ row }">
          <span style="font-size:13px">{{ cronDesc(row.cronExpr) }}</span>
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
          {{ row.lastTriggerAt ? formatTime(row.lastTriggerAt) : '从未' }}
        </template>
      </el-table-column>
      <el-table-column label="操作" width="280">
        <template #default="{ row }">
          <el-button size="small" type="primary" link @click="openEditDialog(row)">编辑</el-button>
          <el-button size="small" type="success" link @click="handleTrigger(row)">立即触发</el-button>
          <el-button size="small" type="warning" link @click="openLogDialog(row)">查看日志</el-button>
          <el-button size="small" type="danger" link @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 添加/编辑弹窗 -->
    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑计划' : '添加计划'" width="520px">
      <el-form label-width="90px">
        <el-form-item label="仓库">
          <el-select v-model="form.repositoryId" placeholder="请选择仓库" style="width:100%">
            <el-option v-for="r in repos" :key="r.id" :label="r.repoName" :value="r.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="分支">
          <el-select v-model="form.branchName" placeholder="请选择分支" style="width:100%" :loading="branchLoading">
            <el-option v-for="b in branchList" :key="b.name" :label="b.name" :value="b.name" />
          </el-select>
        </el-form-item>

        <!-- 执行类型切换 -->
        <el-form-item label="执行类型">
          <el-radio-group v-model="form.execType">
            <el-radio value="interval">间隔执行</el-radio>
            <el-radio value="scheduled">定时执行</el-radio>
          </el-radio-group>
        </el-form-item>

        <!-- 间隔执行 -->
        <template v-if="form.execType === 'interval'">
          <el-form-item label="间隔">
            <div style="display:flex;gap:8px;align-items:center">
              <span>每隔</span>
              <el-input-number v-model="form.intervalVal" :min="1" :max="59" style="width:100px" />
              <el-select v-model="form.intervalUnit" style="width:120px">
                <el-option label="分钟" value="minute" />
                <el-option label="小时" value="hour" />
              </el-select>
            </div>
          </el-form-item>
        </template>

        <!-- 定时执行 -->
        <template v-else>
          <el-form-item label="频率">
            <div style="display:flex;gap:8px;align-items:center;flex-wrap:wrap">
              <el-select v-model="form.schedFreq" style="width:130px">
                <el-option label="每天" value="daily" />
                <el-option label="每个工作日" value="workday" />
                <el-option label="每周" value="weekly" />
                <el-option label="每月" value="monthly" />
              </el-select>
              <el-time-picker v-model="form._time" format="HH:mm" value-format="HH:mm" placeholder="选择时间" style="width:120px" />
              <el-select v-if="form.schedFreq === 'weekly'" v-model="form.weekday" style="width:100px">
                <el-option v-for="d in weekDays" :key="d.value" :label="d.label" :value="d.value" />
              </el-select>
              <el-select v-if="form.schedFreq === 'monthly'" v-model="form.monthDay" style="width:100px">
                <el-option v-for="d in monthDays" :key="d" :label="`${d}号`" :value="d" />
              </el-select>
            </div>
          </el-form-item>
        </template>

        <el-form-item label="启用">
          <el-switch v-model="form.enabled" :active-value="1" :inactive-value="0" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave" :loading="saveLoading">保存</el-button>
      </template>
    </el-dialog>

    <!-- 执行日志弹窗 -->
    <el-dialog v-model="logDialogVisible" title="执行日志" width="800px">
      <el-table :data="logList" v-loading="logLoading" stripe size="small">
        <el-table-column prop="triggerAt" label="执行时间" width="160">
          <template #default="{ row }">
            {{ formatTime(row.triggerAt) }}
          </template>
        </el-table-column>
        <el-table-column prop="repoName" label="仓库" min-width="120" />
        <el-table-column prop="branchName" label="分支" width="120" />
        <el-table-column prop="status" label="状态" width="70">
          <template #default="{ row }">
            <el-tag :type="row.status === 1 ? 'success' : 'danger'" size="small">
              {{ row.status === 1 ? '成功' : '失败' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="newCommits" label="新提交" width="70" align="center" />
        <el-table-column prop="enqueued" label="入队" width="70" align="center" />
        <el-table-column prop="durationSeconds" label="耗时(秒)" width="80" align="center" />
        <el-table-column prop="errorMessage" label="失败原因" min-width="150">
          <template #default="{ row }">
            <span style="color:#f56c6c;font-size:12px">{{ row.errorMessage || '—' }}</span>
          </template>
        </el-table-column>
      </el-table>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { scheduleApi, repositoryApi } from '@/api'

const list = ref<any[]>([])
const repos = ref<any[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const saveLoading = ref(false)
const branchList = ref<any[]>([])
const branchLoading = ref(false)
const logDialogVisible = ref(false)
const logList = ref<any[]>([])
const logLoading = ref(false)

const weekDays = [
  { label: '周日', value: '0' },
  { label: '周一', value: '1' },
  { label: '周二', value: '2' },
  { label: '周三', value: '3' },
  { label: '周四', value: '4' },
  { label: '周五', value: '5' },
  { label: '周六', value: '6' },
]
const monthDays = Array.from({ length: 31 }, (_, i) => i + 1)

const form = ref({
  id: 0,
  repositoryId: 0,
  branchName: 'master',
  cronExpr: '0 9 * * *',
  enabled: 1,
  execType: 'scheduled' as 'interval' | 'scheduled',
  intervalVal: 1,
  intervalUnit: 'hour' as 'minute' | 'hour',
  schedFreq: 'daily' as 'daily' | 'workday' | 'weekly' | 'monthly',
  _time: '09:00',
  weekday: '1',
  monthDay: 1
})

onMounted(async () => {
  const [r1, r2] = await Promise.all([
    scheduleApi.list() as Promise<any>,
    repositoryApi.list() as Promise<any>
  ])
  if (r1.success) list.value = r1.data || []
  if (r2.success) repos.value = r2.data || []
})

watch(() => form.value.repositoryId, async (repoId) => {
  branchList.value = []
  if (!repoId) return
  branchLoading.value = true
  const res: any = await repositoryApi.getBranches(repoId)
  branchLoading.value = false
  if (res.success) branchList.value = res.data || []
})

/** 根据当前 form 状态重建 cronExpr，格式：6字段 秒 分 时 日 月 周 */
function rebuildCron() {
  const f = form.value
  // 6字段格式：秒 分 时 日 月 周
  if (f.execType === 'interval') {
    if (f.intervalUnit === 'minute') {
      // 每隔N分钟：秒=0, 分=*/N, 时=*, 日=*, 月=*, 周=*
      f.cronExpr = `0 */${f.intervalVal} * * * *`
    } else {
      // 每隔N小时：秒=0, 分=0, 时=*/N, 日=*, 月=*, 周=*
      f.cronExpr = `0 0 */${f.intervalVal} * * *`
    }
  } else {
    const [h, m] = (f._time || '09:00').split(':')
    const hh = h.padStart(2, '0')
    const mm = m.padStart(2, '0')
    if (f.schedFreq === 'daily') f.cronExpr = `0 ${mm} ${hh} * * * *`
    else if (f.schedFreq === 'workday') f.cronExpr = `0 ${mm} ${hh} * * 1-5 *`
    else if (f.schedFreq === 'weekly') f.cronExpr = `0 ${mm} ${hh} * * ${f.weekday} *`
    else if (f.schedFreq === 'monthly') f.cronExpr = `0 ${mm} ${hh} ${f.monthDay} * * *`
  }
}

/** 把 cronExpr 解析回 form 的派生字段（仅编辑时用） */
function applyCronToForm(cronExpr: string) {
  const p = cronExpr.trim().split(/\s+/)
  if (p.length < 4) return
  // 补齐到 6 字段：秒 分 时 日 月 周
  while (p.length < 6) p.push('*')

  const [, min, hour, day, month, dow] = p

  if (min.startsWith('*/')) {
    Object.assign(form.value, { execType: 'interval', intervalVal: parseInt(min.slice(2)) || 1, intervalUnit: 'minute' })
  } else if (hour.startsWith('*/')) {
    Object.assign(form.value, { execType: 'interval', intervalVal: parseInt(hour.slice(2)) || 1, intervalUnit: 'hour' })
  } else {
    form.value.execType = 'scheduled'
    form.value._time = `${hour.padStart(2, '0')}:${min.padStart(2, '0')}`
    if ((dow === '1-5' || dow === '1,2,3,4,5') && day === '*') form.value.schedFreq = 'workday'
    else if (dow !== '*' && day === '*') form.value.schedFreq = 'weekly'
    else if (day !== '*') form.value.schedFreq = 'monthly'
    else form.value.schedFreq = 'daily'
    if (dow !== '*') form.value.weekday = dow
    if (day !== '*') form.value.monthDay = parseInt(day) || 1
  }
}

function openAddDialog() {
  Object.assign(form.value, {
    id: 0, repositoryId: repos.value[0]?.id || 0, branchName: 'master',
    cronExpr: '0 9 * * *', enabled: 1,
    execType: 'scheduled', intervalVal: 1, intervalUnit: 'hour',
    schedFreq: 'daily', _time: '09:00', weekday: '1', monthDay: 1
  })
  dialogVisible.value = true
  if (form.value.repositoryId) loadBranches(form.value.repositoryId)
}

function openEditDialog(row: any) {
  Object.assign(form.value, {
    id: row.id, repositoryId: row.repositoryId, branchName: row.branchName,
    cronExpr: row.cronExpr, enabled: row.enabled,
    weekday: '1', monthDay: 1
  })
  applyCronToForm(row.cronExpr)
  dialogVisible.value = true
  if (form.value.repositoryId) loadBranches(form.value.repositoryId)
}

async function loadBranches(repoId: number) {
  branchLoading.value = true
  const res: any = await repositoryApi.getBranches(repoId)
  branchLoading.value = false
  if (res.success) branchList.value = res.data || []
}

async function handleSave() {
  if (!form.value.repositoryId) { ElMessage.warning('请选择仓库'); return }
  if (!form.value.branchName) { ElMessage.warning('请选择分支'); return }
  saveLoading.value = true
  try {
    rebuildCron()
    const payload = {
      id: form.value.id,
      repositoryId: form.value.repositoryId,
      branchName: form.value.branchName,
      cronExpr: form.value.cronExpr,
      enabled: form.value.enabled
    }
    const res: any = await (form.value.id ? scheduleApi.update(payload) : scheduleApi.add(payload))
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

async function openLogDialog(row: any) {
  logDialogVisible.value = true
  logLoading.value = true
  const res: any = await scheduleApi.getLogs(row.id, 50)
  logLoading.value = false
  if (res.success) logList.value = res.data || []
  else ElMessage.error(res.msg || '加载日志失败')
}

/**
 * 把 cron 表达式解析成人类可读描述
 * 6字段格式：秒 分 时 日 月 周
 * Cronos 5字段格式：分 时 日 月 周
 */
function cronDesc(expr: string): string {
  if (!expr) return '—'
  const p = expr.trim().split(/\s+/)
  if (p.length < 4) return expr
  while (p.length < 6) p.push('*')
  const [, min, hour, day, month, dow] = p

  // 间隔：每N分钟
  if (min.startsWith('*/')) return `每隔 ${min.slice(2)} 分钟`
  // 间隔：每N小时
  if (hour.startsWith('*/')) return `每隔 ${hour.slice(2)} 小时`
  // 每半小时（分钟位为 */30）
  if (min === '*/30') return `每半小时`

  const timeStr = `${hour.padStart(2, '0')}:${min.padStart(2, '0')}`
  if ((dow === '1-5' || dow === '1,2,3,4,5') && day === '*') return `每个工作日 ${timeStr}`
  if (day === '*' && month === '*' && dow === '*') return `每天 ${timeStr}`
  if (day === '*' && dow !== '*') return `每周${weekdayName(dow)} ${timeStr}`
  if (day !== '*') return `每月${day}号 ${timeStr}`
  return timeStr
}

function weekdayName(d: string): string {
  const map: Record<string, string> = { '0': '周日', '1': '周一', '2': '周二', '3': '周三', '4': '周四', '5': '周五', '6': '周六', '7': '周日' }
  return map[d] || `周${d}`
}

function formatTime(t: string) {
  if (!t) return ''
  // 统一转为 Asia/Shanghai 时区显示；ISO 字符串若缺 Z 需补上
  var d = t.endsWith('Z') || t.includes('+') ? new Date(t) : new Date(t + 'Z')
  return d.toLocaleString('zh-CN', { timeZone: 'Asia/Shanghai' })
}
</script>

<style scoped>
.schedule-page { padding: 20px; }
.page-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px; }
.page-title { font-size: 16px; font-weight: 600; }
</style>

<template>
  <div>
    <div class="page-header">
      <div class="page-title">🗂 问题处理台</div>
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
        <el-select v-model="filterStatus" clearable placeholder="状态" style="width:120px" @change="onFilterChange">
          <el-option label="全部" value="" />
          <el-option label="待处理" :value="0" />
          <el-option label="已认领" :value="1" />
          <el-option label="已修复" :value="2" />
          <el-option label="已忽略" :value="3" />
        </el-select>
        <el-button @click="loadData">刷新</el-button>
      </div>
    </div>

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
          <el-tag size="small" :type="statusTag(row.status)">{{ statusName(row.status) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="handlerName" label="处理人" width="100" />
      <el-table-column label="操作" width="180" fixed="right">
        <template #default="{ row }">
          <template v-if="row.status === 0">
            <el-button size="small" type="primary" @click="handleClaim(row)">认领</el-button>
            <el-button size="small" type="danger" plain @click="openIgnore(row)">忽略</el-button>
          </template>
          <template v-else-if="row.status === 1">
            <el-button size="small" type="success" @click="openFix(row)">标记修复</el-button>
            <el-button size="small" type="info" plain @click="openIgnore(row)">忽略</el-button>
          </template>
          <template v-else>
            <el-button size="small" type="info" plain @click="showDetail(row)">详情</el-button>
          </template>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-model:current-page="pageIndex"
      v-model:page-size="pageSize"
      :page-sizes="[20, 50, 100, 200]"
      :total="total"
      layout="total, sizes, prev, pager, next, jumper"
      @size-change="loadData"
      @current-change="loadData"
      style="margin-top:16px;justify-content:center" />

    <!-- 修复弹窗 -->
    <el-dialog v-model="fixVisible" title="标记修复" width="420px">
      <el-form :model="fixForm" label-width="80px">
        <el-form-item label="处理备注">
          <el-input v-model="fixForm.memo" type="textarea" :rows="3" placeholder="描述修复方案（可选）" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="fixVisible = false">取消</el-button>
        <el-button type="success" @click="doFix">确认修复</el-button>
      </template>
    </el-dialog>

    <!-- 忽略弹窗 -->
    <el-dialog v-model="ignoreVisible" title="标记忽略" width="420px">
      <el-form :model="ignoreForm" label-width="80px">
        <el-form-item label="忽略理由" required>
          <el-input v-model="ignoreForm.memo" type="textarea" :rows="3" placeholder="必须填写忽略理由" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="ignoreVisible = false">取消</el-button>
        <el-button type="info" @click="doIgnore">确认忽略</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { reviewApi, repositoryApi } from '@/api'

const results = ref<any[]>([])
const repos = ref<any[]>([])
const filterRepo = ref('')
const filterSev = ref('')
const filterStatus = ref('')
const pageIndex = ref(1)
const pageSize = ref(50)
const total = ref(0)

const fixVisible = ref(false)
const fixForm = ref({ id: 0, memo: '' })
const ignoreVisible = ref(false)
const ignoreForm = ref({ id: 0, memo: '' })

const sevTag = (s: string) => ({ critical: 'danger', major: 'warning', minor: '', suggestion: 'info' }[s] || '')
const sevName = (s: string) => ({ critical: '致命', major: '严重', minor: '一般', suggestion: '建议' }[s] || s)
const typeTag = (s: string) => ({ security: 'danger', correctness: 'danger', performance: 'success', maintainability: 'info', best_practice: 'purple', code_style: '', other: 'info' }[s] || '')
const typeName = (s: string) => ({ security: '安全', correctness: '正确性', performance: '性能', maintainability: '可维护性', best_practice: '最佳实践', code_style: '代码风格', other: '其他' }[s] || s)
const statusTag = (s: number) => ['', 'warning', 'success', 'info'][s] || 'info'
const statusName = (s: number) => ['待处理', '已认领', '已修复', '已忽略'][s] || '-'

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
    status: filterStatus.value !== '' ? Number(filterStatus.value) : undefined,
    pageIndex: pageIndex.value,
    pageSize: pageSize.value
  })
  if (res.success) {
    const paged = res.data as any
    results.value = paged?.data ?? []
    total.value = paged?.total ?? 0
  }
}

async function handleClaim(row: any) {
  const res: any = await reviewApi.claim(row.id)
  if (res.success) { ElMessage.success('已认领'); loadData() }
  else ElMessage.error(res.msg)
}

function openFix(row: any) { fixForm.value = { id: row.id, memo: '' }; fixVisible.value = true }
async function doFix() {
  const res: any = await reviewApi.handle({ id: fixForm.value.id, status: 2, memo: fixForm.value.memo })
  if (res.success) { ElMessage.success('已标记修复'); fixVisible.value = false; loadData() }
  else ElMessage.error(res.msg)
}

function openIgnore(row: any) { ignoreForm.value = { id: row.id, memo: '' }; ignoreVisible.value = true }
async function doIgnore() {
  if (!ignoreForm.value.memo) { ElMessage.warning('请填写忽略理由'); return }
  const res: any = await reviewApi.handle({ id: ignoreForm.value.id, status: 3, memo: ignoreForm.value.memo })
  if (res.success) { ElMessage.success('已标记忽略'); ignoreVisible.value = false; loadData() }
  else ElMessage.error(res.msg)
}

function showDetail(row: any) {
  ElMessage.info(`${row.description}\n\n修复建议: ${row.suggestion}\n\n处理备注: ${row.handlerMemo || '-'}`)
}
</script>

<style scoped>
.page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; flex-wrap: wrap; gap: 8px; }
</style>

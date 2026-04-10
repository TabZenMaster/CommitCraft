<template>
  <div>
    <div class="page-header">
      <div class="page-title">模型配置</div>
      <el-button type="primary" @click="openDialog()">+ 新增模型</el-button>
    </div>

    <!-- 桌面端表格 -->
    <el-table v-if="!isMobile" v-loading="loading" :data="list" stripe style="width:100%">
      <el-table-column prop="id" label="ID" width="60" align="center" />
      <el-table-column prop="name" label="模型名称" min-width="140" show-overflow-tooltip />
      <el-table-column prop="apiBase" label="API 地址" min-width="200" show-overflow-tooltip />
      <el-table-column prop="description" label="描述" min-width="180" show-overflow-tooltip />
      <el-table-column prop="status" label="状态" width="80" align="center">
        <template #default="{ row }">
          <span class="table-tag" :class="row.status === 1 ? 'success' : 'info'">
            {{ row.status === 1 ? '启用' : '停用' }}
          </span>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="{ row }">
          <div class="action-btns">
            <button class="action-link success" @click="handleTest(row)">测试</button>
            <button class="action-link" @click="openDialog(row)">编辑</button>
            <button class="action-link danger" @click="handleDelete(row)">删除</button>
          </div>
        </template>
      </el-table-column>
    </el-table>

    <!-- 移动端卡片列表 -->
    <div v-else class="card-list">
      <TableCard
        v-for="row in list"
        :key="row.id"
        :row="row"
        :columns="cardColumns"
      >
        <template #header>
          <div class="card-name">{{ row.name }}</div>
          <div class="card-header-right">
            <span class="table-tag" :class="row.status === 1 ? 'success' : 'info'">
              {{ row.status === 1 ? '启用' : '停用' }}
            </span>
          </div>
        </template>
        <template #apiBase="{ row }">
          <span class="cell-text text-muted">{{ row.apiBase }}</span>
        </template>
        <template #description="{ row }">
          <span class="cell-text text-muted">{{ row.description || '-' }}</span>
        </template>
        <template #footer>
          <button class="action-link success" @click="handleTest(row)">测试</button>
          <button class="action-link" @click="openDialog(row)">编辑</button>
          <button class="action-link danger" @click="handleDelete(row)">删除</button>
        </template>
      </TableCard>
      <el-empty v-if="list.length === 0" description="暂无数据" :image-size="60" />
    </div>

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

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑模型' : '新增模型'" width="500px">
      <el-form :model="form" label-width="90px">
        <el-form-item label="模型名称">
          <el-input v-model="form.name" placeholder="如 MiniMax-M2.7" />
        </el-form-item>
        <el-form-item label="API 地址">
          <el-input v-model="form.apiBase" placeholder="http://model.imfan.top" />
        </el-form-item>
        <el-form-item label="API Key">
          <el-input v-model="form.apiKey" placeholder="sk-xxx" show-password />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="form.description" type="textarea" :rows="2" />
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="form.status">
            <el-radio :value="1">启用</el-radio>
            <el-radio :value="0">停用</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { modelConfigApi } from '@/api'
import TableCard from '@/components/TableCard.vue'
import { useBreakpoint } from '@/composables/useBreakpoint'

const { breakpoint } = useBreakpoint()
const isMobile = computed(() => breakpoint.value === 'xs' || breakpoint.value === 'sm')
const pageLayout = computed(() => isMobile.value ? 'total, prev, pager, next' : 'total, sizes, prev, pager, next')

const list = ref<any[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const form = ref<any>({ status: 1 })
const pageIndex = ref(1)
const pageSize = ref(50)
const total = ref(0)

const cardColumns = [
  { key: 'apiBase', label: 'API 地址' },
  { key: 'description', label: '描述' },
]

onMounted(loadData)

async function loadData() {
  loading.value = true
  const res: any = await modelConfigApi.list()
  if (res.success) {
    list.value = res.data ?? []
    total.value = list.value.length
  }
  loading.value = false
}

function openDialog(row?: any) {
  form.value = row ? { ...row } : { status: 1 }
  dialogVisible.value = true
}

async function handleSave() {
  const api = form.value.id ? modelConfigApi.update : modelConfigApi.add
  const res: any = await api(form.value)
  if (res.success) {
    ElMessage.success('保存成功')
    dialogVisible.value = false
    loadData()
  } else {
    ElMessage.error(res.msg)
  }
}

async function handleTest(row: any) {
  const res: any = await modelConfigApi.test(row.id)
  if (res.success) {
    ElMessage.success(res.data?.message || '连接成功')
  } else {
    ElMessage.error(res.msg || '连接失败')
  }
}

async function handleDelete(row: any) {
  await ElMessageBox.confirm('确认删除？', '提示')
  const res: any = await modelConfigApi.delete(row.id)
  if (res.success) { ElMessage.success('删除成功'); loadData() }
  else ElMessage.error(res.msg)
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

.card-name {
  flex: 1;
  font-family: var(--font-display);
  font-size: 13px;
  font-weight: 600;
  color: var(--text-primary);
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

.cell-text {
  font-size: 13px;
  line-height: 1.5;
  color: var(--text-primary);
  word-break: break-all;
}
</style>

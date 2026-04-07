<template>
  <div class="page-container">
    <div class="page-header">
      <div class="page-title">🤖 模型配置</div>
      <el-button type="primary" @click="openDialog()" class="btn-primary-sm">+ 新增模型</el-button>
    </div>

    <div class="sentry-card table-card">
      <el-table :data="list" stripe>
        <el-table-column prop="id" label="ID" width="60" />
        <el-table-column prop="name" label="模型名称" />
        <el-table-column prop="apiBase" label="API 地址" show-overflow-tooltip />
        <el-table-column prop="description" label="描述" show-overflow-tooltip />
        <el-table-column prop="status" label="状态" width="80">
          <template #default="{ row }">
            <el-tag :type="row.status === 1 ? 'success' : 'info'" size="small">
              {{ row.status === 1 ? '启用' : '停用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="200">
          <template #default="{ row }">
            <el-button size="small" type="success" @click="handleTest(row)">测试</el-button>
            <el-button size="small" @click="openDialog(row)">编辑</el-button>
            <el-button size="small" type="danger" @click="handleDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

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
import { ref, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { modelConfigApi } from '@/api'

const list = ref<any[]>([])
const dialogVisible = ref(false)
const form = ref<any>({ status: 1 })

onMounted(loadData)

async function loadData() {
  const res: any = await modelConfigApi.list()
  if (res.success) list.value = res.data
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
.page-container { padding: 20px 24px; background: var(--bg-primary); min-height: 100vh; }
.page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; }
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

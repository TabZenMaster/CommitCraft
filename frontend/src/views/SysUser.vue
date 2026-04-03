<template>
  <div>
    <div class="page-header">
      <div class="page-title">👥 用户管理</div>
      <el-button type="primary" @click="openDialog()">+ 新增用户</el-button>
    </div>

    <el-table :data="list" stripe>
      <el-table-column prop="id" label="ID" width="60" />
      <el-table-column prop="username" label="用户名" />
      <el-table-column prop="realName" label="姓名" />
      <el-table-column prop="role" label="角色" width="120">
        <template #default="{ row }">
          <el-tag :type="row.role === 'admin' ? 'danger' : row.role === 'reviewer' ? 'warning' : 'info'" size="small">
            {{ roleName(row.role) }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="status" label="状态" width="80">
        <template #default="{ row }">
          <el-tag :type="row.status === 1 ? 'success' : 'info'" size="small">{{ row.status === 1 ? '启用' : '停用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="160">
        <template #default="{ row }">
          <el-button size="small" @click="openDialog(row)">编辑</el-button>
          <el-button v-if="row.username !== 'admin'" size="small" type="danger" @click="handleDelete(row)">删除</el-button>
            <span v-else style="color:#999;font-size:12px">-</span>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑用户' : '新增用户'" width="420px">
      <el-form :model="form" label-width="80px">
        <el-form-item label="用户名">
          <el-input v-model="form.username" :disabled="!!form.id" />
        </el-form-item>
        <el-form-item label="姓名">
          <el-input v-model="form.realName" />
        </el-form-item>
        <el-form-item label="密码" v-if="!form.id">
          <el-input v-model="form.password" type="password" show-password placeholder="默认 123456" />
        </el-form-item>
        <el-form-item label="角色">
          <el-select v-model="form.role" style="width:100%">
            <el-option label="管理员" value="admin" />
            <el-option label="审核员" value="reviewer" />
            <el-option label="开发者" value="developer" />
          </el-select>
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
import { sysUserApi } from '@/api'

const list = ref<any[]>([])
const dialogVisible = ref(false)
const form = ref<any>({ status: 1, role: 'developer' })

onMounted(loadData)

async function loadData() {
  const res: any = await sysUserApi.list()
  if (res.success) list.value = res.data
}

function openDialog(row?: any) {
  form.value = row ? { ...row } : { status: 1, role: 'developer' }
  dialogVisible.value = true
}

async function handleSave() {
  if (!form.value.username || !form.value.realName) {
    ElMessage.warning('请填写用户名和姓名')
    return
  }
  if (!form.value.id) form.value.password = form.value.password || '123456'
  const api = form.value.id ? sysUserApi.update : sysUserApi.add
  const res: any = await api(form.value)
  if (res.success) {
    ElMessage.success('保存成功')
    dialogVisible.value = false
    loadData()
  } else ElMessage.error(res.msg)
}

async function handleDelete(row: any) {
  await ElMessageBox.confirm('确认删除？', '提示')
  const res: any = await sysUserApi.delete(row.id)
  if (res.success) { ElMessage.success('删除成功'); loadData() }
  else ElMessage.error(res.msg)
}

function roleName(role: string) {
  return { admin: '管理员', reviewer: '审核员', developer: '开发者' }[role] || role
}
</script>

<style scoped>
.page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
</style>

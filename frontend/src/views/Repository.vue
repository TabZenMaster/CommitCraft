<template>
  <div class="page-container">
    <div class="page-header">
      <div class="page-title">📦 仓库管理</div>
      <el-button type="primary" @click="openDialog()">+ 新增仓库</el-button>
    </div>

    <el-table :data="list" stripe>
      <el-table-column prop="id" label="ID" width="60" />
      <el-table-column prop="repoName" label="仓库名称" />
      <el-table-column prop="repoUrl" label="仓库地址" show-overflow-tooltip />
      <el-table-column prop="modelName" label="绑定模型" width="150" />
      <el-table-column prop="status" label="状态" width="80">
        <template #default="{ row }">
          <el-tag :type="row.status === 1 ? 'success' : 'info'" size="small">{{ row.status === 1 ? '启用' : '停用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="280" class-name="op-col">
        <template #default="{ row }">
          <el-button size="small" @click="openDialog(row)">编辑</el-button>
          <el-button size="small" type="info" @click="handleTest(row)">测试连接</el-button>
          <el-button size="small" type="primary" @click="openTrigger(row)">立即审核</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 新增/编辑弹窗 -->
    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑仓库' : '新增仓库'" width="560px">
      <el-form :model="form" label-width="100px">
        <el-form-item label="仓库名称">
          <el-input v-model="form.repoName" disabled placeholder="保存后自动获取或点击获取">
            <template #append>
              <el-button @click="handleFetchRepoName" :loading="refreshLoading">获取名称</el-button>
            </template>
          </el-input>
        </el-form-item>
        <el-form-item label="仓库地址">
          <el-input v-model="form.repoUrl" placeholder="如 https://gitee.com/jnrobot/fms-backend" />
        </el-form-item>
        <el-form-item label="Access Token">
          <el-input v-model="form.giteeToken" type="password" show-password placeholder="私有仓库 Access Token（公共仓库可留空）" />
        </el-form-item>
        <el-form-item label="绑定模型">
          <el-select v-model="form.modelConfigId" style="width:100%">
            <el-option v-for="m in models" :key="m.id" :label="m.name" :value="m.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="状态">
          <el-radio-group v-model="form.status">
            <el-radio :label="1">启用</el-radio>
            <el-radio :label="0">停用</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave">保存</el-button>
      </template>
    </el-dialog>

    <!-- 触发审核：选分支 → 选 commit → 确认 -->
    <el-dialog v-model="triggerVisible" title="选择 Commit 审核" width="860px">
      <div v-if="!selectedCommit">
        <div style="margin-bottom:16px">
          <span style="font-size:14px;color:#666;margin-right:12px">选择分支：</span>
          <el-select v-model="selectedBranch" placeholder="请选择分支" style="width:300px" :loading="branchLoading" @change="loadCommits">
            <el-option v-for="b in branchList" :key="b.name" :label="b.name" :value="b.name" />
          </el-select>
          <span v-if="branchList.length" style="margin-left:12px;font-size:12px;color:#999">{{ branchList.length }} 个分支</span>
        </div>
        <div v-if="!selectedBranch" style="text-align:center;padding:40px;color:#999">请先选择分支</div>
        <div v-else>
          <div style="margin-bottom:8px;padding:0 8px;font-size:12px;color:#999">分支「{{ selectedBranch }}」，点击「审核」选择该 Commit</div>
          <div v-for="c in commitList" :key="c.sha" class="commit-row">
            <div style="display:flex;align-items:flex-start;gap:12px">
              <div style="flex-shrink:0;text-align:right;width:90px">
                <div class="commit-sha">{{ c.sha?.slice(0, 7) }}</div>
                <div style="font-size:12px;color:#999">{{ c.committer }}</div>
                <div style="font-size:12px;color:#bbb">{{ c.committedAt?.replace('T',' ').slice(0,16) }}</div>
              </div>
              <div style="flex:1;min:0">
                <div class="commit-msg">{{ c.message }}</div>
                <div style="margin-top:8px;text-align:right">
                  <el-button size="small" type="primary" @click="selectCommit(c)">审核</el-button>
                </div>
              </div>
            </div>
          </div>
          <div v-if="commitLoading" style="text-align:center;padding:24px;color:#999">加载中...</div>
          <div v-else-if="!commitList.length" style="text-align:center;padding:24px;color:#999">该分支暂无提交记录</div>
        </div>
      </div>
      <div v-else>
        <el-alert type="info" :closable="false" style="margin-bottom:16px">确认审核以下 Commit：</el-alert>
        <el-descriptions :column="2" border size="small">
          <el-descriptions-item label="分支">{{ selectedBranch }}</el-descriptions-item>
          <el-descriptions-item label="SHA">{{ selectedCommit.sha?.slice(0, 8) }}</el-descriptions-item>
          <el-descriptions-item label="提交人">{{ selectedCommit.committer }}</el-descriptions-item>
          <el-descriptions-item label="时间">{{ selectedCommit.committedAt?.replace('T',' ').slice(0,16) }}</el-descriptions-item>
          <el-descriptions-item label="信息" :span="2">{{ selectedCommit.message }}</el-descriptions-item>
        </el-descriptions>
        <div style="margin-top:20px;display:flex;gap:12px;justify-content:flex-end">
          <el-button @click="selectedCommit = null">返回列表</el-button>
          <el-button type="primary" :loading="triggerLoading" @click="doTrigger">确认审核</el-button>
        </div>
      </div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { repositoryApi, reviewApi } from '@/api'

const list = ref<any[]>([])
const models = ref<any[]>([])
const dialogVisible = ref(false)
const form = ref<any>({})

const triggerVisible = ref(false)
const selectedCommit = ref<any>(null)
const triggerLoading = ref(false)
const commitList = ref<any[]>([])
const commitLoading = ref(false)
const branchList = ref<any[]>([])
const branchLoading = ref(false)
const selectedBranch = ref('')
const refreshLoading = ref(false)
let triggerRepoId = 0

onMounted(() => { loadData() })

async function loadData() {
  const [repoRes, modelRes] = await Promise.all([
    repositoryApi.list() as Promise<any>,
    repositoryApi.models() as Promise<any>,
  ])
  if (repoRes.success) list.value = repoRes.data || []
  if (modelRes.success) models.value = modelRes.data || []
}

async function openDialog(row?: any) {
  if (row) {
    form.value = { ...row, giteeToken: row.giteeToken || '***' }
  } else {
    form.value = { status: 1, modelConfigId: models.value[0]?.id, repoName: '', repoUrl: '', giteeToken: '' }
  }
  dialogVisible.value = true
}

async function handleTest(row: any) {
  const res: any = await repositoryApi.test(row.id)
  if (res.success) {
    ElMessage.success('连接成功：' + (res.data?.message || '仓库可访问'))
  } else {
    ElMessage.error(res.msg || '连接失败')
  }
}

async function handleSave() {
  if (!form.value.repoUrl) { ElMessage.warning('请填写仓库地址'); return }
  const api = form.value.id ? repositoryApi.update : repositoryApi.add
  const res: any = await api(form.value)
  if (res.success) {
    ElMessage.success('保存成功')
    dialogVisible.value = false
    loadData()
  } else ElMessage.error(res.msg || '保存失败')
}

async function handleFetchRepoName() {
  if (!form.value.repoUrl) { ElMessage.warning('请先填写仓库地址'); return }
  refreshLoading.value = true
  const res: any = await repositoryApi.fetch({ repoId: form.value.id, repoUrl: form.value.repoUrl })
  refreshLoading.value = false
  if (res.success) {
    form.value.repoName = res.data?.name || ''
    ElMessage.success('已获取：' + (res.data?.name || ''))
  } else ElMessage.error(res.msg || '获取仓库名称失败')
}

async function openTrigger(row: any) {
  triggerRepoId = row.id
  triggerVisible.value = true
  selectedCommit.value = null
  selectedBranch.value = ''
  branchList.value = []
  commitList.value = []
  branchLoading.value = true
  const res: any = await repositoryApi.getBranches(row.id)
  branchLoading.value = false
  if (res.success) branchList.value = res.data || []
  else ElMessage.error(res.msg || '获取分支失败')
}

async function loadCommits() {
  if (!selectedBranch.value) return
  selectedCommit.value = null
  commitList.value = []
  commitLoading.value = true
  const res: any = await repositoryApi.getCommits(triggerRepoId, selectedBranch.value)
  commitLoading.value = false
  if (res.success) commitList.value = res.data || []
  else ElMessage.error(res.msg || '获取提交记录失败')
}

function selectCommit(c: any) {
  selectedCommit.value = c
}

async function doTrigger() {
  if (!selectedCommit.value) return
  triggerLoading.value = true
  const res: any = await reviewApi.trigger({
    repositoryId: triggerRepoId,
    commitSha: selectedCommit.value.sha,
    commitMessage: selectedCommit.value.message,
    committer: selectedCommit.value.committer,
    committedAt: selectedCommit.value.committedAt,
    branchName: selectedBranch.value,
  })
  triggerLoading.value = false
  if (res.success) {
    ElMessage.success('审核任务已创建')
    triggerVisible.value = false
  } else ElMessage.error(res.msg || '触发失败')
}
</script>

<style scoped>
.repository-page { }
.page-header { margin-bottom: 16px; }
.commit-row { padding: 12px 8px; border-bottom: 1px solid #f0f0f0; }
.commit-row:hover { background: #f5f7fa; }
.commit-sha { font-family: monospace; color: #409eff; font-size: 12px; }
:deep(.op-col .cell) { display: flex; gap: 4px; align-items: center; flex-wrap: nowrap; }
.commit-msg { font-size: 13px; color: #444; line-height: 1.6; word-break: break-all; }
</style>

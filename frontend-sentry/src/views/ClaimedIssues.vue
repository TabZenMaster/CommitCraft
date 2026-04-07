<template>
  <div class="page-container">
    <div class="page-header">
      <div class="page-title">⏳ 待处理问题</div>
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
        <el-select v-model="filterType" clearable placeholder="问题类型" style="width:120px" @change="onFilterChange">
          <el-option label="全部" value="" />
          <el-option label="安全" value="security" />
          <el-option label="正确性" value="correctness" />
          <el-option label="性能" value="performance" />
          <el-option label="可维护性" value="maintainability" />
          <el-option label="最佳实践" value="best_practice" />
          <el-option label="代码风格" value="code_style" />
          <el-option label="其他" value="other" />
        </el-select>
        <el-button @click="loadData" class="btn-primary-sm">刷新</el-button>
      </div>
    </div>

    <div class="sentry-card table-card">
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
        <el-table-column label="待处理人" width="100">
          <template #default="{ row }">
            <span v-if="row.handlerName" class="handler-link" @click="openAssign(row)">{{ row.handlerName }}</span>
            <span v-else style="color:var(--text-muted)">-</span>
          </template>
        </el-table-column>
        <el-table-column label="代码" width="90">
          <template #default="{ row }">
            <el-button v-if="row.diffContent" size="small" @click="openCode(row.diffContent)">查看代码</el-button>
            <span v-else style="color:var(--text-muted);font-size:12px">-</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="160" fixed="right">
          <template #default="{ row }">
            <el-button size="small" type="success" @click="openFix(row)">标记修复</el-button>
            <el-button size="small" type="info" plain @click="openIgnore(row)">忽略</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

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
        <el-form-item label="修复方案" required>
          <el-input v-model="fixForm.memo" type="textarea" :rows="3" placeholder="描述修复方案（必填）" />
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

    <!-- 分配弹窗 -->
    <el-dialog v-model="assignVisible" title="重新分配" width="400px">
      <el-form :model="assignForm" label-width="80px">
        <el-form-item label="分配给">
          <el-select v-model="assignForm.targetUserId" placeholder="选择用户" style="width:100%">
            <el-option v-for="u in allUsers" :key="u.id" :label="u.realName + ' (' + u.username + ')(' + roleName(u.role) + ')'" :value="u.id" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="assignVisible = false">取消</el-button>
        <el-button type="primary" @click="doAssign" :disabled="!assignForm.targetUserId">确认分配</el-button>
      </template>
    </el-dialog>

    <!-- 全屏代码查看器 -->
    <Teleport to="body">
      <div v-if="overlayVisible" class="cv-overlay" @click.self="closeOverlay">
        <div class="cv-panel">
          <div class="cv-topbar">
            <span v-if="diffStats" class="cv-add">+{{ diffStats.additions }}</span>
            <span v-if="diffStats" class="cv-del">-{{ diffStats.deletions }}</span>
            <button class="cv-close" @click="closeOverlay">✕</button>
          </div>
          <div ref="diffRef" class="cv-diff"></div>
        </div>
      </div>
    </Teleport>
  </div>
</template>

<script setup lang="ts">
import { ref, nextTick, onMounted, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { reviewApi, repositoryApi, sysUserApi } from '@/api'
import { html } from 'diff2html'
import 'diff2html/bundles/css/diff2html.min.css'

const results = ref<any[]>([])
const repos = ref<any[]>([])
const filterRepo = ref('')
const filterSev = ref('')
const filterType = ref('')
const pageIndex = ref(1)
const pageSize = ref(50)
const total = ref(0)

const fixVisible = ref(false)
const fixForm = ref({ id: 0, memo: '' })
const ignoreVisible = ref(false)
const ignoreForm = ref({ id: 0, memo: '' })
const assignVisible = ref(false)
const assignForm = ref({ id: 0, targetUserId: undefined as number | undefined })
const allUsers = ref<any[]>([])

const overlayVisible = ref(false)
const diffRef = ref<HTMLElement>()
const diffStats = ref<{ additions: number; deletions: number } | null>(null)

watch(overlayVisible, (v) => {
  document.documentElement.classList[v ? 'add' : 'remove']('cv-lock')
})

function openCode(diffContent: string) {
  overlayVisible.value = true
  nextTick(() => renderDiff(diffContent))
}

function closeOverlay() {
  overlayVisible.value = false
}

function renderDiff(content: string) {
  if (!diffRef.value) return
  if (!content.trim()) {
    diffStats.value = null
    diffRef.value.innerHTML = '<p style="color:#999;padding:40px">无差异内容</p>'
    return
  }
  const lines = content.split('\n')
  let a = 0, d = 0
  for (const l of lines) {
    if (l.startsWith('+') && !l.startsWith('+++')) a++
    else if (l.startsWith('-') && !l.startsWith('---')) d++
  }
  diffStats.value = { additions: a, deletions: d }
  diffRef.value.innerHTML = html(content, {
    drawFileList: false,
    fileContentToggle: false,
    highlight: true,
    synchronizedScroll: true,
    matching: 'lines',
    outputFormat: 'side-by-side',
    colorScheme: 'dark'
  }) as string
  nextTick(() => {
    if (!diffRef.value) return
    const diffContainers = diffRef.value.querySelectorAll('.d2h-files-diff')
    diffContainers.forEach(container => {
      const panes = container.querySelectorAll('.d2h-file-side-diff')
      if (panes.length === 2) {
        const leftPane = panes[0] as HTMLElement
        const rightPane = panes[1] as HTMLElement
        let isSyncingLeftScroll = false
        let isSyncingRightScroll = false
        leftPane.addEventListener('scroll', () => {
          if (!isSyncingLeftScroll) { isSyncingRightScroll = true; rightPane.scrollTop = leftPane.scrollTop; rightPane.scrollLeft = leftPane.scrollLeft }
          isSyncingLeftScroll = false
        })
        rightPane.addEventListener('scroll', () => {
          if (!isSyncingRightScroll) { isSyncingLeftScroll = true; leftPane.scrollTop = rightPane.scrollTop; leftPane.scrollLeft = rightPane.scrollLeft }
          isSyncingRightScroll = false
        })
      }
    })
  })
}

const sevTag = (s: string) => ({ critical: 'danger', major: 'warning', minor: '', suggestion: 'info' }[s] || '')
const sevName = (s: string) => ({ critical: '致命', major: '严重', minor: '一般', suggestion: '建议' }[s] || s)
const typeTag = (s: string) => ({ security: 'danger', correctness: 'danger', performance: 'success', maintainability: 'info', best_practice: 'warning', code_style: '', other: 'info' }[s] || '')
const typeName = (s: string) => ({ security: '安全', correctness: '正确性', performance: '性能', maintainability: '可维护性', best_practice: '最佳实践', code_style: '代码风格', other: '其他' }[s] || s)
const roleName = (role: string) => ({ admin: '管理员', reviewer: '审核员', developer: '开发者' }[role] || role)

onMounted(async () => {
  const r2 = await repositoryApi.list()
  if (r2.success) repos.value = r2.data
  Promise.allSettled([sysUserApi.list()]).then(([usersResult]) => {
    if (usersResult.status === 'fulfilled' && usersResult.value.success)
      allUsers.value = usersResult.value.data || []
  })
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
    issueType: filterType.value || undefined,
    status: 1,
    pageIndex: pageIndex.value,
    pageSize: pageSize.value
  })
  if (res.success) {
    const paged = res.data as any
    results.value = paged?.data ?? []
    total.value = paged?.total ?? 0
  }
}

function openFix(row: any) { fixForm.value = { id: row.id, memo: '' }; fixVisible.value = true }
async function doFix() {
  if (!fixForm.value.memo) { ElMessage.warning('请填写修复方案'); return }
  const res: any = await reviewApi.handle({ id: fixForm.value.id, status: 2, memo: fixForm.value.memo })
  if (res.success) { ElMessage.success('已标记修复'); fixVisible.value = false; loadData() }
  else ElMessage.error(res.msg)
}

function openIgnore(row: any) { ignoreForm.value = { id: row.id, memo: '' }; ignoreVisible.value = true }

function openAssign(row: any) { assignForm.value = { id: row.id, targetUserId: undefined }; assignVisible.value = true }
async function doAssign() {
  if (!assignForm.value.targetUserId) { ElMessage.warning('请选择要分配的用户'); return }
  const res: any = await reviewApi.assign({ id: assignForm.value.id, targetUserId: assignForm.value.targetUserId })
  if (res.success) { ElMessage.success(res.data || '已分配'); assignVisible.value = false; loadData() }
  else ElMessage.error(res.msg)
}
async function doIgnore() {
  if (!ignoreForm.value.memo) { ElMessage.warning('请填写忽略理由'); return }
  const res: any = await reviewApi.handle({ id: ignoreForm.value.id, status: 3, memo: ignoreForm.value.memo })
  if (res.success) { ElMessage.success('已标记忽略'); ignoreVisible.value = false; loadData() }
  else ElMessage.error(res.msg)
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

.handler-link { color: var(--lime); cursor: pointer; }
.handler-link:hover { text-decoration: underline; }
</style>

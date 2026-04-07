<template>
  <div>
    <div class="page-header">
      <div class="page-title">待处理问题</div>
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
        <el-button @click="loadData">刷新</el-button>
      </div>
    </div>

    <el-table :data="results" stripe style="width:100%">
      <el-table-column type="index" width="50" align="center" />
      <el-table-column prop="filePath" label="文件" min-width="200" show-overflow-tooltip />
      <el-table-column label="行号" width="110" align="center">
        <template #default="{ row }">{{ row.lineStart || '-' }}~{{ row.lineEnd || '-' }}</template>
      </el-table-column>
      <el-table-column prop="issueType" label="类型" width="100" align="center">
        <template #default="{ row }">
          <span class="table-tag" :class="typeTag(row.issueType)">{{ typeName(row.issueType) }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="severity" label="严重程度" width="100" align="center">
        <template #default="{ row }">
          <span class="table-tag" :class="sevTag(row.severity)">{{ sevName(row.severity) }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="description" label="问题描述" min-width="220" show-overflow-tooltip />
      <el-table-column prop="suggestion" label="修复建议" min-width="180" show-overflow-tooltip />
      <el-table-column label="待处理人" width="100" align="center">
        <template #default="{ row }">
          <span v-if="row.handlerName" class="action-link" @click="openAssign(row)">{{ row.handlerName }}</span>
          <span v-else class="text-muted">-</span>
        </template>
      </el-table-column>
      <el-table-column label="代码" width="80" align="center">
        <template #default="{ row }">
          <button v-if="row.diffContent" class="action-link" @click="openCode(row.diffContent)">查看</button>
          <span v-else class="text-muted">-</span>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="120" fixed="right">
        <template #default="{ row }">
          <div class="action-btns">
            <button class="action-link success" @click="openFix(row)">修复</button>
            <button class="action-link" @click="openIgnore(row)">忽略</button>
          </div>
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
import { ref, nextTick, onMounted, watch, computed } from 'vue'
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

const sevTag = (s: string) => ({ critical: 'danger', major: 'warning', minor: 'info', suggestion: 'info' }[s] || 'info')
const sevName = (s: string) => ({ critical: '致命', major: '严重', minor: '一般', suggestion: '建议' }[s] || s)
const typeTag = (s: string) => ({ security: 'danger', correctness: 'danger', performance: 'success', maintainability: 'info', best_practice: 'purple', code_style: 'info', other: 'info' }[s] || 'info')
const typeName = (s: string) => ({ security: '安全', correctness: '正确性', performance: '性能', maintainability: '可维护性', best_practice: '最佳实践', code_style: '代码风格', other: '其他' }[s] || s)
const roleName = (role: string) => ({ admin: '管理员', reviewer: '审核员', developer: '开发者' }[role] || role)

const currentUser = JSON.parse(localStorage.getItem('cr_user') || '{}')
const canAssign = computed(() => currentUser.role === 'admin' || currentUser.role === 'reviewer')

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
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
  flex-wrap: wrap;
  gap: 8px;
  background: var(--bg-surface);
  border: 1px solid var(--border-default);
  padding: 12px 16px;
}

.page-title {
  font-family: var(--font-display);
  font-size: 14px;
  font-weight: 400;
  text-transform: uppercase;
  letter-spacing: 1px;
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

/* 全屏覆盖层 - always dark for code viewer */
.cv-overlay { position: fixed; inset: 0; background: rgba(0,0,0,0.85); z-index: 9999; overflow: hidden; }
.cv-panel { position: absolute; inset: 0; background: #0d1117; display: flex; flex-direction: column; }
.cv-topbar { height: 40px; flex-shrink: 0; display: flex; align-items: center; gap: 8px; padding: 0 12px; background: #161b22; }
.cv-add { color: #3fb950; font-size: 13px; font-weight: 700; }
.cv-del { color: #f85149; font-size: 13px; font-weight: 700; }
.cv-close { margin-left: auto; background: none; border: none; color: #8b949e; font-size: 15px; cursor: pointer; padding: 4px 8px; }
.cv-close:hover { color: #e6edf3; }
.cv-diff { flex: 1; overflow: auto; min-height: 0; display: flex; flex-direction: column; }
.cv-diff .d2h-wrapper,
.cv-diff .d2h-file-wrapper { flex: 1; display: flex; flex-direction: column; min-height: 0; margin: 0 !important; border: none; border-radius: 0; }
.cv-diff .d2h-files-diff { flex: 1; overflow: hidden; display: flex; min-height: 0; }
.cv-diff .d2h-file-side-diff { height: 100%; overflow: auto !important; }
.cv-diff .d2h-code-side-linenumber { position: sticky !important; left: 0; z-index: 1; }

html.cv-lock,
html.cv-lock body { overflow: hidden !important; height: 100% !important; }
</style>

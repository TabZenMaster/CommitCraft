<template>
  <div>
    <div class="page-header">
      <div>
        <el-button @click="router.push('/review')">← 返回</el-button>
        <span style="margin-left:12px">审核任务 #{{ taskId }} 结果</span>
      </div>
      <span v-if="taskInfo" style="color:#999;font-size:13px">
        {{ taskInfo.commitSha?.slice(0, 8) }} | {{ taskInfo.commitMessage }} | {{ taskInfo.committer }}
      </span>
    </div>

    <el-table v-if="results.length" :data="results" stripe>
      <el-table-column type="index" width="50" />
      <el-table-column prop="filePath" label="文件" width="200" show-overflow-tooltip />
      <el-table-column label="行号" width="90">
        <template #default="{ row }">{{ row.lineStart }}~{{ row.lineEnd }}</template>
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
      <el-table-column prop="description" label="问题描述" />
      <el-table-column prop="suggestion" label="修复建议" />
      <el-table-column label="代码" width="90">
        <template #default="{ row }">
          <el-button v-if="row.diffContent" size="small" @click="showCode(row)">查看代码</el-button>
          <span v-else style="color:#999;font-size:12px">-</span>
        </template>
      </el-table-column>
      <el-table-column prop="status" label="状态" width="90">
        <template #default="{ row }">
          <el-tag size="small" :type="statusTag(row.status)">{{ statusName(row.status) }}</el-tag>
        </template>
      </el-table-column>
    </el-table>

    <el-empty v-else description="暂无审核结果，问题可能已被修复或 AI 未发现问题" />

    <!-- 代码详情弹窗 -->
    <el-dialog v-model="codeDialogVisible" :title="currentCode?.filePath" width="800px" destroy-on-close>
      <pre><code v-html="highlightedCode"></code></pre>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { reviewApi } from '@/api'
import hljs from 'highlight.js'
import 'highlight.js/styles/github-dark.min.css'

const route = useRoute()
const router = useRouter()
const taskId = Number(route.params.id)
const taskInfo = ref<any>(null)
const results = ref<any[]>([])
const codeDialogVisible = ref(false)
const currentCode = ref<any>(null)

const highlightedCode = computed(() => {
  if (!currentCode.value?.diffContent) return ''
  const code = currentCode.value.diffContent
  // 从文件路径推断语言
  const ext = currentCode.value.filePath?.split('.').pop() ?? ''
  const langMap: Record<string, string> = {
    cs: 'csharp', js: 'javascript', ts: 'typescript', vue: 'xml',
    py: 'python', java: 'java', go: 'go', rs: 'rust', sql: 'sql',
    json: 'json', xml: 'xml', html: 'xml', css: 'css', sh: 'bash',
    yml: 'yaml', yaml: 'yaml', md: 'markdown', txt: 'plaintext'
  }
  const lang = langMap[ext] || 'plaintext'
  try { return hljs.highlight(code, { language: lang }).value }
  catch { return hljs.highlightAuto(code).value }
})

function showCode(row: any) {
  currentCode.value = row
  codeDialogVisible.value = true
}

const sevTag = (s: string) => ({ critical: 'danger', major: 'warning', minor: 'info', suggestion: 'info' }[s] || 'info')
const sevName = (s: string) => ({ critical: '致命', major: '严重', minor: '警告', suggestion: '建议' }[s] || s)
const typeTag = (s: string) => ({ security: 'danger', correctness: 'warning', performance: 'info', maintainability: 'info', best_practice: 'info', code_style: 'info', other: 'info' }[s] || 'info')
const typeName = (s: string) => ({ security: '安全', correctness: '正确性', performance: '性能', maintainability: '可维护性', best_practice: '最佳实践', code_style: '代码风格', other: '其他' }[s] || s)
const statusTag = (s: number) => ['', 'warning', 'success', 'info'][s] || 'info'
const statusName = (s: number) => ['待处理', '已认领', '已修复', '已忽略'][s] || '-'

onMounted(async () => {
  const [t, r] = await Promise.all([
    reviewApi.task(taskId),
    reviewApi.results(taskId)
  ])
  if (t.success) taskInfo.value = t.data
  if (r.success) {
    const order = { critical: 0, major: 1, minor: 2, suggestion: 3 }
    results.value = r.data.sort((a: any, b: any) => (order[a.severity] ?? 99) - (order[b.severity] ?? 99))
  }
})
</script>

<style scoped>
.page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
</style>

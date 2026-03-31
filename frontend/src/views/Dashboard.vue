<template>
  <div>
    <h3>📊 数据概览</h3>
    <el-row :gutter="16" style="margin-bottom:20px">
      <el-col :span="6">
        <el-card>
          <div class="stat-num" style="color:#409eff">{{ stats.total || 0 }}</div>
          <div class="stat-label">问题总数</div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card>
          <div class="stat-num" style="color:#f56c6c">{{ stats.bySeverity?.critical || 0 }}</div>
          <div class="stat-label">致命问题</div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card>
          <div class="stat-num" style="color:#e6a23c">{{ stats.pending || 0 }}</div>
          <div class="stat-label">待处理</div>
        </el-card>
      </el-col>
      <el-col :span="6">
        <el-card>
          <div class="stat-num" style="color:#67c23a">{{ stats.fixedCount || 0 }}</div>
          <div class="stat-label">已修复</div>
        </el-card>
      </el-col>
    </el-row>

    <el-row :gutter="16">
      <el-col :span="12">
        <el-card title="按问题类型分布">
          <template #header>按问题类型分布</template>
          <div v-if="Object.keys(stats.byType || {}).length">
            <div v-for="(cnt, type) in stats.byType" :key="type" class="type-row">
              <span>{{ type }}</span>
              <el-progress :percentage="Math.round((cnt / stats.total) * 100)" :stroke-width="10" />
              <span class="cnt">{{ cnt }}</span>
            </div>
          </div>
          <el-empty v-else description="暂无数据" :image-size="60" />
        </el-card>
      </el-col>
      <el-col :span="12">
        <el-card>
          <template #header>按严重程度分布</template>
          <div class="severity-list">
            <div v-for="(sev, key) in stats.bySeverity" :key="key" class="sev-item">
              <el-tag :type="sevType(key)" size="small">{{ key }}</el-tag>
              <span>{{ sev }} 个</span>
            </div>
          </div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { reviewApi } from '@/api'
const stats = ref<any>({})

const sevType = (key: string) => {
  const map: Record<string, string> = { critical: 'danger', major: 'warning', minor: 'info', suggestion: 'info' }
  return map[key] || 'info'
}

onMounted(async () => {
  const res: any = await reviewApi.statistics()
  if (res.success) stats.value = res.data
})
</script>

<style scoped>
.stat-num { font-size: 36px; font-weight: bold; text-align: center; }
.stat-label { text-align: center; color: #999; font-size: 13px; margin-top: 4px; }
.type-row { display: flex; align-items: center; gap: 8px; margin-bottom: 8px; }
.type-row span { font-size: 13px; min-width: 100px; }
.cnt { font-weight: bold; min-width: 30px; text-align: right; }
.severity-list { display: flex; flex-direction: column; gap: 10px; }
.sev-item { display: flex; align-items: center; gap: 10px; font-size: 14px; }
</style>

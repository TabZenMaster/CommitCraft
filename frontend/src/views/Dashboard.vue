<template>
  <div class="dashboard">
    <el-row :gutter="16" class="kpi-row">
      <el-col :span="6" v-for="(item, i) in kpiList" :key="i">
        <el-card class="kpi-card" shadow="hover">
          <div class="kpi-accent" :style="{ background: item.color }" />
          <div class="kpi-icon">{{ item.icon }}</div>
          <div class="kpi-body">
            <div class="kpi-num" :style="{ color: item.color }">{{ item.value }}</div>
            <div class="kpi-label">{{ item.label }}</div>
          </div>
        </el-card>
      </el-col>
    </el-row>

    <el-row :gutter="16">
      <el-col :span="14">
        <el-card shadow="hover" class="chart-card">
          <template #header><span class="card-title">📊 问题类型分布</span></template>
          <div v-if="Object.keys(stats.byType || {}).length">
            <div v-for="(cnt, type) in stats.byType" :key="type" class="type-row">
              <span class="type-label">{{ typeName(type) }}</span>
              <el-progress
                :percentage="Math.round((cnt / stats.total) * 100)"
                :stroke-width="10"
                :color="typeColor(type)"
              />
              <span class="type-cnt">{{ cnt }}</span>
            </div>
          </div>
          <el-empty v-else description="暂无数据" :image-size="60" />
        </el-card>
      </el-col>

      <el-col :span="10">
        <el-card shadow="hover" class="chart-card">
          <template #header><span class="card-title">🔢 严重程度分布</span></template>
          <div class="sev-grid">
            <div v-for="(sev, key) in stats.bySeverity" :key="key" class="sev-item">
              <div class="sev-dot" :style="{ background: sevColor(key) }" />
              <span class="sev-label">{{ sevName(key) }}</span>
              <span class="sev-num" :style="{ color: sevColor(key) }">{{ sev }}</span>
            </div>
          </div>
          <el-divider v-if="Object.keys(stats.bySeverity || {}).length" />
          <div class="sev-summary">
            <span class="summary-item">
              <span class="dot critical" />致命 {{ stats.bySeverity?.critical || 0 }}
            </span>
            <span class="summary-item">
              <span class="dot major" />严重 {{ stats.bySeverity?.major || 0 }}
            </span>
            <span class="summary-item">
              <span class="dot minor" />警告 {{ stats.bySeverity?.minor || 0 }}
            </span>
            <span class="summary-item">
              <span class="dot suggestion" />建议 {{ stats.bySeverity?.suggestion || 0 }}
            </span>
          </div>
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { reviewApi } from '@/api'

const stats = ref<any>({})

const sevName = (s: string) => ({ critical: '致命', major: '严重', minor: '警告', suggestion: '建议' }[s] || s)
const typeName = (s: string) => ({
  security: '安全', correctness: '正确性', performance: '性能',
  maintainability: '可维护性', best_practice: '最佳实践', code_style: '代码风格', other: '其他'
}[s] || s)
const sevColor = (s: string) => ({ critical: '#f56c6c', major: '#e6a23c', minor: '#909399', suggestion: '#67c23a' }[s] || '#999')
const typeColor = (s: string) => ({
  security: '#f56c6c', correctness: '#e6a23c', performance: '#409eff',
  maintainability: '#909399', best_practice: '#53d1a6', code_style: '#b37feb', other: '#bbb'
}[s] || '#999')

const kpiList = computed(() => [
  { icon: '📋', label: '问题总数', value: stats.value.total || 0, color: '#409eff' },
  { icon: '💥', label: '致命问题', value: stats.value.bySeverity?.critical || 0, color: '#f56c6c' },
  { icon: '⏳', label: '待处理', value: stats.value.pending || 0, color: '#e6a23c' },
  { icon: '✅', label: '已修复', value: stats.value.fixedCount || 0, color: '#67c23a' },
])

onMounted(async () => {
  const res: any = await reviewApi.statistics()
  if (res.success) stats.value = res.data
})
</script>

<style scoped>
.dashboard {}

/* KPI 卡片 */
.kpi-row { margin-bottom: 16px; }
.kpi-card {
  position: relative;
  overflow: hidden;
  border: none;
  border-radius: 12px;
  cursor: default;
  transition: transform 0.2s, box-shadow 0.2s;
}
.kpi-card:hover { transform: translateY(-2px); box-shadow: 0 8px 24px rgba(0,0,0,0.1); }
.kpi-accent {
  position: absolute;
  top: 0; left: 0;
  width: 4px; height: 100%;
  border-radius: 4px 0 0 4px;
}
.kpi-icon { font-size: 28px; margin-bottom: 8px; }
.kpi-num { font-size: 32px; font-weight: 800; line-height: 1; }
.kpi-label { font-size: 13px; color: #909399; margin-top: 6px; }

/* 图表卡片 */
.chart-card { border-radius: 12px; border: none; }
.card-title { font-size: 14px; font-weight: 600; color: #1e2a38; }

.type-row { display: flex; align-items: center; gap: 12px; margin-bottom: 14px; }
.type-label { font-size: 13px; color: #606266; min-width: 80px; }
.type-cnt { font-size: 13px; font-weight: 700; color: #303133; min-width: 28px; text-align: right; }

.sev-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
.sev-item { display: flex; align-items: center; gap: 8px; }
.sev-dot { width: 10px; height: 10px; border-radius: 50%; flex-shrink: 0; }
.sev-label { font-size: 13px; color: #606266; flex: 1; }
.sev-num { font-size: 18px; font-weight: 700; }

.sev-summary { display: flex; flex-wrap: wrap; gap: 10px 16px; padding-top: 4px; }
.summary-item { display: flex; align-items: center; gap: 6px; font-size: 13px; color: #606266; }
.dot { width: 8px; height: 8px; border-radius: 50%; }
.dot.critical { background: #f56c6c; }
.dot.major { background: #e6a23c; }
.dot.minor { background: #909399; }
.dot.suggestion { background: #67c23a; }
</style>

<template>
  <div class="dashboard">
    <div class="page-header">
      <el-select v-model="filterRepo" clearable placeholder="全部仓库" style="width:180px" @change="loadAll">
        <el-option v-for="r in repos" :key="r.id" :label="r.repoName" :value="r.id" />
      </el-select>
    </div>

    <!-- KPI -->
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

    <el-row :gutter="16" class="chart-row">
      <!-- 7天趋势 -->
      <el-col :span="12">
        <el-card shadow="hover" class="chart-card">
          <template #header><span class="card-title">📈 7天问题趋势</span></template>
          <div v-if="trend.dates?.length" ref="trendChart" class="echart" />
          <el-empty v-else description="暂无数据" :image-size="60" />
        </el-card>
      </el-col>
      <!-- 状态占比 -->
      <el-col :span="12">
        <el-card shadow="hover" class="chart-card">
          <template #header><span class="card-title">🍩 问题状态分布</span></template>
          <div v-if="stats.total > 0" ref="statusChart" class="echart" />
          <el-empty v-else description="暂无数据" :image-size="60" />
        </el-card>
      </el-col>
    </el-row>

    <el-row :gutter="16" class="chart-row">
      <!-- 仓库排名 -->
      <el-col :span="10">
        <el-card shadow="hover" class="chart-card">
          <template #header><span class="card-title">🏆 仓库问题排名</span></template>
          <div v-if="repoRanking.length" ref="rankingChart" class="echart" />
          <el-empty v-else description="暂无数据" :image-size="60" />
        </el-card>
      </el-col>
      <!-- 最近审核 -->
      <el-col :span="14">
        <el-card shadow="hover" class="chart-card">
          <template #header><span class="card-title">🕐 最近审核</span></template>
          <el-table :data="recentTasks" size="small" stripe :max-height="260">
            <el-table-column prop="repoName" label="仓库" min-width="120" show-overflow-tooltip />
            <el-table-column prop="branchName" label="分支" min-width="100" show-overflow-tooltip />
            <el-table-column prop="commitSha" label="Commit" width="90">
              <template #default="{ row }"><code style="color:#409eff;font-size:12px">{{ row.commitSha }}</code></template>
            </el-table-column>
            <el-table-column prop="issueCount" label="问题数" width="70" align="center" />
            <el-table-column prop="createTime" label="时间" width="100" />
            <el-table-column prop="status" label="状态" width="80" align="center">
              <template #default="{ row }">
                <el-tag :type="taskStatusType(row.status)" size="small">{{ taskStatusName(row.status) }}</el-tag>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </el-col>
    </el-row>

    <!-- 处理效率 -->
    <el-row :gutter="16" class="chart-row">
      <el-col :span="24">
        <el-card shadow="hover" class="chart-card">
          <template #header>
            <span class="card-title">⏱ 处理效率</span>
          </template>
          <div v-if="handling.avgHours > 0 || handling.count > 0" class="eff-content">
            <div class="eff-kpi">
              <div class="eff-big">{{ handling.avgHours }}<span class="eff-unit">小时</span></div>
              <div class="eff-sub">平均修复耗时（共 {{ handling.count }} 条记录）</div>
            </div>
            <div class="eff-bars">
              <div v-for="item in handling.details" :key="item.time" class="eff-bar-item">
                <div class="eff-bar-label">{{ item.time }}</div>
                <div class="eff-bar-track">
                  <div class="eff-bar-fill" :style="{ width: Math.min(100, item.hours / handling.avgHours * 100) + '%', background: barColor(item.hours) }" />
                </div>
                <div class="eff-bar-value">{{ item.hours }}h</div>
              </div>
            </div>
          </div>
          <el-empty v-else description="暂无已修复记录" :image-size="60" />
        </el-card>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, nextTick } from 'vue'
import { reviewApi, repositoryApi } from '@/api'
import * as echarts from 'echarts'

const filterRepo = ref<number | undefined>(undefined)
const stats = ref<any>({})
const repos = ref<any[]>([])
const trend = ref<any>({ dates: [], counts: [] })
const repoRanking = ref<any[]>([])
const recentTasks = ref<any[]>([])
const handling = ref<any>({ avgHours: 0, count: 0, details: [] })

const trendChart = ref<HTMLDivElement>()
const statusChart = ref<HTMLDivElement>()
const rankingChart = ref<HTMLDivElement>()
let trendChartIns: echarts.ECharts | null = null
let statusChartIns: echarts.ECharts | null = null
let rankingChartIns: echarts.ECharts | null = null

const kpiList = computed(() => [
  { icon: '📋', label: '问题总数', value: stats.value.total || 0, color: '#409eff' },
  { icon: '💥', label: '致命问题', value: stats.value.bySeverity?.critical || 0, color: '#f56c6c' },
  { icon: '⏳', label: '待处理', value: stats.value.pending || 0, color: '#e6a23c' },
  { icon: '✅', label: '已修复', value: stats.value.fixedCount || 0, color: '#67c23a' },
])

async function loadAll() {
  const [st, tr, rr, rt, hs] = await Promise.all([
    reviewApi.statistics(filterRepo.value),
    reviewApi.trend(filterRepo.value),
    reviewApi.repoRanking(),
    reviewApi.recentTasks(10),
    reviewApi.handlingStats(),
  ])
  if (st.success) stats.value = st.data || {}
  if (tr.success) trend.value = tr.data || { dates: [], counts: [] }
  if (rr.success) repoRanking.value = rr.data || []
  if (rt.success) recentTasks.value = rt.data || []
  if (hs.success) handling.value = hs.data || { avgHours: 0, count: 0, details: [] }
  await nextTick()
  renderCharts()
}

function renderCharts() {
  renderTrend()
  renderStatus()
  renderRanking()
}

function renderTrend() {
  if (!trendChart.value || !trend.value.dates?.length) return
  if (trendChartIns) trendChartIns.dispose()
  trendChartIns = echarts.init(trendChart.value)
  trendChartIns.setOption({
    tooltip: { trigger: 'axis' },
    grid: { left: 40, right: 20, top: 10, bottom: 25 },
    xAxis: { type: 'category', data: trend.value.dates, axisLine: { lineStyle: { color: '#e4e7ed' } }, axisLabel: { fontSize: 11, color: '#606266' } },
    yAxis: { type: 'value', splitLine: { lineStyle: { color: '#f0f0f0' } }, axisLabel: { fontSize: 11 } },
    series: [{
      data: trend.value.counts,
      type: 'line',
      smooth: true,
      areaStyle: { color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [{ offset: 0, color: 'rgba(64,158,255,0.3)' }, { offset: 1, color: 'rgba(64,158,255,0.02)' }]) },
      lineStyle: { color: '#409eff', width: 2 },
      itemStyle: { color: '#409eff' },
      markPoint: { data: [{ type: 'max', label: { fontSize: 10 } }] }
    }]
  })
}

function renderStatus() {
  if (!statusChart.value || !stats.value.total) return
  if (statusChartIns) statusChartIns.dispose()
  const data = [
    { name: '待处理', value: stats.value.pending || 0, color: '#e6a23c' },
    { name: '已认领', value: stats.value.claimed || 0, color: '#409eff' },
    { name: '已修复', value: stats.value.fixedCount || 0, color: '#67c23a' },
    { name: '已忽略', value: stats.value.ignored || 0, color: '#909399' },
  ].filter(d => d.value > 0)
  statusChartIns = echarts.init(statusChart.value)
  statusChartIns.setOption({
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
    legend: { orient: 'vertical', right: 10, top: 'center', textStyle: { fontSize: 12 } },
    series: [{
      type: 'pie',
      radius: ['40%', '70%'],
      center: ['35%', '50%'],
      label: { show: false },
      emphasis: { label: { show: true, fontWeight: 'bold' } },
      data: data.map(d => ({ ...d, itemStyle: { color: d.color } }))
    }]
  })
}

function renderRanking() {
  if (!rankingChart.value || !repoRanking.value.length) return
  if (rankingChartIns) rankingChartIns.dispose()
  rankingChartIns = echarts.init(rankingChart.value)
  const data = repoRanking.value.map((r: any) => ({ name: r.name, value: r.count }))
  rankingChartIns.setOption({
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
    grid: { left: 100, right: 40, top: 10, bottom: 10 },
    xAxis: { type: 'value', splitLine: { lineStyle: { color: '#f0f0f0' } }, axisLabel: { fontSize: 11 } },
    yAxis: { type: 'category', data: data.map(d => d.name).reverse(), axisLabel: { fontSize: 11, color: '#606266' }, axisLine: { lineStyle: { color: '#e4e7ed' } } },
    series: [{
      type: 'bar',
      data: data.map(d => d.value).reverse(),
      barWidth: 16,
      itemStyle: {
        color: new echarts.graphic.LinearGradient(0, 0, 1, 0, [{ offset: 0, color: '#409eff' }, { offset: 1, color: '#53d1a6' }]),
        borderRadius: [0, 4, 4, 0]
      },
      label: { show: true, position: 'right', fontSize: 11, color: '#606266' }
    }]
  })
}

function taskStatusName(s: number) {
  return ['待审核', '审核中', '已完成', '失败'][s] ?? '未知'
}
function taskStatusType(s: number) {
  return ['info', 'warning', 'success', 'danger'][s] ?? 'info'
}

function barColor(h: number) {
  if (h < 1) return '#67c23a'
  if (h < 4) return '#409eff'
  if (h < 24) return '#e6a23c'
  return '#f56c6c'
}

onMounted(async () => {
  const r = await repositoryApi.list()
  if (r.success) repos.value = r.data || []
  loadAll()
  window.addEventListener('resize', () => {
    trendChartIns?.resize()
    statusChartIns?.resize()
    rankingChartIns?.resize()
  })
})
</script>

<style scoped>
.dashboard {}
.page-header { display: flex; align-items: center; gap: 12px; margin-bottom: 16px; }

.kpi-row { margin-bottom: 16px; }
.kpi-card {
  position: relative; overflow: hidden; border-radius: 12px; border: none;
  cursor: default; transition: transform 0.2s, box-shadow 0.2s;
}
.kpi-card:hover { transform: translateY(-2px); box-shadow: 0 8px 24px rgba(0,0,0,0.1); }
.kpi-accent { position: absolute; top: 0; left: 0; width: 4px; height: 100%; border-radius: 4px 0 0 4px; }
.kpi-icon { font-size: 28px; margin-bottom: 8px; }
.kpi-num { font-size: 32px; font-weight: 800; line-height: 1; }
.kpi-label { font-size: 13px; color: #909399; margin-top: 6px; }

.chart-row { margin-bottom: 16px; }
.chart-card { border-radius: 12px; border: none; }
.card-title { font-size: 14px; font-weight: 600; color: #1e2a38; }

.echart { height: 220px; width: 100%; }

/* 处理效率 */
.eff-content { display: flex; gap: 24px; align-items: flex-start; }
.eff-kpi { min-width: 140px; text-align: center; padding: 16px 0; }
.eff-big { font-size: 48px; font-weight: 800; color: #409eff; line-height: 1; }
.eff-unit { font-size: 16px; font-weight: 400; margin-left: 4px; color: #909399; }
.eff-sub { font-size: 12px; color: #909399; margin-top: 8px; }
.eff-bars { flex: 1; display: flex; flex-direction: column; gap: 8px; padding: 12px 0; }
.eff-bar-item { display: flex; align-items: center; gap: 10px; }
.eff-bar-label { font-size: 11px; color: #909399; min-width: 80px; }
.eff-bar-track { flex: 1; height: 12px; background: #f0f2f5; border-radius: 6px; overflow: hidden; }
.eff-bar-fill { height: 100%; border-radius: 6px; transition: width 0.4s; min-width: 2px; }
.eff-bar-value { font-size: 11px; font-weight: 700; color: #303133; min-width: 36px; text-align: right; }
</style>

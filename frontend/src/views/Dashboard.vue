<template>
  <div class="dashboard">

    <!-- 页面标题区 -->
    <div class="dash-header">
      <div class="dash-title">
        <span class="dash-icon">📊</span>
        <div>
          <div class="dash-h1">数据概览</div>
          <div class="dash-sub">实时监控代码审核数据</div>
        </div>
      </div>
      <div class="dash-controls">
        <el-select v-model="filterRepo" clearable placeholder="全部仓库" size="large" style="width:200px" @change="loadAll">
          <el-option v-for="r in repos" :key="r.id" :label="r.repoName" :value="r.id" />
        </el-select>
        <el-button type="primary" size="large" @click="loadAll" :loading="loading" class="refresh-btn">
          <span v-if="!loading">🔄 刷新</span>
        </el-button>
        <span class="update-time" v-if="lastUpdate">更新于 {{ lastUpdate }}</span>
      </div>
    </div>

    <!-- 仓库总览 -->
    <div class="section-label"><span>仓库总览</span></div>
    <div class="overview-row">
      <div v-for="(item, i) in overviewKpi" :key="i" class="overview-card">
        <div class="ov-icon" :style="{ background: ovColors[i] + '18', color: ovColors[i] }">{{ item.icon }}</div>
        <div class="ov-body">
          <div class="ov-num">{{ item.value }}</div>
          <div class="ov-label">{{ item.label }}</div>
        </div>
      </div>
    </div>

    <!-- 问题概览 -->
    <div class="section-label"><span>问题概览</span></div>
    <el-row :gutter="20" class="kpi-row">
      <el-col :span="6" v-for="(item, i) in kpiList" :key="i">
        <div class="kpi-card" :style="{ '--accent': item.color }">
          <div class="kpi-glow" :style="{ background: item.color }" />
          <div class="kpi-inner">
            <div class="kpi-icon-wrap" :style="{ background: item.color + '20', color: item.color }">{{ item.icon }}</div>
            <div class="kpi-content">
              <div class="kpi-num" :style="{ color: item.color }">{{ item.value }}</div>
              <div class="kpi-label">{{ item.label }}</div>
            </div>
          </div>
        </div>
      </el-col>
    </el-row>

    <!-- 图表区第一行 -->
    <el-row :gutter="20" class="chart-row">
      <!-- 7天趋势 -->
      <el-col :span="14">
        <div class="chart-card">
          <div class="chart-header">
            <span class="chart-title">📈 7天问题趋势</span>
            <div class="chart-legend">
              <span class="leg"><span class="leg-dot" style="background:#409eff"></span>全部</span>
              <span class="leg"><span class="leg-dot" style="background:#f56c6c"></span>致命</span>
              <span class="leg"><span class="leg-dot" style="background:#e6a23c"></span>严重</span>
            </div>
          </div>
          <div v-if="trend.dates?.length" ref="trendChart" class="echart" />
          <el-empty v-else description="暂无数据" :image-size="60" />
        </div>
      </el-col>
      <!-- 问题类型分布 -->
      <el-col :span="10">
        <div class="chart-card">
          <div class="chart-header">
            <span class="chart-title">🔍 问题类型分布</span>
          </div>
          <div v-if="Object.keys(stats.byType || {}).length" ref="typeChart" class="echart" />
          <el-empty v-else description="暂无数据" :image-size="60" />
        </div>
      </el-col>
    </el-row>

    <!-- 图表区第二行 -->
    <el-row :gutter="20" class="chart-row">
      <!-- 状态占比 -->
      <el-col :span="8">
        <div class="chart-card">
          <div class="chart-header">
            <span class="chart-title">🍩 问题状态分布</span>
          </div>
          <div v-if="stats.total > 0" ref="statusChart" class="echart" />
          <el-empty v-else description="暂无数据" :image-size="60" />
        </div>
      </el-col>
      <!-- 仓库排名 -->
      <el-col :span="8">
        <div class="chart-card">
          <div class="chart-header">
            <span class="chart-title">🏆 仓库问题排名</span>
          </div>
          <div v-if="repoRanking.length" ref="rankingChart" class="echart" />
          <el-empty v-else description="暂无数据" :image-size="60" />
        </div>
      </el-col>
      <!-- 最近审核 -->
      <el-col :span="8">
        <div class="chart-card">
          <div class="chart-header">
            <span class="chart-title">🕐 最近审核</span>
          </div>
          <el-table :data="recentTasks" size="small" :max-height="180">
            <el-table-column prop="repoName" label="仓库" min-width="100" show-overflow-tooltip />
            <el-table-column prop="commitSha" label="Commit" width="76">
              <template #default="{ row }"><code style="color:#409eff;font-size:11px">{{ row.commitSha }}</code></template>
            </el-table-column>
            <el-table-column prop="issueCount" label="问题" width="46" align="center" />
            <el-table-column prop="createTime" label="时间" width="76" />
          </el-table>
        </div>
      </el-col>
    </el-row>

    <!-- 处理效率 -->
    <el-row :gutter="20" class="chart-row">
      <el-col :span="24">
        <div class="chart-card eff-card">
          <div class="chart-header">
            <span class="chart-title">⏱ 处理效率</span>
          </div>
          <div v-if="handling.avgHours > 0 || handling.count > 0" class="eff-layout">
            <div class="eff-left">
              <div class="eff-big">{{ handling.avgHours }}</div>
              <div class="eff-unit">小时</div>
              <div class="eff-sub">平均修复耗时 <span class="eff-count">{{ handling.count }} 条记录</span></div>
            </div>
            <div class="eff-right">
              <div v-for="item in handling.details" :key="item.time" class="eff-row">
                <div class="eff-time">{{ item.time }}</div>
                <div class="eff-track">
                  <div class="eff-fill" :style="{ width: Math.min(100, item.hours / handling.avgHours * 100) + '%', background: barColor(item.hours) }" />
                </div>
                <div class="eff-val">{{ item.hours }}h</div>
              </div>
            </div>
          </div>
          <el-empty v-else description="暂无已修复记录" :image-size="60" />
        </div>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'
import { reviewApi, repositoryApi } from '@/api'
import * as echarts from 'echarts'

const loading = ref(false)
const lastUpdate = ref('')
let autoTimer: ReturnType<typeof setInterval> | null = null

const filterRepo = ref<number | undefined>(undefined)
const stats = ref<any>({})
const repos = ref<any[]>([])
const trend = ref<any>({ dates: [], counts: [], criticalCounts: [], majorCounts: [] })
const repoRanking = ref<any[]>([])
const recentTasks = ref<any[]>([])
const handling = ref<any>({ avgHours: 0, count: 0, details: [] })
const overview = ref<any>({})

const trendChart = ref<HTMLDivElement>()
const statusChart = ref<HTMLDivElement>()
const rankingChart = ref<HTMLDivElement>()
const typeChart = ref<HTMLDivElement>()
let trendChartIns: echarts.ECharts | null = null
let statusChartIns: echarts.ECharts | null = null
let rankingChartIns: echarts.ECharts | null = null
let typeChartIns: echarts.ECharts | null = null

const ovColors = ['#409eff', '#53d1a6', '#67c23a', '#e6a23c', '#f56c6c']

const overviewKpi = computed(() => [
  { icon: '📦', label: '仓库数', value: overview.value.totalRepos || 0 },
  { icon: '📋', label: '审核任务', value: overview.value.totalTasks || 0 },
  { icon: '✅', label: '已完成', value: overview.value.totalReviews || 0 },
  { icon: '🐛', label: '发现问题', value: overview.value.totalIssues || 0 },
  { icon: '🔥', label: '今日新增', value: overview.value.todayIssues || 0 },
])

const kpiList = computed(() => [
  { icon: '📋', label: '问题总数', value: stats.value.total || 0, color: '#409eff' },
  { icon: '💥', label: '致命问题', value: stats.value.bySeverity?.critical || 0, color: '#f56c6c' },
  { icon: '⚠️', label: '严重问题', value: stats.value.bySeverity?.major || 0, color: '#e6a23c' },
  { icon: '✅', label: '已修复', value: stats.value.fixedCount || 0, color: '#67c23a' },
])

function formatTime() {
  const now = new Date()
  lastUpdate.value = now.toTimeString().slice(0, 5)
}

async function loadAll() {
  loading.value = true
  const [st, tr, rr, rt, hs, ro] = await Promise.all([
    reviewApi.statistics(filterRepo.value),
    reviewApi.trend(filterRepo.value),
    reviewApi.repoRanking(),
    reviewApi.recentTasks(10),
    reviewApi.handlingStats(),
    reviewApi.repoOverview(),
  ])
  if (st.success) stats.value = st.data || {}
  if (tr.success) trend.value = tr.data || { dates: [], counts: [], criticalCounts: [], majorCounts: [] }
  if (rr.success) repoRanking.value = rr.data || []
  if (rt.success) recentTasks.value = rt.data || []
  if (hs.success) handling.value = hs.data || { avgHours: 0, count: 0, details: [] }
  if (ro.success) overview.value = ro.data || {}
  formatTime()
  loading.value = false
  await nextTick()
  renderCharts()
}

function renderCharts() {
  renderTrend()
  renderStatus()
  renderRanking()
  renderType()
}

function renderTrend() {
  if (!trendChart.value || !trend.value.dates?.length) return
  if (trendChartIns) trendChartIns.dispose()
  trendChartIns = echarts.init(trendChart.value)
  trendChartIns.setOption({
    tooltip: { trigger: 'axis', backgroundColor: 'rgba(255,255,255,0.96)', borderColor: '#e4e7ed', textStyle: { color: '#303133', fontSize: 12 }, extraCssText: 'box-shadow: 0 4px 16px rgba(0,0,0,0.08)' },
    legend: { show: false },
    grid: { left: 44, right: 20, top: 12, bottom: 24 },
    xAxis: { type: 'category', data: trend.value.dates, axisLine: { lineStyle: { color: '#e4e7ed' } }, axisLabel: { fontSize: 11, color: '#909399' }, splitLine: { show: false } },
    yAxis: { type: 'value', splitLine: { lineStyle: { color: '#f0f0f0', type: 'dashed' } }, axisLabel: { fontSize: 11, color: '#909399' }, axisLine: { show: false } },
    series: [
      { name: '全部', data: trend.value.counts, type: 'line', smooth: 0.4, lineStyle: { color: '#409eff', width: 3 }, itemStyle: { color: '#409eff' }, areaStyle: { color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [{ offset: 0, color: 'rgba(64,158,255,0.18)' }, { offset: 1, color: 'rgba(64,158,255,0.01)' }]) }, symbol: 'circle', symbolSize: 5 },
      { name: '致命', data: trend.value.criticalCounts, type: 'line', smooth: 0.4, lineStyle: { color: '#f56c6c', width: 2.5 }, itemStyle: { color: '#f56c6c' }, symbol: 'circle', symbolSize: 4 },
      { name: '严重', data: trend.value.majorCounts, type: 'line', smooth: 0.4, lineStyle: { color: '#e6a23c', width: 2.5 }, itemStyle: { color: '#e6a23c' }, symbol: 'circle', symbolSize: 4 }
    ]
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
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)', backgroundColor: 'rgba(255,255,255,0.96)', borderColor: '#e4e7ed', textStyle: { color: '#303133', fontSize: 12 }, extraCssText: 'box-shadow: 0 4px 16px rgba(0,0,0,0.08)' },
    legend: { orient: 'vertical', right: 8, top: 'center', textStyle: { fontSize: 11, color: '#606266' }, itemGap: 10 },
    series: [{ type: 'pie', radius: ['42%', '68%'], center: ['34%', '50%'], label: { show: false }, emphasis: { scale: true, scaleSize: 6, label: { show: true, fontSize: 12, fontWeight: 600 } }, data: data.map(d => ({ ...d, itemStyle: { color: d.color, borderRadius: 6, borderWidth: 2, borderColor: '#fff' } })) }]
  })
}

function renderRanking() {
  if (!rankingChart.value || !repoRanking.value.length) return
  if (rankingChartIns) rankingChartIns.dispose()
  rankingChartIns = echarts.init(rankingChart.value)
  const data = repoRanking.value.map((r: any) => ({ name: r.name, value: r.count }))
  rankingChartIns.setOption({
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' }, backgroundColor: 'rgba(255,255,255,0.96)', borderColor: '#e4e7ed', textStyle: { color: '#303133', fontSize: 12 }, extraCssText: 'box-shadow: 0 4px 16px rgba(0,0,0,0.08)' },
    grid: { left: 90, right: 32, top: 8, bottom: 8 },
    xAxis: { type: 'value', splitLine: { lineStyle: { color: '#f0f0f0', type: 'dashed' } }, axisLabel: { fontSize: 10, color: '#909399' }, axisLine: { show: false } },
    yAxis: { type: 'category', data: data.map(d => d.name).reverse(), axisLabel: { fontSize: 11, color: '#606266' }, axisLine: { lineStyle: { color: '#e4e7ed' } }, axisTick: { show: false } },
    series: [{ type: 'bar', data: data.map(d => d.value).reverse(), barWidth: 14, itemStyle: { color: new echarts.graphic.LinearGradient(0, 0, 1, 0, [{ offset: 0, color: '#409eff' }, { offset: 1, color: '#53d1a6' }]), borderRadius: [0, 6, 6, 0] }, label: { show: true, position: 'right', fontSize: 11, color: '#909399', formatter: '{c}' } }]
  })
}

const typeNames: Record<string, string> = {
  security: '安全', correctness: '正确性', performance: '性能',
  maintainability: '可维护性', best_practice: '最佳实践', code_style: '代码风格', other: '其他'
}
const typeColors: Record<string, string> = {
  security: '#f56c6c', correctness: '#e6a23c', performance: '#409eff',
  maintainability: '#909399', best_practice: '#53d1a6', code_style: '#b37feb', other: '#bbb'
}

function renderType() {
  if (!typeChart.value || !stats.value.byType) return
  if (typeChartIns) typeChartIns.dispose()
  const entries = Object.entries(stats.value.byType as Record<string, number>)
  if (!entries.length) return
  entries.sort((a, b) => b[1] - a[1])
  typeChartIns = echarts.init(typeChart.value)
  typeChartIns.setOption({
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)', backgroundColor: 'rgba(255,255,255,0.96)', borderColor: '#e4e7ed', textStyle: { color: '#303133', fontSize: 12 }, extraCssText: 'box-shadow: 0 4px 16px rgba(0,0,0,0.08)' },
    series: [{ type: 'pie', radius: ['38%', '62%'], center: ['50%', '50%'], label: { formatter: '{b}\n{d}%', fontSize: 11, color: '#606266' }, data: entries.map(([k, v]) => ({ name: typeNames[k] || k, value: v, itemStyle: { color: typeColors[k] || '#999', borderRadius: 6, borderWidth: 2, borderColor: '#fff' } })) }]
  })
}

function barColor(h: number) {
  if (h < 1) return '#67c23a'
  if (h < 4) return '#409eff'
  if (h < 24) return '#e6a23c'
  return '#f56c6c'
}

onMounted(() => {
  repositoryApi.list().then(res => { if (res.success) repos.value = res.data || [] })
  loadAll()
  autoTimer = setInterval(loadAll, 30000)
  window.addEventListener('resize', () => {
    trendChartIns?.resize(); statusChartIns?.resize(); rankingChartIns?.resize(); typeChartIns?.resize()
  })
})

onUnmounted(() => { if (autoTimer) clearInterval(autoTimer) })
</script>

<style scoped>
.dashboard {
  padding: 16px 20px;
  background: #f5f7fa;
  min-height: 100vh;
}

/* === 头部 === */
.dash-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 14px;
  background: linear-gradient(135deg, #1e2a38 0%, #2d4a6b 100%);
  border-radius: 12px;
  padding: 16px 20px;
  box-shadow: 0 4px 20px rgba(30, 42, 56, 0.16);
}
.dash-title { display: flex; align-items: center; gap: 16px; }
.dash-icon { font-size: 28px; }
.dash-h1 { font-size: 18px; font-weight: 800; color: #fff; letter-spacing: 0.5px; }
.dash-sub { font-size: 12px; color: rgba(255,255,255,0.5); margin-top: 2px; }
.dash-controls { display: flex; align-items: center; gap: 10px; }
.refresh-btn { border-radius: 8px; font-weight: 600; }
.update-time { font-size: 12px; color: rgba(255,255,255,0.45); white-space: nowrap; }

/* === 区块标题 === */
.section-label {
  font-size: 12px;
  font-weight: 700;
  color: #909399;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin-bottom: 8px;
  padding-left: 4px;
}
.section-label span {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.section-label span::before {
  content: '';
  display: inline-block;
  width: 3px;
  height: 14px;
  background: #409eff;
  border-radius: 2px;
}

/* === 仓库总览卡片 === */
.overview-row { margin-bottom: 12px; display: flex; gap: 12px; }
.overview-row .overview-card { flex: 1; min-width: 0; }
.overview-card {
  background: #fff;
  border-radius: 10px;
  padding: 12px 14px;
  display: flex;
  align-items: center;
  gap: 10px;
  box-shadow: 0 1px 6px rgba(0,0,0,0.06);
  transition: transform 0.2s, box-shadow 0.2s;
  border: 1px solid rgba(0,0,0,0.04);
}
.overview-card:hover { transform: translateY(-3px); box-shadow: 0 8px 28px rgba(0,0,0,0.1); }
.ov-icon { font-size: 22px; width: 42px; height: 42px; border-radius: 10px; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
.ov-num { font-size: 22px; font-weight: 800; color: #1e2a38; line-height: 1; }
.ov-label { font-size: 11px; color: #909399; margin-top: 3px; }

/* === KPI 卡片 === */
.kpi-row { margin-bottom: 12px; }
.kpi-card {
  position: relative;
  border-radius: 12px;
  overflow: hidden;
  background: #fff;
  box-shadow: 0 1px 6px rgba(0,0,0,0.06);
  transition: transform 0.2s, box-shadow 0.2s;
  border: 1px solid rgba(0,0,0,0.04);
  cursor: default;
}
.kpi-card:hover { transform: translateY(-2px); box-shadow: 0 6px 20px rgba(0,0,0,0.1); }
.kpi-glow {
  position: absolute;
  top: -24px;
  right: -24px;
  width: 80px;
  height: 80px;
  border-radius: 50%;
  opacity: 0.08;
  filter: blur(16px);
}
.kpi-inner {
  position: relative;
  padding: 14px 16px;
  display: flex;
  align-items: center;
  gap: 12px;
}
.kpi-icon-wrap {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  flex-shrink: 0;
}
.kpi-content { flex: 1; }
.kpi-num { font-size: 24px; font-weight: 900; line-height: 1; }
.kpi-label { font-size: 12px; color: #909399; margin-top: 3px; }

/* === 图表卡片 === */
.chart-row { margin-bottom: 12px; display: flex; }
.chart-row > .el-col { display: flex; flex-direction: column; }
.chart-card {
  background: #fff;
  border-radius: 12px;
  padding: 14px 16px;
  box-shadow: 0 1px 6px rgba(0,0,0,0.06);
  border: 1px solid rgba(0,0,0,0.04);
  flex: 1;
  display: flex;
  flex-direction: column;
  min-height: 0;
}
.chart-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 10px;
}
.chart-title { font-size: 13px; font-weight: 700; color: #1e2a38; }
.chart-legend { display: flex; gap: 10px; }
.leg { display: flex; align-items: center; gap: 4px; font-size: 11px; color: #909399; }
.leg-dot { width: 7px; height: 7px; border-radius: 50%; flex-shrink: 0; }
.echart { flex: 1; width: 100%; min-height: 180px; }

/* === 处理效率 === */
.eff-card { }
.eff-layout { display: flex; gap: 24px; align-items: flex-start; }
.eff-left { min-width: 110px; text-align: center; padding: 8px 0; }
.eff-big { font-size: 42px; font-weight: 900; color: #409eff; line-height: 1; }
.eff-unit { font-size: 14px; font-weight: 500; color: #909399; margin-top: 2px; }
.eff-sub { font-size: 11px; color: #909399; margin-top: 6px; line-height: 1.5; }
.eff-count { color: #409eff; font-weight: 700; }
.eff-right { flex: 1; padding: 8px 0; display: flex; flex-direction: column; gap: 7px; min-width: 0; }
.eff-row { display: flex; align-items: center; gap: 8px; }
.eff-time { font-size: 11px; color: #909399; min-width: 60px; }
.eff-track { flex: 1; height: 10px; background: #f0f2f5; border-radius: 5px; overflow: hidden; min-width: 60px; }
.eff-fill { height: 100%; border-radius: 5px; transition: width 0.5s cubic-bezier(0.4,0,0.2,1); min-width: 2px; }
.eff-val { font-size: 11px; font-weight: 700; color: #303133; min-width: 30px; text-align: right; }

/* === Element Plus 覆盖 === */
:deep(.el-table) { --el-table-border-color: #f0f0f0; --el-table-header-bg-color: #fafafa; }
:deep(.el-table th.el-table__cell) { font-weight: 600; color: #606266; font-size: 12px; }
:deep(.el-table td.el-table__cell) { font-size: 12px; color: #303133; }
:deep(.el-select .el-input__wrapper) { border-radius: 8px; }
</style>

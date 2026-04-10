<template>
  <div class="dashboard">
    <!-- 页面标题区 -->
    <div class="dash-header">
      <div class="dash-title">
        <div>
          <div class="dash-h1">数据概览</div>
          <div class="dash-sub">实时监控代码审核数据</div>
        </div>
      </div>
      <div class="dash-controls">
        <el-select v-model="filterRepo" clearable placeholder="全部仓库" size="default" style="width:180px" @change="loadAll">
          <el-option v-for="r in repos" :key="r.id" :label="r.repoName" :value="r.id" />
        </el-select>
        <el-button type="primary" size="default" @click="loadAll" :loading="loading" class="btn">
          <RefreshRight v-if="!loading" class="btn-icon" />
          <Loading v-else class="btn-icon spinning" />
          <span class="btn-text">刷新</span>
        </el-button>
        <span class="update-time" v-if="lastUpdate">更新于 {{ lastUpdate }}</span>
      </div>
    </div>

    <!-- 数据总览 -->
    <div class="section-group">
      <div class="section-label">仓库总览</div>
      <div class="overview-row">
        <div v-for="(item, i) in overviewKpi" :key="i" class="overview-card">
          <component :is="item.icon" class="ov-icon" :style="{ color: ovColors[i] }" />
          <div class="ov-body">
            <div class="ov-num">{{ item.value }}</div>
            <div class="ov-label">{{ item.label }}</div>
          </div>
        </div>
      </div>
    </div>

    <!-- 问题概览 -->
    <div class="section-group">
      <div class="section-label">问题统计</div>
      <div class="stats-grid">
        <div v-for="(item, i) in kpiList" :key="i" class="stat-card" :class="{ 'stat-critical': item.severity === 'critical', 'stat-major': item.severity === 'major' }">
          <div class="stat-header">
            <component :is="item.icon" class="stat-icon" />
            <span class="stat-severity-dot"></span>
          </div>
          <div class="stat-value">{{ item.value }}</div>
          <div class="stat-label">{{ item.label }}</div>
        </div>
      </div>
    </div>

    <!-- 图表区 -->
    <div class="section-group">
      <div class="charts-grid">
        <!-- 7天趋势 -->
        <div class="chart-card chart-wide">
          <div class="chart-header">
            <span class="chart-title">7天问题趋势</span>
            <div class="chart-legend">
              <span class="leg"><span class="leg-dot leg-blue"></span>全部</span>
              <span class="leg"><span class="leg-dot leg-critical"></span>致命</span>
              <span class="leg"><span class="leg-dot leg-major"></span>严重</span>
            </div>
          </div>
          <div v-if="trend.dates?.length" ref="trendChart" class="echart" />
          <el-empty v-else description="暂无数据" :image-size="60" />
        </div>

        <!-- 问题类型分布 -->
        <div class="chart-card">
          <div class="chart-header">
            <span class="chart-title">问题类型分布</span>
          </div>
          <div v-if="Object.keys(stats.byType || {}).length" ref="typeChart" class="echart" />
          <el-empty v-else description="暂无数据" :image-size="60" />
        </div>
      </div>
    </div>

    <!-- 第二行图表 -->
    <div class="section-group">
      <div class="charts-grid charts-grid-3">
        <!-- 状态占比 -->
        <div class="chart-card">
          <div class="chart-header">
            <span class="chart-title">问题状态分布</span>
          </div>
          <div v-if="stats.total > 0" ref="statusChart" class="echart" />
          <el-empty v-else description="暂无数据" :image-size="60" />
        </div>

        <!-- 仓库排名 -->
        <div class="chart-card">
          <div class="chart-header">
            <span class="chart-title">仓库问题排名</span>
          </div>
          <div v-if="repoRanking.length" ref="rankingChart" class="echart" />
          <el-empty v-else description="暂无数据" :image-size="60" />
        </div>

        <!-- 最近审核 -->
        <div class="chart-card">
          <div class="chart-header">
            <span class="chart-title">最近审核</span>
          </div>
          <div class="table-scroll-wrapper" v-if="recentTasks.length">
            <table class="recent-table">
              <thead>
                <tr>
                  <th>仓库</th>
                  <th>Commit</th>
                  <th>问题</th>
                  <th>时间</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in recentTasks" :key="row.id || Math.random()">
                  <td>{{ row.repoName }}</td>
                  <td><span class="commit-sha">{{ row.commitSha?.slice(0, 7) }}</span></td>
                  <td class="center">{{ row.issueCount }}</td>
                  <td>{{ row.createTime?.replace('T', ' ').slice(0, 16) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
          <el-empty v-else description="暂无数据" :image-size="60" />
        </div>
      </div>
    </div>

    <!-- 处理效率 -->
    <div class="section-group" v-if="handling.avgHours > 0 || handling.count > 0">
      <div class="section-label">处理效率</div>
      <div class="eff-card">
        <div class="eff-main">
          <div class="eff-big">{{ handling.avgHours }}</div>
          <div class="eff-unit">小时</div>
          <div class="eff-sub">平均修复耗时 · <span class="eff-count">{{ handling.count }}</span> 条记录</div>
        </div>
        <div class="eff-bars">
          <div v-for="item in handling.details" :key="item.time" class="eff-row">
            <div class="eff-time">{{ item.time }}</div>
            <div class="eff-track">
              <div class="eff-fill" :class="barClass(item.hours)" :style="{ width: Math.min(100, item.hours / handling.avgHours * 100) + '%' }" />
            </div>
            <div class="eff-val">{{ item.hours }}h</div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick } from 'vue'
import { reviewApi, repositoryApi } from '@/api'
import { on, off } from '@/utils/eventBus'
import * as echarts from 'echarts'
import {
  RefreshRight, Loading, Box, Document, CircleCheck,
  Bell, HotWater, WarningFilled, Warning
} from '@element-plus/icons-vue'

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

const ovColors = ['#409eff', '#53d1a6', '#67d23a', '#e6a23c', '#f56c6c']

const overviewKpi = computed(() => [
  { icon: Box, label: '仓库数', value: overview.value.totalRepos || 0 },
  { icon: Document, label: '审核任务', value: overview.value.totalTasks || 0 },
  { icon: CircleCheck, label: '已完成', value: overview.value.totalReviews || 0 },
  { icon: Bell, label: '发现问题', value: overview.value.totalIssues || 0 },
  { icon: HotWater, label: '今日新增', value: overview.value.todayIssues || 0 },
])

const kpiList = computed(() => [
  { icon: Document, label: '问题总数', value: stats.value.total || 0, color: '#ffffff', severity: '' },
  { icon: WarningFilled, label: '致命问题', value: stats.value.bySeverity?.critical || 0, color: '#f56c6c', severity: 'critical' },
  { icon: Warning, label: '严重问题', value: stats.value.bySeverity?.major || 0, color: '#e6a23c', severity: 'major' },
  { icon: CircleCheck, label: '已修复', value: stats.value.fixedCount || 0, color: '#67d23a', severity: '' },
])

function formatTime() {
  const now = new Date()
  lastUpdate.value = now.toTimeString().slice(0, 5)
}

async function loadAll(showLoading = true) {
  if (showLoading) loading.value = true
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
  if (showLoading) loading.value = false
  await nextTick()
  renderCharts()
}

function renderCharts() {
  renderTrend()
  renderStatus()
  renderRanking()
  renderType()
}

function getChartColors() {
  const style = getComputedStyle(document.documentElement)
  return {
    text: style.getPropertyValue('--text-secondary').trim() || 'rgba(255,255,255,0.7)',
    border: style.getPropertyValue('--border-default').trim() || 'rgba(255,255,255,0.1)',
    bg: style.getPropertyValue('--bg-primary').trim() || '#1f2228',
  }
}

function renderTrend() {
  if (!trendChart.value || !trend.value.dates?.length) return
  if (trendChartIns) trendChartIns.dispose()
  trendChartIns = echarts.init(trendChart.value)
  const { text, border } = getChartColors()
  trendChartIns.setOption({
    tooltip: { trigger: 'axis', backgroundColor: 'rgba(31,34,40,0.95)', borderColor: border, textStyle: { color: text, fontSize: 12 } },
    legend: { show: false },
    grid: { left: 44, right: 20, top: 12, bottom: 24 },
    xAxis: { type: 'category', data: trend.value.dates, axisLine: { lineStyle: { color: border } }, axisLabel: { fontSize: 11, color: text }, splitLine: { show: false } },
    yAxis: { type: 'value', splitLine: { lineStyle: { color: border, type: 'dashed' } }, axisLabel: { fontSize: 11, color: text }, axisLine: { show: false } },
    series: [
      { name: '全部', data: trend.value.counts, type: 'line', smooth: 0.4, lineStyle: { color: '#409eff', width: 3 }, itemStyle: { color: '#409eff' }, areaStyle: { color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [{ offset: 0, color: 'rgba(64,158,255,0.18)' }, { offset: 1, color: 'rgba(64,158,255,0.01)' }]) }, symbol: 'circle', symbolSize: 5 },
      { name: '致命', data: trend.value.criticalCounts, type: 'line', smooth: 0.4, lineStyle: { color: '#f56c6c', width: 2.5 }, itemStyle: { color: '#f56c6c' }, symbol: 'circle', symbolSize: 4 },
      { name: '严重', data: trend.value.majorCounts, type: 'line', smooth: 0.4, lineStyle: { color: '#e6a23c', width: 2.5 }, itemStyle: { color: '#e6a23c' }, symbol: 'circle', symbolSize: 4 }
    ]
  });
  setTimeout(() => trendChartIns?.resize(), 50)
}

function renderStatus() {
  if (!statusChart.value || !stats.value.total) return
  if (statusChartIns) statusChartIns.dispose()
  const { text, border, bg } = getChartColors()
  const data = [
    { name: '待处理', value: stats.value.pending || 0, color: '#e6a23c' },
    { name: '已认领', value: stats.value.claimed || 0, color: '#409eff' },
    { name: '已修复', value: stats.value.fixedCount || 0, color: '#67d23a' },
    { name: '已忽略', value: stats.value.ignored || 0, color: '#909399' },
  ].filter(d => d.value > 0)
  statusChartIns = echarts.init(statusChart.value)
  statusChartIns.setOption({
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)', backgroundColor: 'rgba(31,34,40,0.95)', borderColor: border, textStyle: { color: text, fontSize: 12 } },
    legend: { orient: 'vertical', right: 4, top: 'center', textStyle: { fontSize: 10, color: text }, itemGap: 6 },
    series: [{ type: 'pie', radius: ['30%', '52%'], center: ['28%', '50%'], label: { show: false }, emphasis: { scale: true, scaleSize: 6, label: { show: true, fontSize: 12, fontWeight: 600 } }, data: data.map(d => ({ ...d, itemStyle: { color: d.color, borderRadius: 0, borderWidth: 2, borderColor: bg } })) }]
  });
  setTimeout(() => statusChartIns?.resize(), 50)
}

function renderRanking() {
  if (!rankingChart.value || !repoRanking.value.length) return
  if (rankingChartIns) rankingChartIns.dispose()
  const { text, border } = getChartColors()
  const data = repoRanking.value.map((r: any) => ({ name: r.name, value: r.count }))
  rankingChartIns = echarts.init(rankingChart.value)
  rankingChartIns.setOption({
    tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' }, backgroundColor: 'rgba(31,34,40,0.95)', borderColor: border, textStyle: { color: text, fontSize: 12 } },
    grid: { left: 90, right: 32, top: 8, bottom: 8 },
    xAxis: { type: 'value', splitLine: { lineStyle: { color: border, type: 'dashed' } }, axisLabel: { fontSize: 10, color: text }, axisLine: { show: false } },
    yAxis: { type: 'category', data: data.map(d => d.name).reverse(), axisLabel: { fontSize: 11, color: text }, axisLine: { lineStyle: { color: border } }, axisTick: { show: false } },
    series: [{ type: 'bar', data: data.map(d => d.value).reverse(), barWidth: 14, itemStyle: { color: new echarts.graphic.LinearGradient(0, 0, 1, 0, [{ offset: 0, color: '#409eff' }, { offset: 1, color: '#53d1a6' }]), borderRadius: [0, 0, 0, 0] }, label: { show: true, position: 'right', fontSize: 11, color: text, formatter: '{c}' } }]
  });
  setTimeout(() => rankingChartIns?.resize(), 50)
}

const typeNames: Record<string, string> = {
  security: '安全', correctness: '正确性', performance: '性能',
  maintainability: '可维护性', best_practice: '最佳实践', code_style: '代码风格', other: '其他'
}
const typeColors: Record<string, string> = {
  security: '#f56c6c', correctness: '#e6a23c', performance: '#409eff',
  maintainability: '#909399', best_practice: '#67d23a', code_style: '#b37feb', other: '#bbb'
}

function renderType() {
  if (!typeChart.value || !stats.value.byType) return
  if (typeChartIns) typeChartIns.dispose()
  const { text, border, bg } = getChartColors()
  const entries = Object.entries(stats.value.byType as Record<string, number>)
  if (!entries.length) return
  entries.sort((a, b) => b[1] - a[1])
  typeChartIns = echarts.init(typeChart.value)
  typeChartIns.setOption({
    tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)', backgroundColor: 'rgba(31,34,40,0.95)', borderColor: border, textStyle: { color: text, fontSize: 12 } },
    series: [{ type: 'pie', radius: ['38%', '62%'], center: ['50%', '50%'], label: { formatter: '{b}\n{d}%', fontSize: 11, color: text }, data: entries.map(([k, v]) => ({ name: typeNames[k] || k, value: v, itemStyle: { color: typeColors[k] || '#999', borderRadius: 0, borderWidth: 2, borderColor: bg } })) }]
  });
  setTimeout(() => typeChartIns?.resize(), 50)
}

function barClass(h: number) {
  if (h < 1) return 'eff-fill-fast'
  if (h < 4) return 'eff-fill-medium'
  if (h < 24) return 'eff-fill-slow'
  return 'eff-fill-critical'
}

function reRenderCharts() {
  renderCharts()
}

onMounted(() => {
  repositoryApi.list().then(res => { if (res.success) repos.value = res.data || [] })
  loadAll()
  autoTimer = setInterval(() => loadAll(false), 30000)
  window.addEventListener('resize', () => {
    trendChartIns?.resize(); statusChartIns?.resize(); rankingChartIns?.resize(); typeChartIns?.resize()
  })
  on('theme-changed', reRenderCharts)
})

onUnmounted(() => {
  if (autoTimer) clearInterval(autoTimer)
  off('theme-changed', reRenderCharts)
  trendChartIns?.dispose()
  statusChartIns?.dispose()
  rankingChartIns?.dispose()
  typeChartIns?.dispose()
})
</script>

<style>
</style>

<style scoped>
.dashboard {
  padding: 12px 16px;
  min-height: 100vh;
}

/* === 头部 === */
.dash-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 24px;
  padding: 16px 20px;
  background: var(--bg-surface);
  border: 1px solid var(--border-default);
}

.dash-title {
  display: flex;
  align-items: center;
  gap: 14px;
}

.dash-icon {
  width: 44px;
  height: 44px;
  color: var(--text-primary);
}

.dash-h1 {
  font-family: var(--font-body);
  font-size: 16px;
  font-weight: 400;
  text-transform: none;
  letter-spacing: normal;
  color: var(--text-primary);
}

.dash-sub {
  font-size: 12px;
  color: var(--text-muted);
  margin-top: 2px;
}

.dash-controls {
  display: flex;
  align-items: center;
  gap: 12px;
}

.update-time {
  font-family: var(--font-body);
  font-size: 11px;
  color: var(--text-muted);
  white-space: nowrap;
}

.btn-icon {
  width: 14px;
  height: 14px;
  margin-right: 4px;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.spinning {
  animation: spin 1s linear infinite;
}

/* === Section Groups === */
.section-group {
  margin-bottom: 20px;
}

.section-label {
  font-family: var(--font-body);
  font-size: 11px;
  font-weight: 400;
  color: var(--text-muted);
  text-transform: none;
  letter-spacing: normal;
  margin-bottom: 12px;
  padding-left: 4px;
}

/* === 仓库总览卡片 === */
.overview-row {
  display: grid;
  grid-template-columns: repeat(5, 1fr);
  gap: 12px;
}

.overview-card {
  background: var(--bg-surface);
  border: 1px solid var(--border-default);
  padding: 14px 16px;
  display: flex;
  align-items: center;
  gap: 12px;
  transition: border-color 0.2s;
}

.overview-card:hover {
  border-color: var(--border-strong);
}

.ov-icon {
  width: 40px;
  height: 40px;
  flex-shrink: 0;
  background: var(--bg-surface-hover);
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid;
  border-color: inherit;
}

.ov-body {
  flex: 1;
  min-width: 0;
}

.ov-num {
  font-family: var(--font-display);
  font-size: 24px;
  font-weight: 300;
  color: var(--text-primary);
  line-height: 1;
}

.ov-label {
  font-family: var(--font-display);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--text-muted);
  margin-top: 4px;
}

/* === KPI 卡片 === */
.stats-grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 12px;
}

.stat-card {
  background: var(--bg-surface);
  border: 1px solid var(--border-default);
  padding: 16px;
  transition: border-color 0.2s;
}

.stat-card:hover {
  border-color: var(--border-strong);
}

.stat-card.stat-critical {
  border-left: 3px solid var(--color-critical);
}
.stat-card.stat-critical .stat-value {
  color: var(--color-critical);
}
.stat-card.stat-critical .stat-severity-dot {
  background: var(--color-critical);
}

.stat-card.stat-major {
  border-left: 3px solid var(--color-major);
}
.stat-card.stat-major .stat-value {
  color: var(--color-major);
}
.stat-card.stat-major .stat-severity-dot {
  background: var(--color-major);
}

.stat-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
}

.stat-icon {
  width: 18px;
  height: 18px;
}

.stat-severity-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--text-muted);
}

.stat-value {
  font-family: var(--font-display);
  font-size: 32px;
  font-weight: 300;
  line-height: 1;
  margin-bottom: 4px;
  color: var(--text-primary);
}

.stat-label {
  font-family: var(--font-display);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--text-muted);
}

/* === 图表网格 === */
.charts-grid {
  display: grid;
  grid-template-columns: 3fr 2fr;
  gap: 12px;
}

.charts-grid-3 {
  grid-template-columns: repeat(3, 1fr);
}

.chart-card {
  background: var(--bg-surface);
  border: 1px solid var(--border-default);
  padding: 16px;
  transition: border-color 0.2s;
}

.chart-card:hover {
  border-color: var(--border-strong);
}

.chart-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}

.chart-title {
  font-family: var(--font-body);
  font-size: 12px;
  font-weight: 400;
  text-transform: none;
  letter-spacing: normal;
  color: var(--text-primary);
}

.chart-legend {
  display: flex;
  gap: 12px;
}

.leg {
  display: flex;
  align-items: center;
  gap: 6px;
  font-family: var(--font-display);
  font-size: 11px;
  color: var(--text-muted);
}

.leg-dot {
  width: 8px;
  height: 8px;
}
.leg-dot.leg-blue { background: var(--ring-blue); }
.leg-dot.leg-critical { background: var(--color-critical); }
.leg-dot.leg-major { background: var(--color-major); }

.echart {
  width: 100%;
  height: 220px;
}

/* === 表格样式 === */
.commit-sha {
  font-family: var(--font-display);
  font-size: 12px;
  color: var(--ring-blue);
  background: var(--bg-surface-hover);
  padding: 2px 6px;
}

.recent-table {
  width: 100%;
  border-collapse: collapse;
}

.recent-table th,
.recent-table td {
  padding: 12px 8px;
  text-align: left;
  font-size: 13px;
  color: var(--text-secondary);
  border-bottom: 1px solid var(--border-default);
}

.recent-table th {
  font-family: var(--font-display);
  font-size: 11px;
  font-weight: 400;
  text-transform: uppercase;
  letter-spacing: 1px;
  color: var(--text-muted);
  background: var(--bg-surface);
}

.recent-table td.center {
  text-align: center;
}

/* === 处理效率 === */
.eff-card {
  background: var(--bg-surface);
  border: 1px solid var(--border-default);
  padding: 20px 24px;
  display: flex;
  gap: 48px;
  align-items: flex-start;
}

.eff-main {
  text-align: center;
  min-width: 120px;
}

.eff-big {
  font-family: var(--font-display);
  font-size: 56px;
  font-weight: 300;
  color: var(--text-primary);
  line-height: 1;
}

.eff-unit {
  font-family: var(--font-display);
  font-size: 14px;
  color: var(--text-muted);
  margin-top: 4px;
}

.eff-sub {
  font-family: var(--font-display);
  font-size: 11px;
  color: var(--text-muted);
  margin-top: 8px;
}

.eff-count {
  color: var(--text-primary);
  font-weight: 600;
}

.eff-bars {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding-top: 8px;
}

.eff-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.eff-time {
  font-family: var(--font-display);
  font-size: 11px;
  color: var(--text-muted);
  min-width: 50px;
}

.eff-track {
  flex: 1;
  height: 8px;
  background: var(--bg-surface-hover);
}

.eff-fill {
  height: 100%;
  transition: width 0.5s cubic-bezier(0.4, 0, 0.2, 1);
}
.eff-fill-fast { background: var(--color-success); }
.eff-fill-medium { background: var(--ring-blue); }
.eff-fill-slow { background: var(--color-major); }
.eff-fill-critical { background: var(--color-critical); }

.eff-val {
  font-family: var(--font-display);
  font-size: 11px;
  color: var(--text-secondary);
  min-width: 35px;
  text-align: right;
}

/* === Responsive === */
@media (max-width: 1200px) {
  .overview-row {
    grid-template-columns: repeat(3, 1fr);
  }
}

@media (max-width: 1024px) {
  .stats-grid {
    grid-template-columns: repeat(2, 1fr);
  }

  .charts-grid {
    grid-template-columns: 1fr;
  }

  .charts-grid-3 {
    grid-template-columns: 1fr;
  }

  .eff-card {
    flex-direction: column;
    gap: 24px;
  }

  .eff-main {
    text-align: left;
  }
}

@media (max-width: 768px) {
  .dashboard {
    padding: 0;
  }

  .overview-row {
    grid-template-columns: repeat(2, 1fr);
  }

  .dash-header {
    flex-direction: column;
    gap: 12px;
    align-items: flex-start;
  }
}

@media (max-width: 480px) {
  .overview-row {
    grid-template-columns: 1fr;
  }

  .stats-grid {
    grid-template-columns: 1fr;
  }

  .chart-card {
    padding: 8px;
    overflow: hidden;
  }

  .echart {
    height: 160px;
    width: 100%;
  }

  /* 原生 table 横向滚动 */
  .table-scroll-wrapper {
    overflow-x: auto;
    width: 100%;
  }

  .recent-table {
    width: 100%;
    border-collapse: collapse;
    table-layout: fixed;
  }

  .recent-table th,
  .recent-table td {
    padding: 8px 6px;
    text-align: left;
    font-size: 12px;
    border-bottom: 1px solid var(--border-default);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .recent-table th {
    font-weight: 600;
    color: var(--text-muted);
    background: var(--bg-surface);
    text-transform: uppercase;
    font-size: 10px;
    letter-spacing: 0.5px;
  }

  .recent-table td.center {
    text-align: center;
  }

}
</style>

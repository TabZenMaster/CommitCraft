<template>
  <div class="table-card">
    <div v-if="$slots.header" class="card-header">
      <slot name="header" />
    </div>

    <div class="card-body">
      <div
        v-for="col in columns"
        :key="col.key"
        class="card-row"
      >
        <div class="card-label">{{ col.label }}</div>
        <div class="card-value">
          <slot :name="col.key" :row="row" :value="getVal(col)">
            {{ fmt(getVal(col), col) }}
          </slot>
        </div>
      </div>
    </div>

    <div v-if="$slots.footer" class="card-footer">
      <slot name="footer" />
    </div>
  </div>
</template>

<script setup lang="ts">
interface Column {
  key: string
  label: string
  format?: (val: any) => string
  cls?: string
}

const props = defineProps<{
  row: Record<string, any>
  columns: Column[]
}>()

function getVal(col: Column) {
  return props.row[col.key]
}

function fmt(val: any, col: Column) {
  if (col.format) return col.format(val)
  if (val == null || val === '') return '—'
  return val
}
</script>

<style scoped>
.table-card {
  background: var(--bg-surface);
  border: 1px solid var(--border-default);
  border-radius: 0;
  margin-bottom: 10px;
  overflow: hidden;
}

.card-header {
  padding: 10px 14px;
  border-bottom: 1px solid var(--border-default);
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  background: var(--bg-surface-hover);
}

.card-body {
  padding: 4px 0;
}

.card-row {
  display: flex;
  gap: 12px;
  padding: 7px 14px;
  border-bottom: 1px solid var(--border-default);
  align-items: flex-start;
}

.card-row:last-child {
  border-bottom: none;
}

.card-label {
  font-family: var(--font-display);
  font-size: 10px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--text-muted);
  min-width: 70px;
  flex-shrink: 0;
  padding-top: 2px;
}

.card-value {
  flex: 1;
  font-size: 13px;
  color: var(--text-primary);
  line-height: 1.5;
  word-break: break-all;
  min-width: 0;
}

.card-footer {
  padding: 10px 14px;
  border-top: 1px solid var(--border-default);
  display: flex;
  gap: 8px;
  align-items: center;
  flex-wrap: wrap;
}

/* Status / tag badge in card */
.card-value :deep(.table-tag) {
  display: inline-block;
  font-family: var(--font-display);
  font-size: 10px;
  padding: 2px 6px;
  border: 1px solid;
  border-radius: 2px;
  text-transform: uppercase;
}

.card-value :deep(.table-tag.danger) { color: var(--color-critical); border-color: var(--color-critical); }
.card-value :deep(.table-tag.warning) { color: var(--color-major); border-color: var(--color-major); }
.card-value :deep(.table-tag.success) { color: var(--color-success); border-color: var(--color-success); }
.card-value :deep(.table-tag.info) { color: var(--text-muted); border-color: var(--border-default); }
.card-value :deep(.table-tag.purple) { color: #b37feb; border-color: #b37feb; }

.card-value :deep(.action-link) {
  font-family: var(--font-display);
  font-size: 12px;
  color: var(--ring-blue);
  text-decoration: none;
  cursor: pointer;
  padding: 2px 0;
  border: none;
  background: none;
  transition: opacity 0.15s;
}

.card-value :deep(.action-link:hover) {
  opacity: 0.7;
  text-decoration: underline;
}

.card-value :deep(.action-link.danger) { color: var(--color-critical); }
.card-value :deep(.action-link.warning) { color: var(--color-major); }
.card-value :deep(.action-link.success) { color: var(--color-success); }
.card-value :deep(.action-link.muted) { color: var(--text-muted); }
.card-value :deep(.action-link.muted:hover) { text-decoration: none; opacity: 1; }
</style>

import { ref } from 'vue'

// 简单事件总线
const listeners: Record<string, Set<() => void>> = {}

export function on(event: string, cb: () => void) {
  if (!listeners[event]) listeners[event] = new Set()
  listeners[event].add(cb)
}

export function off(event: string, cb: () => void) {
  listeners[event]?.delete(cb)
}

export function emit(event: string) {
  listeners[event]?.forEach(cb => cb())
}

// 专用于通知刷新任务列表
export const refreshTasks = () => emit('review-updated')

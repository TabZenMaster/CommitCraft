import { ref } from 'vue'

export type Breakpoint = 'xs' | 'sm' | 'md' | 'lg'

const breakpoint = ref<Breakpoint>('lg')
let initialized = false

function update() {
  const w = window.innerWidth
  if (w < 480) breakpoint.value = 'xs'
  else if (w < 768) breakpoint.value = 'sm'
  else if (w < 1024) breakpoint.value = 'md'
  else breakpoint.value = 'lg'
}

function init() {
  if (initialized) return
  initialized = true
  update()
  window.addEventListener('resize', update, { passive: true })
}

export function useBreakpoint() {
  init()
  return { breakpoint }
}

export function isMobile(): boolean {
  return breakpoint.value === 'xs' || breakpoint.value === 'sm'
}

export function isTablet(): boolean {
  return breakpoint.value === 'md'
}

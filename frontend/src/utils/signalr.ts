import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr'
import { ElNotification } from 'element-plus'
import { refreshTasks } from './eventBus'

let connection: HubConnection | null = null
export { connection }
let starting = false

interface Notification {
  title: string
  message: string
  type: 'success' | 'error' | 'info' | 'warning'
  timestamp: string
  data?: { repositoryId: number; reviewCommitId: number; success: boolean }
}

const typeMap: Record<string, 'success' | 'warning' | 'error' | 'info'> = {
  success: 'success',
  error: 'error',
  warning: 'warning',
  info: 'info',
}

export function startSignalR() {
  const token = localStorage.getItem('cr_token')
  if (!token) return

  if (connection?.state === HubConnectionState.Connected ||
      connection?.state === HubConnectionState.Connecting ||
      starting) return

  starting = true
  connection = new HubConnectionBuilder()
    .withUrl('/hubs/notifications', { accessTokenFactory: () => token })
    .withAutomaticReconnect({ nextRetryDelayInSeconds: 10 })
    .build()

  connection.onclose(() => { starting = false })
  connection.onreconnected(() => { starting = false })
  connection.start().then(() => {
    console.log('SignalR 已连接，connectionId=', connection?.connectionId)
    starting = false
  }).catch(err => {
    console.error('SignalR 连接失败:', err)
    starting = false
  })

  // Global debug: catch all ReceiveAiStream* before specific handlers
  ;['ReceiveAiStreamToken', 'ReceiveAiStreamEnd'].forEach(ev => {
    ;(connection as any).on(ev, (...args: any[]) => {
      console.log('[SignalR Global]', ev, 'args:', JSON.stringify(args).slice(0, 200))
    })
  })

  connection.on('ReceiveNotification', (payload: Notification) => {
    // 刷新任务列表
    refreshTasks()

    ElNotification({
      title: payload.title,
      message: payload.message,
      type: typeMap[payload.type] || 'info',
      position: 'top-right',
      duration: 0,
      showClose: true,
    })
  })
}

export function stopSignalR() {
  if (connection?.state === HubConnectionState.Connected) {
    connection.stop().catch(() => {})
    connection = null
  }
}

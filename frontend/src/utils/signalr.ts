import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr'
import { ElMessage } from 'element-plus'

let connection: HubConnection | null = null

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

  // 如果已连接，不要重复建立
  if (connection?.state === HubConnectionState.Connected) return

  connection = new HubConnectionBuilder()
    .withUrl('/hubs/notifications', { accessTokenFactory: () => token })
    .withAutomaticReconnect({ nextRetryDelayInSeconds: 10 })
    .build()

  connection.on('ReceiveNotification', (payload: Notification) => {
    ElMessage({
      message: `${payload.title} ${payload.message}`,
      type: typeMap[payload.type] || 'info',
      duration: 0, // 不自动关闭，用户手动关闭
      showClose: true,
    })
  })

  connection.start().catch(err => {
    console.warn('SignalR 连接失败:', err)
  })
}

export function stopSignalR() {
  if (connection?.state === HubConnectionState.Connected) {
    connection.stop().catch(() => {})
  }
}

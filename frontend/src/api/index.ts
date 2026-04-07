import request from '@/utils/request'

export interface LoginData { username: string; password: string }
export const authApi = {
  login: (data: LoginData) => request.post('/auth/login', data),
  getMe: () => request.get('/auth/me'),
}

export const modelConfigApi = {
  list: () => request.get('/ModelConfig/list'),
  add: (data: any) => request.post('/ModelConfig/add', data),
  update: (data: any) => request.post('/ModelConfig/update', data),
  delete: (id: number) => request.delete(`/ModelConfig/${id}`),
  test: (id: number) => request.post('/ModelConfig/test', { id }),
}

export const repositoryApi = {
  list: () => request.get('/Repository/list'),
  get: (id: number) => request.get(`/Repository/${id}`),
  add: (data: any) => request.post('/Repository/add', data),
  update: (data: any) => request.post('/Repository/update', data),
  delete: (id: number) => request.delete(`/Repository/${id}`),
  test: (id: number) => request.post('/Repository/test', { id }),
  getCommits: (id: number, branch: string) =>
    request.get(`/Repository/${id}/commits`, { params: { branch } }),
  fetch: (data: { repoId: number; repoUrl: string }) => request.post('/Repository/fetch', data),
  getPlainToken: (id: number) => request.get(`/Repository/${id}/plain-token`),
  getBranches: (id: number) => request.get(`/Repository/${id}/branches`),
  models: () => request.get('/modelconfig/list'),
}

export const reviewApi = {
  tasks: (repositoryId = 0) => request.get('/Review/tasks', { params: { repositoryId } }),
  task: (id: number) => request.get(`/Review/task/${id}`),
  trigger: (data: {
    repositoryId: number
    commitSha: string
    commitMessage: string
    committer: string
    committedAt: string
    branchName: string
  }) => request.post('/Review/trigger', data),
  results: (params: {
    reviewCommitId?: number
    repositoryId?: number
    pageIndex?: number
    pageSize?: number
    severity?: string
    status?: number
    issueType?: string
  }) => request.get('/Review/results', { params }),
  claim: (id: number) => request.post('/Review/claim', { id }),
  handle: (data: { id: number; status: number; memo?: string }) => request.post('/Review/handle', data),
  retry: (id: number) => request.post(`/Review/retry/${id}`),
  statistics: (repositoryId?: number) => request.get('/Review/statistics', { params: repositoryId ? { repositoryId } : {} }),
  trend: (repositoryId?: number) => request.get('/Review/trend', { params: repositoryId ? { repositoryId } : {} }),
  repoRanking: () => request.get('/Review/repo-ranking'),
  recentTasks: (limit = 10) => request.get('/Review/recent-tasks', { params: { limit } }),
  repoOverview: () => request.get('/Review/repo-overview'),
  handlingStats: () => request.get('/Review/handling-stats'),
}

export const sysUserApi = {
  list: () => request.get('/SysUser/list'),
  add: (data: any) => request.post('/SysUser/add', data),
  update: (data: any) => request.post('/SysUser/update', data),
  delete: (id: number) => request.delete(`/SysUser/${id}`),
}

export const scheduleApi = {
  list: () => request.get('/Schedule/list'),
  add: (data: { repositoryId: number; branchName: string; cronExpr: string; enabled?: number }) =>
    request.post('/Schedule/add', data),
  update: (data: { id: number; branchName: string; cronExpr: string; enabled: number }) =>
    request.post('/Schedule/update', data),
  delete: (id: number) => request.post('/Schedule/delete', { id }),
  trigger: (id: number) => request.post('/Schedule/trigger', { id }),
  getLogs: (scheduleId = 0, limit = 50) => request.get('/Schedule/logs', { params: { scheduleId, limit } }),
}

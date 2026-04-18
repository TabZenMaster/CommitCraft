import request from '@/utils/request'

export interface QuickRepoDto {
  repoUrl: string
  accessToken: string
}

export interface BranchItem {
  name: string
  commit: string
}

export interface QuickFilesDto {
  repoUrl: string
  accessToken: string
  commitSha: string
}

export interface FileItem {
  filename: string
  patch: string
  status: string
}

export interface ReviewFileDto {
  modelApiUrl: string
  modelName: string
  apiKey: string
  filename: string
  status: string
  patch: string
}

export interface ReviewFileResult {
  filename: string
  result: string
}

export interface QuickCommitsDto {
  repoUrl: string
  accessToken: string
  branchName: string
}

export interface CommitItem {
  sha: string
  fullSha: string
  shortMsg: string
  date: string
  author: string
}

export interface QuickReviewResult {
  commitSha: string
  commitMessage: string
  committer: string
  branchName: string
  reviewText: string
  reviewedAt: string
}

export interface TestAiDto {
  modelApiUrl: string
  modelName: string
  apiKey: string
}

export const quickApi = {
  /** 获取分支列表 */
  getBranches(dto: QuickRepoDto) {
    return request.post<any, any>('/quick/branches', dto)
  },

  /** 获取分支提交列表 */
  getCommits(dto: QuickCommitsDto) {
    return request.post<any, any>('/quick/commits', dto)
  },

  /** 获取提交文件列表 */
  getFiles(dto: QuickFilesDto) {
    return request.post<any, any>('/quick/files', dto)
  },

  /** 单文件AI审核 */
  reviewFile(dto: ReviewFileDto) {
    return request.post<any, any>('/quick/review-file', dto)
  },

  /** 测试 AI 接口 */
  testAi(dto: TestAiDto) {
    return request.post<any, any>('/quick/test-ai', dto)
  },
}

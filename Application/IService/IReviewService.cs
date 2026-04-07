using CodeReview.Domain.Entities;

namespace CodeReview.Application.IService;

public interface IReviewService
{
    /// <summary>获取审核任务列表</summary>
    Task<List<ReviewCommit>> GetTaskListAsync(int repositoryId = 0);
    /// <summary>获取单次任务详情</summary>
    Task<ReviewCommit?> GetTaskByIdAsync(int id);
    /// <summary>手动触发审核（指定 Commit SHA）</summary>
    Task<Result> TriggerReviewAsync(int repositoryId, string commitSha, string commitMessage, string committer, DateTime committedAt, string branchName);
    /// <summary>执行审核（内部调用）</summary>
    Task ExecuteReviewAsync(int reviewCommitId);
    /// <summary>获取审核结果列表</summary>
    Task<List<ReviewResult>> GetResultsAsync(int reviewCommitId = 0, int repositoryId = 0);
    /// <summary>获取审核结果列表（分页）</summary>
    Task<PagedResult<ReviewResult>> GetResultsPageAsync(int reviewCommitId, int repositoryId, int pageIndex, int pageSize, string? severity, int? status, string? issueType = null);
    /// <summary>认领问题</summary>
    Task<Result> ClaimIssueAsync(int resultId, int userId, string userName);
    /// <summary>处理问题（修复/忽略）</summary>
    Task<Result> HandleIssueAsync(int resultId, int userId, string userName, int status, string? memo);
    /// <summary>重试失败的任务</summary>
    Task<Result> RetryReviewAsync(int id);
    /// <summary>获取问题统计</summary>
    Task<Result<object>> GetIssueStatisticsAsync(int repositoryId = 0);
    Task<Result<object>> GetTrendAsync(int repositoryId = 0);
    Task<Result<object>> GetRepoRankingAsync();
    Task<Result<object>> GetRecentTasksAsync(int limit = 10);
    Task<Result<object>> GetRepoOverviewAsync();
    Task<Result<object>> GetHandlingStatsAsync();
}

using System.Net.Http.Json;
using System.Text.Json;
using CodeReview.Application.IService;
using CodeReview.Domain.Entities;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace CodeReview.Application.Service;

public class ReviewScheduleService : IReviewScheduleService
{
    private readonly ISqlSugarClient _db;
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewScheduleService> _logger;

    public ReviewScheduleService(
        ISqlSugarClient db,
        IReviewService reviewService,
        ILogger<ReviewScheduleService> logger)
    {
        _db = db;
        _reviewService = reviewService;
        _logger = logger;
    }

    public async Task<List<ReviewSchedule>> GetAllAsync()
    {
        return await _db.Queryable<ReviewSchedule>()
            .Where(x => !x.IsDeleted)
            .ToListAsync();
    }

    public async Task<List<ReviewSchedule>> GetEnabledAsync()
    {
        return await _db.Queryable<ReviewSchedule>()
            .Where(x => !x.IsDeleted && x.Enabled == 1)
            .ToListAsync();
    }

    public async Task<ReviewSchedule?> GetByIdAsync(int id)
    {
        return await _db.Queryable<ReviewSchedule>()
            .Where(x => x.Id == id && !x.IsDeleted)
            .FirstAsync();
    }

    public async Task<int> CreateAsync(int repositoryId, string branchName, string cronExpr)
    {
        var schedule = new ReviewSchedule
        {
            RepositoryId = repositoryId,
            BranchName = branchName,
            CronExpr = cronExpr,
            Enabled = 1
        };
        return await _db.Insertable(schedule).ExecuteReturnIdentityAsync();
    }

    public async Task UpdateAsync(int id, string branchName, string cronExpr, int enabled)
    {
        await _db.Updateable<ReviewSchedule>()
            .SetColumns(x => x.BranchName == branchName)
            .SetColumns(x => x.CronExpr == cronExpr)
            .SetColumns(x => x.Enabled == enabled)
            .Where(x => x.Id == id)
            .ExecuteCommandAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await _db.Updateable<ReviewSchedule>()
            .SetColumns(x => x.IsDeleted == true)
            .Where(x => x.Id == id)
            .ExecuteCommandAsync();
    }

    /// <summary>
    /// 扫描仓库分支，获取从上次审核之后的所有未审核 commits
    /// </summary>
    public async Task<List<CommitInfo>> GetUnreviewedCommitsAsync(int repositoryId, string branchName)
    {
        var repo = await _db.Queryable<Repository>()
            .Where(x => x.Id == repositoryId && !x.IsDeleted && x.Status == 1)
            .FirstAsync();
        if (repo == null) return new List<CommitInfo>();

        var token = CryptoHelper.Decrypt(repo.GiteeToken);
        var owner = repo.Owner;
        var repoPath = repo.RepoPath;

        using var wc = new HttpClient();
        wc.DefaultRequestHeaders.Add("user-agent", "CodeReview/1.0");

        // 获取分支信息
        var branchUrl = $"https://gitee.com/api/v5/repos/{owner}/{repoPath}/branches/{branchName}?access_token={token}";
        var branchResp = await wc.GetAsync(branchUrl);
        if (!branchResp.IsSuccessStatusCode) return new List<CommitInfo>();

        var branchJson = await branchResp.Content.ReadFromJsonAsync<JsonElement>();
        var latestSha = branchJson.GetProperty("commit").GetProperty("sha").GetString();
        if (string.IsNullOrEmpty(latestSha)) return new List<CommitInfo>();

        // 获取该仓库该分支所有已审核过的 commit SHA（只取最后一次审核的）
        var lastReviewed = await _db.Queryable<ReviewCommit>()
            .Where(x => x.RepositoryId == repositoryId && x.BranchName == branchName && x.Status == 2)
            .OrderByDescending(x => x.CreateTime)
            .Select(x => x.CommitSha)
            .FirstAsync();

        var commits = new List<CommitInfo>();

        if (!string.IsNullOrEmpty(lastReviewed) && lastReviewed == latestSha)
        {
            // 最新 commit 已审核，无需处理
            return commits;
        }

        // 获取 compare 接口，比对 lastReviewed 到 latestSha 之间的 commits
        var baseRef = lastReviewed ?? latestSha;
        var compareUrl = $"https://gitee.com/api/v5/repos/{owner}/{repoPath}/compare/{baseRef}...{latestSha}?access_token={token}";
        var compareResp = await wc.GetAsync(compareUrl);

        if (compareResp.IsSuccessStatusCode)
        {
            var compareJson = await compareResp.Content.ReadFromJsonAsync<JsonElement>();
            foreach (var c in compareJson.GetProperty("commits").EnumerateArray())
            {
                var sha = c.GetProperty("sha").GetString();
                // 跳过已经审核过的（如果 lastReviewed 为空则不跳过首个）
                if (!string.IsNullOrEmpty(lastReviewed) && sha == lastReviewed) continue;
                commits.Add(new CommitInfo
                {
                    Sha = sha!,
                    Message = c.GetProperty("commit").GetProperty("message").GetString() ?? "",
                    Committer = c.GetProperty("committer").GetProperty("login").GetString() ?? "",
                    CommittedAt = c.GetProperty("commit").GetProperty("created_at").GetDateTime()
                });
            }
        }

        // 如果 compare 失败，至少尝试获取最新 commit
        if (commits.Count == 0 && lastReviewed != latestSha)
        {
            var commitUrl = $"https://gitee.com/api/v5/repos/{owner}/{repoPath}/commits/{latestSha}?access_token={token}";
            var commitResp = await wc.GetAsync(commitUrl);
            if (commitResp.IsSuccessStatusCode)
            {
                var cj = await commitResp.Content.ReadFromJsonAsync<JsonElement>();
                commits.Add(new CommitInfo
                {
                    Sha = latestSha!,
                    Message = cj.GetProperty("commit").GetProperty("message").GetString() ?? "",
                    Committer = cj.GetProperty("committer").GetProperty("login").GetString() ?? "",
                    CommittedAt = cj.GetProperty("commit").GetProperty("created_at").GetDateTime()
                });
            }
        }

        return commits;
    }

    /// <summary>
    /// 为指定 schedule 创建审核任务
    /// </summary>
    public async Task TriggerScheduleAsync(int scheduleId)
    {
        var schedule = await GetByIdAsync(scheduleId);
        if (schedule == null || schedule.Enabled != 1) return;

        var commits = await GetUnreviewedCommitsAsync(schedule.RepositoryId, schedule.BranchName);
        foreach (var commit in commits)
        {
            await _reviewService.TriggerReviewAsync(
                schedule.RepositoryId,
                commit.Sha,
                commit.Message,
                commit.Committer,
                commit.CommittedAt,
                schedule.BranchName);
            _logger.LogInformation("Schedule {ScheduleId} 触发：仓库 {RepoId} 分支 {Branch} 提交 {Sha} 已入队",
                scheduleId, schedule.RepositoryId, schedule.BranchName, commit.Sha[..Math.Min(8, commit.Sha.Length)]);
        }

        // 更新最后触发时间
        await _db.Updateable<ReviewSchedule>()
            .SetColumns(x => x.LastTriggerAt == DateTime.Now)
            .Where(x => x.Id == scheduleId)
            .ExecuteCommandAsync();
    }
}

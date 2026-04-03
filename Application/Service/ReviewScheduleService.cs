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
    /// 立即标记计划已触发，防止同一触发点被重复执行
    /// </summary>
    public async Task MarkTriggeredAsync(int scheduleId, DateTime triggerTime)
    {
        await _db.Updateable<ReviewSchedule>()
            .SetColumns(x => x.LastTriggerAt == triggerTime)
            .Where(x => x.Id == scheduleId)
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
            if (compareJson.TryGetProperty("commits", out var commitsArr))
            {
                foreach (var c in commitsArr.EnumerateArray())
                {
                    var sha = c.TryGetProperty("sha", out var shaEl) ? shaEl.GetString() : null;
                    if (string.IsNullOrEmpty(sha)) continue;
                    if (!string.IsNullOrEmpty(lastReviewed) && sha == lastReviewed) continue;

                    var msg = c.TryGetProperty("commit", out var commitEl) && commitEl.TryGetProperty("message", out var msgEl)
                        ? msgEl.GetString() ?? ""
                        : "";
                    var authorName = "";
                    if (c.TryGetProperty("commit", out var co) && co.TryGetProperty("author", out var ao) && ao.TryGetProperty("name", out var no))
                        authorName = no.GetString() ?? "";
                    var committedAt = c.TryGetProperty("commit", out var caEl) && caEl.TryGetProperty("created_at", out var caDateEl)
                        ? caDateEl.GetDateTime()
                        : DateTime.UtcNow;

                    commits.Add(new CommitInfo
                    {
                        Sha = sha!,
                        Message = msg,
                        Committer = authorName,
                        CommittedAt = committedAt
                    });
                }
            }
        }

        // 如果 compare 失败（commits为空），至少尝试获取最新 commit
        if (commits.Count == 0 && lastReviewed != latestSha)
        {
            var commitUrl = $"https://gitee.com/api/v5/repos/{owner}/{repoPath}/commits/{latestSha}?access_token={token}";
            var commitResp = await wc.GetAsync(commitUrl);
            if (commitResp.IsSuccessStatusCode)
            {
                var cj = await commitResp.Content.ReadFromJsonAsync<JsonElement>();
                var msg = cj.TryGetProperty("commit", out var c1) && c1.TryGetProperty("message", out var m1) ? m1.GetString() ?? "" : "";
                var authorName = cj.TryGetProperty("commit", out var ca2) && ca2.TryGetProperty("author", out var aa2) && aa2.TryGetProperty("name", out var na2) ? na2.GetString() ?? "" : "";
                var committedAt = cj.TryGetProperty("commit", out var c3) && c3.TryGetProperty("created_at", out var m3) ? m3.GetDateTime() : DateTime.UtcNow;
                commits.Add(new CommitInfo
                {
                    Sha = latestSha!,
                    Message = msg,
                    Committer = authorName,
                    CommittedAt = committedAt
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

        var startTime = DateTime.UtcNow;
        var log = new ReviewScheduleLog
        {
            ScheduleId = scheduleId,
            RepositoryId = schedule.RepositoryId,
            BranchName = schedule.BranchName,
            TriggerAt = startTime,
            Status = 1,
            NewCommits = 0,
            Enqueued = 0
        };

        try
        {
            var commits = await GetUnreviewedCommitsAsync(schedule.RepositoryId, schedule.BranchName);
            log.NewCommits = commits.Count;

            foreach (var commit in commits)
            {
                await _reviewService.TriggerReviewAsync(
                    schedule.RepositoryId,
                    commit.Sha,
                    commit.Message,
                    commit.Committer,
                    commit.CommittedAt,
                    schedule.BranchName);
                log.Enqueued++;
                _logger.LogInformation("Schedule {ScheduleId} 入队：仓库 {RepoId} 分支 {Branch} 提交 {Sha}",
                    scheduleId, schedule.RepositoryId, schedule.BranchName, commit.Sha[..Math.Min(8, commit.Sha.Length)]);
            }
        }
        catch (Exception ex)
        {
            log.Status = 0;
            log.ErrorMessage = ex.Message;
            _logger.LogError(ex, "Schedule {ScheduleId} 执行异常", scheduleId);
        }
        finally
        {
            log.DurationSeconds = (int)(DateTime.UtcNow - startTime).TotalSeconds;
            await _db.Insertable(log).ExecuteCommandAsync();
        }
    }

    public async Task<List<ReviewScheduleLog>> GetLogsAsync(int scheduleId = 0, int limit = 50)
    {
        var q = _db.Queryable<ReviewScheduleLog>().Where(x => !x.IsDeleted);
        if (scheduleId > 0)
            q = q.Where(x => x.ScheduleId == scheduleId);
        var logs = await q.OrderByDescending(x => x.TriggerAt).Take(limit).ToListAsync();

        // 填充仓库名
        var repos = await _db.Queryable<Repository>().Where(x => !x.IsDeleted).ToListAsync();
        foreach (var log in logs)
            log.RepoName = repos.FirstOrDefault(r => r.Id == log.RepositoryId)?.RepoName;

        return logs;
    }
}

using CodeReview.Application.IService;
using CodeReview.Api.Hubs;
using CodeReview.Domain.Entities;
using Microsoft.AspNetCore.SignalR;
using SqlSugar;

namespace CodeReview.Api.Services;

/// <summary>
/// SignalR 实时通知服务实现
/// </summary>
public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ISqlSugarClient _db;

    public SignalRNotificationService(IHubContext<NotificationHub> hub, ISqlSugarClient db)
    {
        _hub = hub;
        _db = db;
    }

    public async Task BroadcastAsync(string title, string message, string type)
    {
        var payload = new { title, message, type, timestamp = DateTime.Now };
        await _hub.Clients.All.SendAsync("ReceiveNotification", payload);
    }

    public async Task NotifyReviewCompletedAsync(int repositoryId, int reviewCommitId, bool success, string? errorMsg = null)
    {
        // 查询仓库名和 commit 信息
        var repo = await _db.Queryable<Repository>()
            .Where(x => x.Id == repositoryId)
            .Select(x => new { x.RepoName, x.RepoUrl })
            .FirstAsync();
        var commit = await _db.Queryable<ReviewCommit>()
            .Where(x => x.Id == reviewCommitId)
            .Select(x => new { x.CommitSha, x.CommitMessage })
            .FirstAsync();

        string title;
        string message;
        if (success)
        {
            var issueCount = await _db.Queryable<ReviewResult>()
                .Where(x => x.ReviewCommitId == reviewCommitId)
                .CountAsync();
            var sha = commit?.CommitSha?.Length > 8 ? commit.CommitSha[..8] : commit?.CommitSha;
            title = "✅ 审核完成";
            message = issueCount > 0
                ? $"「{repo?.RepoName}」{sha} 发现了 {issueCount} 个问题"
                : $"「{repo?.RepoName}」{sha} 未发现问题，代码质量良好 👍";
        }
        else
        {
            title = "❌ 审核失败";
            message = $"「{repo?.RepoName}」审核失败：{errorMsg}";
        }

        var payload = new
        {
            title,
            message,
            type = success ? "success" : "error",
            timestamp = DateTime.Now,
            data = new { repositoryId, reviewCommitId, success, repoName = repo?.RepoName }
        };
        await _hub.Clients.All.SendAsync("ReceiveNotification", payload);
    }
}

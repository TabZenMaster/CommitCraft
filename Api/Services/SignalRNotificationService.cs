using CodeReview.Application.IService;
using Microsoft.AspNetCore.SignalR;
using CodeReview.Api.Hubs;

namespace CodeReview.Api.Services;

/// <summary>
/// SignalR 实时通知服务实现
/// </summary>
public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRNotificationService(IHubContext<NotificationHub> hub)
    {
        _hub = hub;
    }

    public async Task BroadcastAsync(string title, string message, string type)
    {
        var payload = new { title, message, type, timestamp = DateTime.Now };
        await _hub.Clients.All.SendAsync("ReceiveNotification", payload);
    }

    public async Task NotifyReviewCompletedAsync(int repositoryId, int reviewCommitId, bool success, string? errorMsg = null)
    {
        var payload = new
        {
            title = success ? "✅ 审核完成" : "❌ 审核失败",
            message = success
                ? $"任务 #{reviewCommitId} 审核已完成"
                : $"任务 #{reviewCommitId} 审核失败：{errorMsg}",
            type = success ? "success" : "error",
            timestamp = DateTime.Now,
            data = new { repositoryId, reviewCommitId, success }
        };
        await _hub.Clients.All.SendAsync("ReceiveNotification", payload);
    }
}

namespace CodeReview.Application.IService;

/// <summary>
/// 实时通知服务接口（由 Api 层 SignalR 实现）
/// </summary>
public interface INotificationService
{
    /// <summary>广播给所有在线用户</summary>
    Task BroadcastAsync(string title, string message, string type);

    /// <summary>通知任务状态变更</summary>
    Task NotifyReviewCompletedAsync(int repositoryId, int reviewCommitId, bool success, string? errorMsg = null);

    /// <summary>通知任务开始审核</summary>
    Task NotifyReviewStartedAsync(int repositoryId, int reviewCommitId);
}


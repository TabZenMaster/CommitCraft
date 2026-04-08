using Microsoft.AspNetCore.SignalR;

namespace CodeReview.Api.Hubs;

/// <summary>
/// 实时通知 Hub
/// 前端连接: /hubs/notifications?token=xxx
/// </summary>
public class NotificationHub : Hub
{
    /// <summary>
    /// 流式 AI 回答（客户端通过 SignalR 接收 token 推送）
    /// </summary>
    public async Task StreamAiAnswer(string streamId)
    {
        //streamId 对应客户端 WebSocket 连接 ID，服务端通过它定向推送
    }

    /// <summary>建立连接后记录 userId → connectionId 映射（支持定向推送）</summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}

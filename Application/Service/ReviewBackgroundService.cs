using CodeReview.Application.IService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeReview.Application.Service;

/// <summary>
/// 后台HostedService：持续从队列取任务并执行
/// </summary>
public class ReviewBackgroundService : BackgroundService
{
    private readonly ReviewQueueService _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReviewBackgroundService> _logger;

    public ReviewBackgroundService(
        ReviewQueueService queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ReviewBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReviewBackgroundService 已启动");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var taskId = await _queue.DequeueAsync(stoppingToken);
                _logger.LogInformation("取出审核任务 id={TaskId}，开始执行", taskId);

                // 每次创建新的 Scope，确保 DbContext 等 Scoped 服务生命周期正确
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IReviewService>();
                await service.ExecuteReviewAsync(taskId);

                _logger.LogInformation("审核任务 id={TaskId} 执行完成", taskId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReviewBackgroundService 执行异常");
            }
        }
        _logger.LogInformation("ReviewBackgroundService 已停止");
    }
}

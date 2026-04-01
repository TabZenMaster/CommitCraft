using CodeReview.Application.IService;
using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeReview.Application.Service;

/// <summary>
/// 后台服务：每分钟检查定时任务是否该触发
/// </summary>
public class ReviewSchedulerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReviewSchedulerHostedService> _logger;

    public ReviewSchedulerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ReviewSchedulerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReviewSchedulerHostedService 已启动");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;

                using var scope = _scopeFactory.CreateScope();
                var scheduleService = scope.ServiceProvider.GetRequiredService<IReviewScheduleService>();
                var schedules = await scheduleService.GetEnabledAsync();

                foreach (var schedule in schedules)
                {
                    if (!ShouldRunNow(schedule.CronExpr, now, schedule.LastTriggerAt))
                        continue;

                    _logger.LogInformation("Schedule {Id} 触发：仓库 {RepoId} 分支 {Branch}",
                        schedule.Id, schedule.RepositoryId, schedule.BranchName);

                    try
                    {
                        await scheduleService.TriggerScheduleAsync(schedule.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Schedule {Id} 执行异常", schedule.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReviewSchedulerHostedService 执行异常");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    /// <summary>
    /// 判断某个 cron 表达式在当前时间是否应该触发
    /// </summary>
    private bool ShouldRunNow(string cronExpr, DateTime now, DateTime? lastTriggerAt)
    {
        try
        {
            // Cronos 5字段 CronExpression 支持（分 时 日 月 周）
            // 我们的表达式是 6 字段（秒 分 时 日 月 周），取后5个字段用于匹配
            var parts = cronExpr.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5) return false;

            // 取 [1:] 跳过秒字段，用 分 时 日 月 周 构建 5 位 cron
            var cron5 = string.Join(" ", parts.Skip(1));
            var expr = CronExpression.Parse(cron5);

            var next = expr.GetNextOccurrence(now.AddMinutes(-1), TimeZoneInfo.Local);
            if (next == null) return false;

            // 如果计算出的下次触发时间在当前分钟范围内，认为该触发
            var diff = Math.Abs((next.Value - now).TotalMinutes);
            return diff < 1;
        }
        catch
        {
            return false;
        }
    }
}

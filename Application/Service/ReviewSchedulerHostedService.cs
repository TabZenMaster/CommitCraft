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
                var nowUtc = DateTime.UtcNow;

                using var scope = _scopeFactory.CreateScope();
                var scheduleService = scope.ServiceProvider.GetRequiredService<IReviewScheduleService>();
                var schedules = await scheduleService.GetEnabledAsync();

                foreach (var schedule in schedules)
                {
                    if (!ShouldRunNow(schedule.CronExpr, nowUtc, schedule.LastTriggerAt, schedule.Id))
                        continue;

                    try
                    {
                        await scheduleService.MarkTriggeredAsync(schedule.Id, nowUtc);
                        _ = scheduleService.TriggerScheduleAsync(schedule.Id);
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
    /// 判断某个 cron 表达式在当前时间是否应该触发。
    ///
    /// 前端 rebuildCron() 保证生成标准 6字段（秒 分 时 日 月 周），
    /// 这里直接 Parse(IncludeSeconds)，所见即所得。
    ///
    /// 核心逻辑：
    /// 1. 用 GetNextOccurrence(from=now-1min) 找"最近一次触发点"
    /// 2. 只在 diff=[0,1) 分钟内触发（即 cron 触发点后的第一分钟内）
    /// 3. DB 层防重：若 lastTriggerAt 已经记录了这个触发点（或更晚），跳过
    /// </summary>
    private bool ShouldRunNow(string cronExpr, DateTime nowUtc, DateTime? lastTriggerAtLocal, int scheduleId)
    {
        try
        {
            var parts = cronExpr.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 6) return false;

            var expr = CronExpression.Parse(cronExpr, CronFormat.IncludeSeconds);
            var fromUtc = nowUtc.AddMinutes(-1);
            // 用本地时区解析 cron 表达式（如 "0 30 9 * * *" = 每天09:30本地时间）
            var nextUtc = expr.GetNextOccurrence(fromUtc, TimeZoneInfo.Local);

            if (!nextUtc.HasValue)
            {
                _logger.LogWarning("[SchedulerCheck] id={0} cron={1} nextUtc=null", scheduleId, cronExpr);
                return false;
            }

            var diff = (nowUtc - nextUtc.Value).TotalMinutes;

            _logger.LogInformation(
                "[SchedulerCheck] id={0} cron={1} nowUtc={2:HH:mm:ss} next={3:HH:mm:ss} diff={4:F2} lastLocal={5}",
                scheduleId, cronExpr, nowUtc, nextUtc.Value, diff, lastTriggerAtLocal);

            if (diff < 0 || diff >= 1) return false;

            if (lastTriggerAtLocal.HasValue)
            {
                var lastUtc = TimeZoneInfo.ConvertTimeToUtc(lastTriggerAtLocal.Value, TimeZoneInfo.Local);
                if (lastUtc >= nextUtc.Value)
                {
                    _logger.LogInformation("[SchedulerCheck] id={0} DB防重 lastUtc={1} >= next={2}", scheduleId, lastUtc, nextUtc.Value);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ShouldRunNow 异常 cron={0}", cronExpr);
            return false;
        }
    }
}

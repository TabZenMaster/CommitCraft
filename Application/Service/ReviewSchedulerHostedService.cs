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
                    var (shouldRun, nextUtc) = ShouldRunNow(schedule.CronExpr, nowUtc, schedule.LastTriggerAt, schedule.Id);
                    if (!shouldRun || !nextUtc.HasValue)
                        continue;

                    try
                    {
                        // 存实际的触发时间点（nextUtc）而非检查时间（nowUtc），确保 DB 防重逻辑正确
                        await scheduleService.MarkTriggeredAsync(schedule.Id, nextUtc.Value);
                        // 必须 await，等 TriggerScheduleAsync 完成后再进行下一轮检查，
                        // 否则同一触发点可能被后续检查周期重复触发（DB 写入未提交时下一轮已读旧值）
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
    /// 判断某个 cron 表达式在当前时间是否应该触发。
    /// 返回 (shouldRun, nextUtc) 元组，nextUtc 为实际的触发时间点。
    ///
    /// 前端 rebuildCron() 保证生成标准 6字段（秒 分 时 日 月 周），
    /// 这里直接 Parse(IncludeSeconds)，所见即所得。
    ///
    /// 核心逻辑：
    /// 1. 用 GetNextOccurrence(from=now-2min) 找"最近一次触发点"，留 2 分钟宽窗口
    /// 2. 在 diff=[0,2) 分钟内触发（覆盖 60s 检查间隔导致的 ~1min 漂移）
    /// 3. DB 层防重：若 lastTriggerAt 已经记录了这个触发点（或更晚），跳过
    /// </summary>
    private (bool ShouldRun, DateTime? NextUtc) ShouldRunNow(string cronExpr, DateTime nowUtc, DateTime? lastTriggerAtLocal, int scheduleId)
    {
        try
        {
            var parts = cronExpr.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 6) return (false, null);

            var expr = CronExpression.Parse(cronExpr, CronFormat.IncludeSeconds);
            // 用 now-2min 作为基准，留出 2 分钟窗口覆盖检查间隔 60s 带来的漂移
            var fromUtc = nowUtc.AddMinutes(-2);
            // 显式使用中国时区，避免跨平台 TimeZoneInfo.Local 行为不一致
            var chinaZone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
            var nextUtc = expr.GetNextOccurrence(fromUtc, chinaZone);

            if (!nextUtc.HasValue)
            {
                _logger.LogWarning("[SchedulerCheck] id={0} cron={1} nextUtc=null", scheduleId, cronExpr);
                return (false, null);
            }

            var diff = (nowUtc - nextUtc.Value).TotalMinutes;

            _logger.LogInformation(
                "[SchedulerCheck] id={0} cron={1} nowUtc={2:HH:mm:ss} next={3:HH:mm:ss} diff={4:F2} lastLocal={5}",
                scheduleId, cronExpr, nowUtc, nextUtc.Value, diff, lastTriggerAtLocal);

            // diff >= 0：触发时间已到（不能再是未来）
            // diff < 2：2 分钟宽窗口，覆盖 60s 检查间隔的最大 ~1min 漂移
            if (diff < 0 || diff >= 2) return (false, null);

            if (lastTriggerAtLocal.HasValue)
            {
                var lastUtc = TimeZoneInfo.ConvertTimeToUtc(lastTriggerAtLocal.Value, TimeZoneInfo.Local);
                if (lastUtc >= nextUtc.Value)
                {
                    _logger.LogInformation("[SchedulerCheck] id={0} DB防重 lastUtc={1} >= next={2}", scheduleId, lastUtc, nextUtc.Value);
                    return (false, null);
                }
            }

            return (true, nextUtc.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ShouldRunNow 异常 cron={0}", cronExpr);
            return (false, null);
        }
    }
}

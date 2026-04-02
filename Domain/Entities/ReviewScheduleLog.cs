using SqlSugar;

namespace CodeReview.Domain.Entities;

/// <summary>
/// 定时审核计划执行日志
/// </summary>
[SugarTable("cr_review_schedule_log")]
public class ReviewScheduleLog : BaseEntity
{
    /// <summary>计划ID</summary>
    public int ScheduleId { get; set; }

    /// <summary>仓库ID</summary>
    public int RepositoryId { get; set; }

    /// <summary>分支名（多个用逗号分隔）</summary>
    public string BranchName { get; set; } = "";

    /// <summary>触发时间</summary>
    public DateTime TriggerAt { get; set; }

    /// <summary>耗时（秒）</summary>
    public int DurationSeconds { get; set; }

    /// <summary>状态：0=失败 1=成功</summary>
    public int Status { get; set; }

    /// <summary>发现的新提交数</summary>
    public int NewCommits { get; set; }

    /// <summary>入队任务数</summary>
    public int Enqueued { get; set; }

    /// <summary>失败原因</summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? ErrorMessage { get; set; }

    /// <summary>执行详情（JSON）</summary>
    [SugarColumn(IsNullable = true, ColumnDataType = "text")]
    public string? Detail { get; set; }

    // 非数据库字段
    [SugarColumn(IsIgnore = true)]
    public string? RepoName { get; set; }

    [SugarColumn(IsIgnore = true)]
    public string? StatusText => Status == 1 ? "成功" : "失败";
}

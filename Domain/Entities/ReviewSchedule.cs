using SqlSugar;

namespace CodeReview.Domain.Entities;

/// <summary>
/// 定时审核计划
/// </summary>
[SugarTable("cr_review_schedule")]
public class ReviewSchedule : BaseEntity
{
    /// <summary>仓库ID</summary>
    public int RepositoryId { get; set; }

    /// <summary>分支名</summary>
    public string BranchName { get; set; } = "master";

    /// <summary>Cron 表达式（6位：秒 分 时 日 月 周）</summary>
    public string CronExpr { get; set; } = "0 9 * * *";

    /// <summary>是否启用</summary>
    public int Enabled { get; set; } = 1;

    /// <summary>最后触发时间</summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? LastTriggerAt { get; set; }

    /// <summary>下次触发时间（计算得出）</summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? NextTriggerAt { get; set; }

    // 非数据库字段
    [SugarColumn(IsIgnore = true)]
    public string? RepoName { get; set; }
}

/// <summary>
/// 提交信息（扫描分支时使用）
/// </summary>
public class CommitInfo
{
    public string Sha { get; set; } = "";
    public string Message { get; set; } = "";
    public string Committer { get; set; } = "";
    public DateTime CommittedAt { get; set; }
}

using SqlSugar;

namespace CodeReview.Domain.Entities;

public abstract class BaseEntity
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.Now;

    public bool IsDeleted { get; set; } = false;
}

/// <summary>
/// AI 模型配置
/// </summary>
[SugarTable("cr_model_config")]
public class ModelConfig : BaseEntity
{
    public string Name { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ApiBase { get; set; } = "";
    public string? Description { get; set; } = "";
    /// <summary>1=启用 0=停用</summary>
    public int Status { get; set; } = 1;
}

/// <summary>
/// 代码仓库配置
/// </summary>
[SugarTable("cr_repository")]
public class Repository : BaseEntity
{
    /// <summary>仓库名称（从平台API获取的显示名）</summary>
    public string RepoName { get; set; } = "";
    /// <summary>组织/用户名</summary>
    public string Owner { get; set; } = "";
    /// <summary>仓库路径 slug（API调用用）</summary>
    public string RepoPath { get; set; } = "";
    /// <summary>仓库地址，如 https://gitee.com/jnrobot/fms-backend</summary>
    public string RepoUrl { get; set; } = "";
    /// <summary>该仓库的 Access Token（加密存储）</summary>
    public string GiteeToken { get; set; } = "";
    /// <summary>绑定的 AI 模型 ID</summary>
    public int ModelConfigId { get; set; }
    /// <summary>Webhook 密钥</summary>
    public string? WebhookSecret { get; set; } = "";
    /// <summary>1=启用 0=停用</summary>
    public int Status { get; set; } = 1;
    /// <summary>最后审核时间</summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? LastReviewAt { get; set; }

    // 非数据库字段
    [SugarColumn(IsIgnore = true)]
    public string? ModelName { get; set; }
}

/// <summary>
/// 审核任务（一次 Commit 一次审核）
/// </summary>
[SugarTable("cr_review_commit")]
public class ReviewCommit : BaseEntity
{
    public int RepositoryId { get; set; }
    /// <summary>Commit SHA</summary>
    public string CommitSha { get; set; } = "";
    /// <summary>Commit 信息</summary>
    public string CommitMessage { get; set; } = "";
    /// <summary>提交人</summary>
    public string Committer { get; set; } = "";
    /// <summary>提交时间</summary>
    public DateTime CommittedAt { get; set; }
    /// <summary>分支名</summary>
    public string BranchName { get; set; } = "";
    /// <summary>审核时间</summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? ReviewedAt { get; set; }
    /// <summary>0=待审核 1=审核中 2=已完成 3=失败</summary>
    public int Status { get; set; } = 0;
    /// <summary>失败原因</summary>
    public string? ErrorMsg { get; set; } = "";

    // 非数据库字段
    [SugarColumn(IsIgnore = true)]
    public string? RepoName { get; set; }
    [SugarColumn(IsIgnore = true)]
    public int ResultCount { get; set; }
    [SugarColumn(IsIgnore = true)]
    public int CriticalCount { get; set; }
}

/// <summary>
/// 审核结果条目
/// </summary>
[SugarTable("cr_review_result")]
public class ReviewResult : BaseEntity
{
    public int ReviewCommitId { get; set; }
    public int RepositoryId { get; set; }
    /// <summary>Commit SHA</summary>
    public string CommitSha { get; set; } = "";
    /// <summary>涉及文件</summary>
    public string FilePath { get; set; } = "";
    /// <summary>起始行</summary>
    public int? LineStart { get; set; }
    /// <summary>结束行</summary>
    public int? LineEnd { get; set; }
    /// <summary>问题类型: security/correctness/performance/maintainability/best_practice/code_style/other</summary>
    public string IssueType { get; set; } = "";
    /// <summary>严重程度: critical/major/minor/suggestion</summary>
    public string Severity { get; set; } = "";
    /// <summary>问题描述</summary>
    public string Description { get; set; } = "";
    /// <summary>修复建议</summary>
    public string Suggestion { get; set; } = "";
    /// <summary>相关代码片段（Diff 内容）</summary>
    [SugarColumn(ColumnDataType = "LONGTEXT")]
    public string? DiffContent { get; set; } = "";
    /// <summary>0=待处理 1=已认领 2=已修复 3=已忽略</summary>
    public int Status { get; set; } = 0;
    /// <summary>处理人用户ID</summary>
    [SugarColumn(IsNullable = true)]
    public int? HandlerUserId { get; set; }
    /// <summary>处理人姓名（不映射DB，通过JOIN计算）</summary>
    [SugarColumn(IsIgnore = true)]
    public string? HandlerName { get; set; }
    /// <summary>处理时间</summary>
    [SugarColumn(IsNullable = true)]
    public DateTime? HandledAt { get; set; }
    /// <summary>处理备注</summary>
    public string? HandlerMemo { get; set; } = "";

    // 非数据库字段
    [SugarColumn(IsIgnore = true)]
    public string? RepoName { get; set; }
    [SugarColumn(IsIgnore = true)]
    public string? CommitMessage { get; set; }
}

/// <summary>
/// 处理日志
/// </summary>
[SugarTable("cr_handler_log")]
public class HandlerLog : BaseEntity
{
    public int ReviewResultId { get; set; }
    public int OperatorId { get; set; }
    [SugarColumn(IsNullable = true)]
    public int? OperatorUserId { get; set; }
    /// <summary>操作类型: claim/fix/ignore</summary>
    public string Action { get; set; } = "";
    /// <summary>原状态</summary>
    public int FromStatus { get; set; }
    /// <summary>新状态</summary>
    public int ToStatus { get; set; }
    /// <summary>备注</summary>
    public string? Remark { get; set; }
}

/// <summary>
/// 系统用户
/// </summary>
[SugarTable("sys_user")]
public class SysUser : BaseEntity
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string RealName { get; set; } = "";
    /// <summary>admin/reviewer/developer</summary>
    public string Role { get; set; } = "developer";
    /// <summary>1=启用 0=停用</summary>
    public int Status { get; set; } = 1;
}

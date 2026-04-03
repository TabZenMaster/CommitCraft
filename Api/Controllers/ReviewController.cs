using CodeReview.Application;
using CodeReview.Application.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CodeReview.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReviewController : ControllerBase
{
    private readonly IReviewService _service;

    public ReviewController(IReviewService service) => _service = service;

    /// <summary>审核任务列表</summary>
    [HttpGet("tasks")]
    public async Task<Result<object>> GetTasks([FromQuery] int repositoryId = 0) =>
        Result<object>.Ok(await _service.GetTaskListAsync(repositoryId));

    /// <summary>单次任务详情</summary>
    [HttpGet("task/{id}")]
    public async Task<Result<object>> GetTask(int id) =>
        Result<object>.Ok(await _service.GetTaskByIdAsync(id));

    /// <summary>手动触发审核</summary>
    [Authorize(Roles = "admin,reviewer")]
    [HttpPost("trigger")]
    public async Task<Result> TriggerReview([FromBody] TriggerReviewDto dto) =>
        await _service.TriggerReviewAsync(dto.RepositoryId, dto.CommitSha, dto.CommitMessage, dto.Committer, dto.CommittedAt, dto.BranchName);

    /// <summary>获取审核结果列表（分页）</summary>
    [HttpGet("results")]
    public async Task<Result<object>> GetResults(
        [FromQuery] int reviewCommitId = 0,
        [FromQuery] int repositoryId = 0,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? severity = null,
        [FromQuery] int? status = null) =>
        Result<object>.Ok(await _service.GetResultsPageAsync(reviewCommitId, repositoryId, pageIndex, pageSize, severity, status));

    /// <summary>认领问题</summary>
    [Authorize(Roles = "admin,reviewer,developer")]
    [HttpPost("claim")]
    public async Task<Result> ClaimIssue([FromBody] IdDto dto)
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var name = User.FindFirst("realName")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "";
        if (!int.TryParse(idStr, out var id)) return Result.Fail("无效 Token");
        return await _service.ClaimIssueAsync(dto.Id, id, name);
    }

    /// <summary>处理问题（修复/忽略）</summary>
    [Authorize(Roles = "admin,reviewer,developer")]
    [HttpPost("handle")]
    public async Task<Result> HandleIssue([FromBody] HandleDto dto)
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var name = User.FindFirst("realName")?.Value ?? User.FindFirst(ClaimTypes.Name)?.Value ?? "";
        if (!int.TryParse(idStr, out var id)) return Result.Fail("无效 Token");
        return await _service.HandleIssueAsync(dto.Id, id, name, dto.Status, dto.Memo);
    }

    /// <summary>重试失败任务</summary>
    [Authorize(Roles = "admin,reviewer")]
    [HttpPost("retry/{id}")]
    public async Task<Result> RetryReview(int id) =>
        await _service.RetryReviewAsync(id);

    /// <summary>问题统计</summary>
    [HttpGet("statistics")]
    public async Task<Result<object>> GetStatistics([FromQuery] int repositoryId = 0) =>
        await _service.GetIssueStatisticsAsync(repositoryId);

    /// <summary>仪表盘：7天趋势</summary>
    [HttpGet("trend")]
    public async Task<Result<object>> GetTrend([FromQuery] int repositoryId = 0) =>
        await _service.GetTrendAsync(repositoryId);

    /// <summary>仪表盘：仓库排名</summary>
    [HttpGet("repo-ranking")]
    public async Task<Result<object>> GetRepoRanking() =>
        await _service.GetRepoRankingAsync();

    /// <summary>仪表盘：最近任务</summary>
    [HttpGet("recent-tasks")]
    public async Task<Result<object>> GetRecentTasks([FromQuery] int limit = 10) =>
        await _service.GetRecentTasksAsync(limit);

    /// <summary>仪表盘：处理效率</summary>
    [HttpGet("handling-stats")]
    public async Task<Result<object>> GetHandlingStats() =>
        await _service.GetHandlingStatsAsync();
}

public class TriggerReviewDto
{
    public int RepositoryId { get; set; }
    public string CommitSha { get; set; } = "";
    public string CommitMessage { get; set; } = "";
    public string Committer { get; set; } = "";
    public DateTime CommittedAt { get; set; }
    /// <summary>分支名</summary>
    public string BranchName { get; set; } = "";
}

public class HandleDto
{
    public int Id { get; set; }
    /// <summary>2=已修复 3=已忽略</summary>
    public int Status { get; set; }
    public string? Memo { get; set; }
}

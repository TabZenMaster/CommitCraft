using CodeReview.Application;
using CodeReview.Application.IService;
using CodeReview.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeReview.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ScheduleController : ControllerBase
{
    private readonly IReviewScheduleService _service;
    private readonly IRepositoryService _repoService;

    public ScheduleController(IReviewScheduleService service, IRepositoryService repoService)
    {
        _service = service;
        _repoService = repoService;
    }

    [HttpGet("list")]
    public async Task<Result<object>> GetList()
    {
        var schedules = await _service.GetAllAsync();
        // 填充仓库名
        var repos = await _repoService.GetListAsync();
        foreach (var s in schedules)
            s.RepoName = repos.FirstOrDefault(r => r.Id == s.RepositoryId)?.RepoName;
        return Result<object>.Ok(schedules);
    }

    [HttpPost("add")]
    public async Task<Result> Add([FromBody] AddScheduleDto dto)
    {
        if (dto.RepositoryId <= 0) return Result.Fail("请选择仓库");
        if (string.IsNullOrWhiteSpace(dto.BranchName)) return Result.Fail("请填写分支名");
        if (string.IsNullOrWhiteSpace(dto.CronExpr)) return Result.Fail("请填写 Cron 表达式");
        var id = await _service.CreateAsync(dto.RepositoryId, dto.BranchName, dto.CronExpr);
        return id > 0 ? Result.Ok("添加成功") : Result.Fail("添加失败");
    }

    [HttpPost("update")]
    public async Task<Result> Update([FromBody] UpdateScheduleDto dto)
    {
        await _service.UpdateAsync(dto.Id, dto.BranchName, dto.CronExpr, dto.Enabled);
        return Result.Ok("更新成功");
    }

    [HttpPost("delete")]
    public async Task<Result> Delete([FromBody] IdDto dto)
    {
        await _service.DeleteAsync(dto.Id);
        return Result.Ok("删除成功");
    }

    /// <summary>
    /// 手动触发一次扫描（测试用）
    /// </summary>
    [HttpPost("trigger")]
    public async Task<Result> Trigger([FromBody] IdDto dto)
    {
        await _service.TriggerScheduleAsync(dto.Id);
        return Result.Ok("触发成功，请查看审核任务列表");
    }

    /// <summary>
    /// 执行日志列表
    /// </summary>
    [HttpGet("logs")]
    public async Task<Result<object>> GetLogs([FromQuery] int scheduleId = 0, [FromQuery] int limit = 50)
    {
        var logs = await _service.GetLogsAsync(scheduleId, limit);
        return Result<object>.Ok(logs);
    }
}

public class AddScheduleDto
{
    public int RepositoryId { get; set; }
    public string BranchName { get; set; } = "master";
    public string CronExpr { get; set; } = "0 9 * * *";
}

public class UpdateScheduleDto
{
    public int Id { get; set; }
    public string BranchName { get; set; } = "master";
    public string CronExpr { get; set; } = "0 9 * * *";
    public int Enabled { get; set; } = 1;
}

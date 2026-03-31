using CodeReview.Application;
using CodeReview.Application.IService;
using CodeReview.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeReview.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RepositoryController : ControllerBase
{
    private readonly IRepositoryService _service;

    public RepositoryController(IRepositoryService service) => _service = service;

    [HttpGet("list")]
    public async Task<Result<object>> GetList() =>
        Result<object>.Ok(await _service.GetListAsync());

    [HttpGet("{id}")]
    public async Task<Result<object>> GetById(int id) =>
        Result<object>.Ok(await _service.GetByIdAsync(id));

    [Authorize(Roles = "admin")]
    [HttpGet("{id}/plain-token")]
    public async Task<Result<object>> GetPlainToken(int id) =>
        Result<object>.Ok(await _service.GetPlainGiteeTokenAsync(id));

    [Authorize(Roles = "admin")]
    [HttpPost("add")]
    public async Task<Result> Add([FromBody] Repository model) =>
        await _service.AddAsync(model) ? Result.Ok("添加成功") : Result.Fail("添加失败");

    [Authorize(Roles = "admin")]
    [HttpPost("update")]
    public async Task<Result> Update([FromBody] Repository model) =>
        await _service.UpdateAsync(model) ? Result.Ok("更新成功") : Result.Fail("更新失败");

    [Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<Result> Delete(int id) =>
        await _service.DeleteAsync(id) ? Result.Ok("删除成功") : Result.Fail("删除失败");

    [Authorize(Roles = "admin,reviewer")]
    [HttpPost("test")]
    public async Task<Result<object>> TestConnection([FromBody] IdDto dto) =>
        await _service.TestConnectionAsync(dto.Id);

    /// <summary>从 Gitee 拉取仓库提交列表</summary>
    [Authorize(Roles = "admin,reviewer")]
    [HttpGet("{id}/commits")]
    public async Task<Result<object>> GetCommits(int id, [FromQuery] string? branch = null, [FromQuery] int page = 1, [FromQuery] int perPage = 20) =>
        await _service.GetCommitsAsync(id, branch, page, perPage);

    /// <summary>从 Gitee 刷新仓库名称</summary>
    [Authorize(Roles = "admin")]
    [HttpPost("{id}/refresh")]
    public async Task<Result<object>> RefreshRepoName(int id) =>
        await _service.RefreshRepoNameAsync(id);

    /// <summary>根据仓库地址查询仓库名称（新建时用）</summary>
    [Authorize(Roles = "admin")]
    [HttpPost("fetch")]
    public async Task<Result<object>> FetchRepoName([FromBody] FetchRepoDto dto) =>
        await _service.FetchRepoNameAsync(dto.repoId, dto.repoUrl);

    /// <summary>获取仓库分支列表</summary>
    [Authorize(Roles = "admin,reviewer")]
    [HttpGet("{id}/branches")]
    public async Task<Result<object>> GetBranches(int id) =>
        await _service.GetBranchesAsync(id);
}

public class FetchRepoDto
{
    public string? repoUrl { get; set; }
    public int repoId { get; set; }
}

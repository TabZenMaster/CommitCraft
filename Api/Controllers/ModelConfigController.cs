using CodeReview.Application;
using CodeReview.Application.IService;
using CodeReview.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeReview.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ModelConfigController : ControllerBase
{
    private readonly IModelConfigService _service;

    public ModelConfigController(IModelConfigService service) => _service = service;

    [HttpGet("list")]
    public async Task<Result<object>> GetList() =>
        Result<object>.Ok(await _service.GetListAsync());

    [HttpGet("{id}")]
    public async Task<Result<object>> GetById(int id) =>
        Result<object>.Ok(await _service.GetByIdAsync(id));

    [Authorize(Roles = "admin")]
    [HttpPost("add")]
    public async Task<Result> Add([FromBody] ModelConfig model) =>
        await _service.AddAsync(model) ? Result.Ok("添加成功") : Result.Fail("添加失败");

    [Authorize(Roles = "admin")]
    [HttpPost("update")]
    public async Task<Result> Update([FromBody] ModelConfig model) =>
        await _service.UpdateAsync(model) ? Result.Ok("更新成功") : Result.Fail("更新失败");

    [Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<Result> Delete(int id) =>
        await _service.DeleteAsync(id) ? Result.Ok("删除成功") : Result.Fail("删除失败");

    [Authorize(Roles = "admin")]
    [HttpPost("test")]
    public async Task<Result<object>> TestConnection([FromBody] IdDto dto) =>
        await _service.TestConnectionAsync(dto.Id);
}

public class IdDto { public int Id { get; set; } }

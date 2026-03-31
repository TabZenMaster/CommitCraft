using CodeReview.Application;
using CodeReview.Application.IService;
using CodeReview.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeReview.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SysUserController : ControllerBase
{
    private readonly ISysUserService _service;

    public SysUserController(ISysUserService service) => _service = service;

    [Authorize(Roles = "admin")]
    [HttpGet("list")]
    public async Task<Result<object>> GetList() =>
        Result<object>.Ok(await _service.GetListAsync());

    [Authorize(Roles = "admin")]
    [HttpPost("add")]
    public async Task<Result> Add([FromBody] SysUser model) =>
        await _service.AddAsync(model) ? Result.Ok("添加成功") : Result.Fail("添加失败");

    [Authorize(Roles = "admin")]
    [HttpPost("update")]
    public async Task<Result> Update([FromBody] SysUser model) =>
        await _service.UpdateAsync(model) ? Result.Ok("更新成功") : Result.Fail("更新失败");

    [Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<Result> Delete(int id) =>
        await _service.DeleteAsync(id) ? Result.Ok("删除成功") : Result.Fail("删除失败");
}

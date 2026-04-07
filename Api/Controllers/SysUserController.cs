using CodeReview.Application;
using CodeReview.Application.IService;
using CodeReview.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CodeReview.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SysUserController : ControllerBase
{
    public record ResetPasswordDto(int id, string newPassword);
    public record ChangePasswordDto(string oldPwd, string newPwd);
    public record UpdateUserDto(int id, string? realName, string? gitName);

    private readonly ISysUserService _service;

    public SysUserController(ISysUserService service) => _service = service;

    [HttpGet("me")]
    public async Task<Result<object>> GetMe()
    {
        var idClaim = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier);
        if (idClaim == null) return Result<object>.Fail("未找到用户信息");
        var id = int.Parse(idClaim.Value);
        var user = await _service.GetByIdAsync(id);
        return user != null ? Result<object>.Ok(user) : Result<object>.Fail("用户不存在");
    }

    [Authorize(Roles = "admin")]
    [HttpGet("list")]
    public async Task<Result<object>> GetList() =>
        Result<object>.Ok(await _service.GetListAsync());

    [Authorize(Roles = "admin")]
    [HttpPost("add")]
    public async Task<Result> Add([FromBody] SysUser model) =>
        await _service.AddAsync(model) ? Result.Ok("添加成功") : Result.Fail("添加失败");

    [HttpPost("update")]
    public async Task<Result> Update([FromBody] UpdateUserDto dto)
    {
        var id = int.Parse(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (dto.id != id) return Result.Fail("只能修改自己的信息");
        var user = await _service.GetByIdAsync(dto.id);
        if (user == null) return Result.Fail("用户不存在");
        user.RealName = dto.realName ?? user.RealName;
        user.GitName = dto.gitName;
        return await _service.UpdateAsync(user) ? Result.Ok("更新成功") : Result.Fail("更新失败");
    }

    [Authorize(Roles = "admin")]
    [HttpDelete("{id}")]
    public async Task<Result> Delete(int id) =>
        await _service.DeleteAsync(id) ? Result.Ok("删除成功") : Result.Fail("删除失败");

    [Authorize(Roles = "admin")]
    [HttpPost("reset-password")]
    public async Task<Result> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.newPassword) || dto.newPassword.Length < 6)
            return Result.Fail("密码长度不能少于6位");
        return await _service.ResetPasswordAsync(dto.id, dto.newPassword)
            ? Result.Ok("密码重置成功") : Result.Fail("重置失败");
    }

    [HttpPost("change-password")]
    public async Task<Result<object>> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var id = int.Parse(User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? "0");
        if (string.IsNullOrWhiteSpace(dto.newPwd) || dto.newPwd.Length < 6)
            return Result<object>.Fail("新密码长度不能少于6位");
        var ok = await _service.ChangePasswordAsync(id, dto.oldPwd, dto.newPwd);
        return ok ? Result<object>.Ok(new { needRelogin = true }, "密码修改成功，请重新登录") : Result<object>.Fail("原密码错误");
    }
}

using CodeReview.Application;
using CodeReview.Application.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CodeReview.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ISysUserService _userService;

    public AuthController(ISysUserService userService) => _userService = userService;

    [HttpPost("login")]
    public async Task<Result<object>> Login([FromBody] LoginDto dto)
    {
        var user = await _userService.ValidateAsync(dto.Username, dto.Password);
        if (user == null)
            return Result<object>.Fail("用户名或密码错误");

        var token = GenerateJwt(user.Id, user.Username, user.RealName, user.Role);
        return Result<object>.Ok(new
        {
            token,
            userId = user.Id,
            username = user.Username,
            realName = user.RealName,
            role = user.Role,
            status = user.Status,
            gitName = user.GitName
        });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<Result<object>> GetCurrentUser()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idStr, out var id))
            return Result<object>.Fail("无效 Token");
        var user = await _userService.GetByIdAsync(id);
        if (user == null) return Result<object>.Fail("用户不存在");
        return Result<object>.Ok(new { user.Id, user.Username, user.RealName, user.Role });
    }

    private string GenerateJwt(int id, string username, string realName, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("CodeReview_Secret_Key_2026_Must_Be_32_Chars!"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role),
            new Claim("realName", realName)
        };
        var token = new JwtSecurityToken(
            issuer: "CodeReview",
            audience: "CodeReview",
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LoginDto
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

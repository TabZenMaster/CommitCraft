using CodeReview.Domain.Entities;

namespace CodeReview.Application.IService;

public interface ISysUserService
{
    Task<SysUser?> GetByIdAsync(int id);
    Task<SysUser?> ValidateAsync(string username, string password);
    Task<List<SysUser>> GetListAsync();
    Task<bool> AddAsync(SysUser model);
    Task<bool> UpdateAsync(SysUser model);
    Task<bool> DeleteAsync(int id);
    Task<bool> ResetPasswordAsync(int id, string newPassword);
    Task<bool> ChangePasswordAsync(int id, string oldPassword, string newPassword);
}

using CodeReview.Application.IService;
using CodeReview.Domain.Entities;
using SqlSugar;

namespace CodeReview.Application.Service;

public class SysUserService : ISysUserService
{
    private readonly ISqlSugarClient _db;

    public SysUserService(ISqlSugarClient db) => _db = db;

    public async Task<SysUser?> GetByIdAsync(int id) =>
        await _db.Queryable<SysUser>().Where(x => x.Id == id && !x.IsDeleted).FirstAsync();

    public async Task<SysUser?> ValidateAsync(string username, string password)
    {
        var user = await _db.Queryable<SysUser>()
            .Where(x => x.Username == username && !x.IsDeleted && x.Status == 1)
            .FirstAsync();
        if (user == null) return null;
        if (!BCrypt.Net.BCrypt.Verify(password, user.Password))
            return null;
        return user;
    }

    public async Task<List<SysUser>> GetListAsync() =>
        await _db.Queryable<SysUser>().Where(x => !x.IsDeleted).OrderBy(x => x.CreateTime, OrderByType.Desc).ToListAsync();

    public async Task<bool> AddAsync(SysUser model)
    {
        model.CreateTime = DateTime.Now;
        model.IsDeleted = false;
        model.Password = BCrypt.Net.BCrypt.HashPassword(model.Password);
        return await _db.Insertable(model).ExecuteCommandAsync() > 0;
    }

    public async Task<bool> UpdateAsync(SysUser model)
    {
        model.CreateTime = DateTime.Now;
        return await _db.Updateable(model).ExecuteCommandAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int id) =>
        await _db.Updateable<SysUser>()
            .SetColumns(x => x.IsDeleted == true)
            .Where(x => x.Id == id)
            .ExecuteCommandAsync() > 0;
}

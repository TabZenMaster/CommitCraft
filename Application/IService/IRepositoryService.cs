using CodeReview.Domain.Entities;

namespace CodeReview.Application.IService;

public interface IRepositoryService
{
    Task<List<Repository>> GetListAsync();
    Task<Repository?> GetByIdAsync(int id);
    Task<bool> AddAsync(Repository model);
    Task<bool> UpdateAsync(Repository model);
    Task<bool> DeleteAsync(int id);
    /// <summary>测试 Gitee 仓库连通性</summary>
    Task<Result<object>> TestConnectionAsync(int repoId);
    /// <summary>从 Gitee 拉取仓库提交列表</summary>
    Task<Result<object>> GetCommitsAsync(int repoId, string? branch = null, int page = 1, int perPage = 20);
    /// <summary>从 Gitee 刷新仓库名称</summary>
    Task<Result<object>> RefreshRepoNameAsync(int repoId);
    /// <summary>根据仓库地址查询仓库名称（无需 ID）</summary>
    Task<Result<object>> FetchRepoNameAsync(int repoId, string? overrideUrl = null);
    /// <summary>获取解密后的 Gitee Token（编辑弹窗用）</summary>
    Task<string?> GetPlainGiteeTokenAsync(int repoId);
    /// <summary>获取仓库分支列表</summary>
    Task<Result<object>> GetBranchesAsync(int repoId);
}

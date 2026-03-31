using CodeReview.Domain.Entities;

namespace CodeReview.Application.IService;

public interface IModelConfigService
{
    Task<List<ModelConfig>> GetListAsync();
    Task<ModelConfig?> GetByIdAsync(int id);
    Task<bool> AddAsync(ModelConfig model);
    Task<bool> UpdateAsync(ModelConfig model);
    Task<bool> DeleteAsync(int id);
    /// <summary>测试模型连通性</summary>
    Task<Result<object>> TestConnectionAsync(int modelConfigId);
}

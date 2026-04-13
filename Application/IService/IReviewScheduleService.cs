using CodeReview.Domain.Entities;

namespace CodeReview.Application.IService;

public interface IReviewScheduleService
{
    Task<List<ReviewSchedule>> GetAllAsync();
    Task<List<ReviewSchedule>> GetEnabledAsync();
    Task<ReviewSchedule?> GetByIdAsync(int id);
    Task<bool> ExistsAsync(int repositoryId, string branchName, int? excludeId = null);
    Task<int> CreateAsync(int repositoryId, string branchName, string cronExpr);
    Task UpdateAsync(int id, string branchName, string cronExpr, int enabled);
    Task DeleteAsync(int id);
    Task MarkTriggeredAsync(int scheduleId, DateTime triggerTime);
    Task<List<CommitInfo>> GetUnreviewedCommitsAsync(int repositoryId, string branchName);
    Task TriggerScheduleAsync(int scheduleId);
    Task<List<ReviewScheduleLog>> GetLogsAsync(int scheduleId = 0, int limit = 50);
}

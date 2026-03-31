using CodeReview.Application.IService;
using CodeReview.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.Net.Http;
using System.Text;

namespace CodeReview.Application.Service;

public class ReviewService : IReviewService
{
    private readonly ISqlSugarClient _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ReviewQueueService _queue;

    public ReviewService(ISqlSugarClient db, IHttpClientFactory httpClientFactory, ReviewQueueService queue)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _queue = queue;
    }

    /// <summary>确保 API 地址以 /v1 结尾，自动补全</summary>
    private static string FixApiBase(string baseUrl)
    {
        baseUrl = baseUrl.TrimEnd('/');
        if (!baseUrl.EndsWith("/v1")) baseUrl += "/v1";
        return baseUrl;
    }

    // ========== 任务管理 ==========

    public async Task<List<ReviewCommit>> GetTaskListAsync(int repositoryId = 0)
    {
        var query = _db.Queryable<ReviewCommit>().Where(x => !x.IsDeleted);
        if (repositoryId > 0)
            query = query.Where(x => x.RepositoryId == repositoryId);

        var tasks = query.OrderBy(x => x.CreateTime, OrderByType.Desc).ToList();

        if (tasks.Any())
        {
            var repoIds = tasks.Select(t => t.RepositoryId).Distinct().ToList();
            var repoMap = (await _db.Queryable<Repository>()
                .Where(x => !x.IsDeleted && repoIds.Contains(x.Id))
                .Select(x => new { x.Id, x.RepoName })
                .ToListAsync())
                .ToDictionary(x => x.Id, x => x.RepoName);

            foreach (var t in tasks)
            {
                t.RepoName = repoMap.GetValueOrDefault(t.RepositoryId);
                t.ResultCount = await _db.Queryable<ReviewResult>()
                    .Where(x => !x.IsDeleted && x.ReviewCommitId == t.Id)
                    .CountAsync();
                t.CriticalCount = await _db.Queryable<ReviewResult>()
                    .Where(x => !x.IsDeleted && x.ReviewCommitId == t.Id && x.Severity == "critical")
                    .CountAsync();
            }
        }
        return tasks;
    }

    public async Task<ReviewCommit?> GetTaskByIdAsync(int id) =>
        await _db.Queryable<ReviewCommit>().Where(x => x.Id == id && !x.IsDeleted).FirstAsync();

    // ========== 触发审核 ==========

    public async Task<Result> TriggerReviewAsync(int repositoryId, string commitSha,
        string commitMessage, string committer, DateTime committedAt, string branchName)
    {
        var repo = await _db.Queryable<Repository>()
            .Where(x => x.Id == repositoryId && !x.IsDeleted && x.Status == 1)
            .FirstAsync();
        if (repo == null)
            return Result.Fail("仓库不存在或已停用");

        // 相同 Commit SHA 已审核完成，直接拦截不重复审核
        var exists = await _db.Queryable<ReviewCommit>()
            .Where(x => !x.IsDeleted && x.RepositoryId == repositoryId
                && x.CommitSha == commitSha && x.Status == 2)
            .FirstAsync();
        if (exists != null)
            return Result.Fail($"该 Commit 已审核完成（SHA: {commitSha[..8]}），请勿重复提交");

        // 新建审核任务
        var task = new ReviewCommit
        {
            RepositoryId = repositoryId,
            CommitSha = commitSha,
            CommitMessage = commitMessage,
            Committer = committer,
            CommittedAt = committedAt,
            BranchName = branchName,
            Status = 0,
            CreateTime = DateTime.Now,
            IsDeleted = false
        };
        task = await _db.Insertable(task).ExecuteReturnEntityAsync();
        File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] Inserted task id={task.Id}, commitSha={task.CommitSha[..8]} branch={task.BranchName}\n");

        // 入队立即返回，由后台服务执行审核（解决 HTTP 请求卡住的问题）
        if (!_queue.Enqueue(task.Id))
        {
            File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] 队列满！task id={task.Id} 入队失败\n");
        }

        return Result.Ok("审核任务已创建，正在后台执行");
    }

    public async Task ExecuteReviewAsync(int reviewCommitId)
    {
        File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] ExecuteReviewAsync called for id={reviewCommitId}\n");
        try
        {
            // 更新状态为审核中
            await _db.Updateable<ReviewCommit>()
                .SetColumns(x => x.Status == 1)
                .Where(x => x.Id == reviewCommitId)
                .ExecuteCommandAsync();

            var task = await GetTaskByIdAsync(reviewCommitId);
            if (task == null) return;

            var repo = await _db.Queryable<Repository>()
                .Where(x => x.Id == task.RepositoryId)
                .FirstAsync();
            if (repo == null) { await MarkFailed(reviewCommitId, "仓库不存在"); return; }
            repo.GiteeToken = CryptoHelper.Decrypt(repo.GiteeToken); // 解密 Token

            var model = await _db.Queryable<ModelConfig>()
                .Where(x => x.Id == repo.ModelConfigId && !x.IsDeleted && x.Status == 1)
                .FirstAsync();
            if (model == null) { await MarkFailed(reviewCommitId, "模型配置不存在或已停用"); return; }
            model.ApiKey = CryptoHelper.Decrypt(model.ApiKey); // 解密

            // 拉取 Commit Diff
            var diff = await FetchCommitDiff(repo, task.CommitSha);
            if (diff == null) { await MarkFailed(reviewCommitId, "无法获取 Commit Diff，请检查仓库地址和 Token"); return; }
            if (diff.Count == 0) { await MarkSuccess(reviewCommitId, "无文件变更"); return; }

            // 调用 AI 审核
            File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] 开始调用 AI，diff 文件数={diff.Count}\n");
            var (issues, aiError) = await CallAiReview(model, task, diff);
            File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] AI 调用返回 issues={(issues?.Count ?? -1)} error={aiError}\n");
            if (issues == null) { await MarkFailed(reviewCommitId, aiError ?? "AI 调用失败"); return; }

            // 解析结果入库
            await SaveReviewResults(reviewCommitId, task.RepositoryId, task.CommitSha, issues);

            // 更新仓库最后审核时间
            await _db.Updateable<Repository>()
                .SetColumns(x => x.LastReviewAt == DateTime.Now)
                .Where(x => x.Id == repo.Id)
                .ExecuteCommandAsync();

            await MarkSuccess(reviewCommitId);
        }
        catch (Exception ex)
        {
            await MarkFailed(reviewCommitId, ex.Message);
        }
    }

    private async Task MarkFailed(int reviewCommitId, string msg)
    {
        await _db.Updateable<ReviewCommit>()
            .SetColumns(x => x.Status == 3)
            .SetColumns(x => x.ErrorMsg == msg)
            .SetColumns(x => x.ReviewedAt == DateTime.Now)
            .Where(x => x.Id == reviewCommitId)
            .ExecuteCommandAsync();
    }

    private async Task MarkSuccess(int reviewCommitId, string? msg = null)
    {
        await _db.Updateable<ReviewCommit>()
            .SetColumns(x => x.Status == 2)
            .SetColumns(x => x.ReviewedAt == DateTime.Now)
            .Where(x => x.Id == reviewCommitId)
            .ExecuteCommandAsync();
    }

    // ========== Gitee API ==========

    private async Task<List<CommitFile>> FetchCommitDiff(Repository repo, string sha)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        if (!string.IsNullOrEmpty(repo.GiteeToken))
            client.DefaultRequestHeaders.Add("Authorization", $"token {repo.GiteeToken}");

        var repoSlug = string.IsNullOrEmpty(repo.RepoPath) ? repo.RepoName : repo.RepoPath;

        // 先获取该 commit 的 parent sha
        var commitUrl = $"https://gitee.com/api/v5/repos/{repo.Owner}/{repoSlug}/commits/{sha}";
        File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] FetchCommitDiff: GET {commitUrl}\n");
        var commitResp = await client.GetAsync(commitUrl);
        File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] FetchCommitDiff: commitResp={(int)commitResp.StatusCode}\n");
        if (!commitResp.IsSuccessStatusCode) return new List<CommitFile>();
        var commitJson = await commitResp.Content.ReadAsStringAsync();
        var commitInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(commitJson);
        var parents = commitInfo?["parents"] as JArray;
        var parentSha = parents?.FirstOrDefault()?["sha"]?.ToString();
        File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] FetchCommitDiff: parentSha={parentSha ?? "null"}\n");

        // 使用 compare API 获取 diff
        var diffUrl = $"https://gitee.com/api/v5/repos/{repo.Owner}/{repoSlug}/compare/{parentSha ?? sha}...{sha}";
        File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] FetchCommitDiff: GET {diffUrl}\n");
        var diffResp = await client.GetAsync(diffUrl);
        File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] FetchCommitDiff: diffResp={(int)diffResp.StatusCode}\n");
        if (!diffResp.IsSuccessStatusCode) return new List<CommitFile>();
        var diffJson = await diffResp.Content.ReadAsStringAsync();
        var diffInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(diffJson) ?? new();
        var files = diffInfo["files"] as JArray ?? new JArray();
        File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] FetchCommitDiff: files count={files.Count}\n");

        return files.Select(f =>
        {
            var diffVal = f["diff"]?.ToString();
            var patchVal = f["patch"]?.ToString();
            return new CommitFile
            {
                filename = f["filename"]?.ToString() ?? f["new_path"]?.ToString() ?? "",
                status = f["status"]?.ToString() ?? "",
                additions = f["additions"]?.ToString(),
                deletions = f["deletions"]?.ToString(),
                diff = string.IsNullOrWhiteSpace(diffVal) ? (patchVal ?? "") : diffVal
            };
        }).ToList();
    }

    // ========== AI 审核 ==========

    private async Task<(List<ReviewIssue>? Issues, string? ErrorMessage)> CallAiReview(ModelConfig model, ReviewCommit task, List<CommitFile> files)
    {
        File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] CallAiReview 开始，model={model.Name}，文件数={files.Count}\n");
        // 构建 Prompt（限制单文件 diff 最大 20KB，防止 token 爆表）
        var diffText = string.Join("\n---\n", files.Select(f =>
        {
            var d = f.diff;
            if (d.Length > 20000) d = d[..20000] + $"\n... (截断，原始长度 {d.Length} 字符)";
            return $"文件: {f.filename}\n变更:\n{d}";
        }));

        var systemPrompt = @"你是一位资深代码审核专家，严格审核代码变更，发现问题并给出修复建议。

审核维度：correctness / security / performance / maintainability / best_practice / code_style / other
严重程度：critical（致命）/ major（严重）/ minor（警告）/ suggestion（建议）

【输出格式 - 必须严格遵循】
每行一个发现，8个字段用|分隔：
文件名|起始行|结束行|问题类型|严重程度|问题描述|修复建议|相关代码（最多8行，从diff中截取）

示例：
src/Controller/User.cs|15|20|security|critical|存在SQL注入风险|使用参数化查询|var sql = SELECT * FROM users WHERE id= + id
src/utils/helper.ts|30|30|performance|major|重复计算|添加缓存

无问题时只输出：[]";

        var userPrompt = $"请审核以下代码变更（Commit: {task.CommitSha[..8]}，信息: {task.CommitMessage}，提交人: {task.Committer}）：\n\n{diffText}";

        // 重试配置：最多3次，502/503/504/429/timeout 时重试
        const int maxRetries = 3;
        var retryDelay = TimeSpan.FromSeconds(5);
        Exception? lastEx = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(300);

                var body = new
                {
                    model = model.Name,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    max_tokens = 4096,
                    temperature = 0.1
                };

                var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, $"{FixApiBase(model.ApiBase)}/chat/completions")
                {
                    Content = content
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", model.ApiKey);

                var response = await client.SendAsync(request);
                var raw = await response.Content.ReadAsStringAsync();
                File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] AI HTTP响应 Status={(int)response.StatusCode} Content-Length={raw.Length}\n");

                // 502/503/504/429 需要重试
                if ((int)response.StatusCode is >= 502 and <= 504 or 429)
                {
                    lastEx = new Exception($"AI API 返回 {(int)response.StatusCode}");
                    File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] AI 调用失败({(int)response.StatusCode})，第{attempt}次重试...\n");
                    if (attempt < maxRetries) { await Task.Delay(retryDelay); continue; }
                    return (null, lastEx.Message);
                }

                if (!response.IsSuccessStatusCode)
                {
                    File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] AI HTTP非200: {(int)response.StatusCode}，响应体: {raw[..Math.Min(200, raw.Length)]}\n");
                    return (null, $"HTTP {(int)response.StatusCode}");
                }

                // 调试：打印响应内容前500字符
                File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] AI HTTP 200，响应体前500: {raw[..Math.Min(500, raw.Length)]}\n");

                JObject? result;
                try
                {
                    result = JsonConvert.DeserializeObject<JObject>(raw);
                }
                catch (Exception ex)
                {
                    File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] AI JObject 解析失败: {ex.GetType().Name}: {ex.Message}\n");
                    return (null, $"JSON 解析失败: {ex.Message}");
                }

                File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] deserialization OK, result type={result?.GetType().Name}\n");

                // MiniMax reasoning models: content is often empty [], actual response is in reasoning_content
                // 用 LINQ-to-JSON 安全访问，不用 dynamic
                var choices = result?["choices"] as JArray;
                var msgContent = choices?[0]?["message"]?["content"]?.Value<string>();
                var reasoning = choices?[0]?["message"]?["reasoning_content"]?.Value<string>();
                var reply = !string.IsNullOrWhiteSpace(msgContent) ? msgContent : reasoning;

                File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] choices count={choices?.Count}, content len={msgContent?.Length ?? -1}, reasoning len={reasoning?.Length ?? -1}\n");
                if (string.IsNullOrWhiteSpace(reply)) {
                    File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] reply为空，msgContent='{msgContent?.Substring(0, Math.Min(50, msgContent?.Length ?? 0))}', reasoning='{reasoning?.Substring(0, Math.Min(50, reasoning?.Length ?? 0))}'\n");
                    return (new List<ReviewIssue>(), null);
                }

                // 尝试提取 JSON 数组（处理可能的 markdown 包装）
                // 解析 pipe 分隔的文本格式：文件名|起始行|结束行|类型|严重|描述|建议 [|代码片段]
                var issues = new List<ReviewIssue>();
                var lines = reply.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] 解析 {lines.Length} 行文本格式结果\n");
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed == "[]" || string.IsNullOrWhiteSpace(trimmed)) continue;
                    var parts = trimmed.Split('|');
                    if (parts.Length < 7) continue;
                    issues.Add(new ReviewIssue
                    {
                        file_path = parts[0].Trim(),
                        line_start = int.TryParse(parts[1].Trim(), out var ls) ? ls : 0,
                        line_end = int.TryParse(parts[2].Trim(), out var le) ? le : 0,
                        issue_type = parts[3].Trim(),
                        severity = parts[4].Trim(),
                        description = parts[5].Trim(),
                        suggestion = parts[6].Trim(),
                        diff_content = parts.Length > 7 ? parts[7].Trim() : null
                    });
                }
                File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] 解析出 {issues.Count} 个问题\n");
                return (issues, null);
            }
            catch (TaskCanceledException)
            {
                // timeout
                lastEx = new Exception("AI 调用超时");
                File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] AI 调用超时，第{attempt}次重试...\n");
                if (attempt < maxRetries) { await Task.Delay(retryDelay); continue; }
                return (null, lastEx?.Message ?? "AI 调用超时");
            }
            catch (HttpRequestException ex)
            {
                lastEx = ex;
                File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] AI HttpRequestException: {ex.Message}，第{attempt}次重试...\n");
                if (attempt < maxRetries) { await Task.Delay(retryDelay); continue; }
                return (null, lastEx?.Message ?? "网络异常");
            }
            catch (Exception ex)
            {
                File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] AI outer catch({ex.GetType().Name}): {ex.Message}\n");
                return (null, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        return (null, lastEx?.Message);
    }

    private async Task SaveReviewResults(int reviewCommitId, int repositoryId, string commitSha, List<ReviewIssue> issues)
    {
        var entities = issues.Select(i => new ReviewResult
        {
            ReviewCommitId = reviewCommitId,
            RepositoryId = repositoryId,
            CommitSha = commitSha,
            FilePath = i.file_path ?? "",
            LineStart = i.line_start,
            LineEnd = i.line_end,
            IssueType = i.issue_type ?? "other",
            Severity = i.severity ?? "minor",
            Description = i.description ?? "",
            Suggestion = i.suggestion ?? "",
            DiffContent = i.diff_content ?? "",
            HandlerId = 0,
            Status = 0,
            CreateTime = DateTime.Now,
            IsDeleted = false
        }).ToList();

        if (entities.Any())
            await _db.Insertable(entities).ExecuteCommandAsync();
    }

    // ========== 结果查询 ==========

    public async Task<List<ReviewResult>> GetResultsAsync(int reviewCommitId = 0, int repositoryId = 0)
    {
        var query = _db.Queryable<ReviewResult>().Where(x => !x.IsDeleted);
        if (reviewCommitId > 0)
            query = query.Where(x => x.ReviewCommitId == reviewCommitId);
        if (repositoryId > 0)
            query = query.Where(x => x.RepositoryId == repositoryId);

        return await query.OrderBy(x => x.Severity, OrderByType.Desc)
            .OrderBy(x => x.CreateTime, OrderByType.Desc)
            .ToListAsync();
    }

    public async Task<Result> ClaimIssueAsync(int resultId, int userId, string userName)
    {
        var result = await _db.Queryable<ReviewResult>()
            .Where(x => x.Id == resultId && !x.IsDeleted).FirstAsync();
        if (result == null) return Result.Fail("问题不存在");
        if (result.Status != 0) return Result.Fail("只有待处理状态可以认领");

        await AddLog(resultId, userId, userName, "claim", result.Status, 1);

        result.Status = 1;
        result.HandlerId = userId;
        result.HandlerName = userName;
        await _db.Updateable(result).ExecuteCommandAsync();
        return Result.Ok("已认领");
    }

    public async Task<Result> HandleIssueAsync(int resultId, int userId, string userName, int status, string? memo)
    {
        var result = await _db.Queryable<ReviewResult>()
            .Where(x => x.Id == resultId && !x.IsDeleted).FirstAsync();
        if (result == null) return Result.Fail("问题不存在");
        if (status != 2 && status != 3) return Result.Fail("状态必须是已修复(2)或已忽略(3)");
        if (result.Status != 1 && result.Status != 0)
            return Result.Fail("当前状态不允许此操作");

        await AddLog(resultId, userId, userName, status == 2 ? "fix" : "ignore",
            result.Status, status);

        result.Status = status;
        result.HandlerId = userId;
        result.HandlerName = userName;
        result.HandledAt = DateTime.Now;
        result.HandlerMemo = memo;
        await _db.Updateable(result).ExecuteCommandAsync();
        return Result.Ok(status == 2 ? "已标记修复" : "已标记忽略");
    }

    public async Task<Result> RetryReviewAsync(int id)
    {
        var task = await GetTaskByIdAsync(id);
        if (task == null) return Result.Fail("任务不存在");

        // 只有失败状态可以重试
        if (task.Status != 3) return Result.Fail("只有失败状态可以重试");

        // 重置状态并清空错误信息
        await _db.Updateable<ReviewCommit>()
            .SetColumns(x => x.Status == 0)
            .SetColumns(x => x.ErrorMsg == "")
            .Where(x => x.Id == id)
            .ExecuteCommandAsync();

        // 删除旧结果（避免混淆）
        await _db.Deleteable<ReviewResult>()
            .Where(x => x.ReviewCommitId == id)
            .ExecuteCommandAsync();

        if (!_queue.Enqueue(id))
            return Result.Fail("队列已满，请稍后重试");

        return Result.Ok("已重新加入队列");
    }

    private async Task AddLog(int resultId, int userId, string userName, string action, int from, int to)
    {
        await _db.Insertable(new HandlerLog
        {
            ReviewResultId = resultId,
            OperatorId = userId,
            OperatorName = userName,
            Action = action,
            FromStatus = from,
            ToStatus = to,
            CreateTime = DateTime.Now,
            IsDeleted = false
        }).ExecuteCommandAsync();
    }

    public async Task<Result<object>> GetIssueStatisticsAsync(int repositoryId = 0)
    {
        var query = _db.Queryable<ReviewResult>().Where(x => !x.IsDeleted);
        if (repositoryId > 0)
            query = query.Where(x => x.RepositoryId == repositoryId);

        var all = await query.ToListAsync();
        return Result<object>.Ok(new
        {
            total = all.Count,
            pending = all.Count(x => x.Status == 0),
            claimed = all.Count(x => x.Status == 1),
            fixedCount = all.Count(x => x.Status == 2),
            ignored = all.Count(x => x.Status == 3),
            bySeverity = new
            {
                critical = all.Count(x => x.Severity == "critical"),
                major = all.Count(x => x.Severity == "major"),
                minor = all.Count(x => x.Severity == "minor"),
                suggestion = all.Count(x => x.Severity == "suggestion")
            },
            byType = all.GroupBy(x => x.IssueType)
                .ToDictionary(g => g.Key, g => g.Count())
        });
    }
}

// ========== Gitee API 响应结构 ==========

public class CommitFile
{
    public string filename { get; set; } = "";
    public string status { get; set; } = "";
    public string? additions { get; set; }
    public string? deletions { get; set; }
    public string diff { get; set; } = "";
}

// ========== AI 审核结果结构 ==========

public class ReviewIssue
{
    public string? file_path { get; set; }
    public int? line_start { get; set; }
    public int? line_end { get; set; }
    public string? issue_type { get; set; }
    public string? severity { get; set; }
    public string? description { get; set; }
    public string? suggestion { get; set; }
    public string? diff_content { get; set; }
}

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
    private readonly INotificationService? _notify;

    public ReviewService(ISqlSugarClient db, IHttpClientFactory httpClientFactory, ReviewQueueService queue,
        INotificationService? notify = null)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _queue = queue;
        _notify = notify;
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
            var commitInfo = await _db.Queryable<ReviewCommit>()
                .Where(x => x.Id == reviewCommitId)
                .Select(x => new { x.RepositoryId })
                .FirstAsync();

            await _db.Updateable<ReviewCommit>()
                .SetColumns(x => x.Status == 1)
                .Where(x => x.Id == reviewCommitId)
                .ExecuteCommandAsync();

            // SignalR 通知开始审核
            if (_notify != null && commitInfo != null)
                await _notify.NotifyReviewStartedAsync(commitInfo.RepositoryId, reviewCommitId);

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
            var (files, parentSha) = await FetchCommitDiff(repo, task.CommitSha);
            if (files == null || files.Count == 0) { await MarkSuccess(reviewCommitId, "无文件变更"); return; }

            // 获取每个文件的新旧内容（用于 split diff 展示）
            foreach (var f in files)
            {
                f.newContent = await FetchFileContent(repo, f.filename, task.CommitSha);
                if (!string.IsNullOrEmpty(parentSha))
                    f.oldContent = await FetchFileContent(repo, f.filename, parentSha);
            }

            // 调用 AI 审核
            File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] 开始调用 AI，diff 文件数={files.Count}\n");
            var (issues, aiError) = await CallAiReview(model, task, files);
            File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] AI 调用返回 issues={(issues?.Count ?? -1)} error={aiError}\n");
            if (issues == null) { await MarkFailed(reviewCommitId, aiError ?? "AI 调用失败"); return; }

            // 解析结果入库
            await SaveReviewResults(reviewCommitId, task.RepositoryId, task.CommitSha, issues, files);

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
        var commit = await _db.Queryable<ReviewCommit>()
            .Where(x => x.Id == reviewCommitId).Select(x => new { x.RepositoryId, x.CommitSha }).FirstAsync();

        await _db.Updateable<ReviewCommit>()
            .SetColumns(x => x.Status == 3)
            .SetColumns(x => x.ErrorMsg == msg)
            .SetColumns(x => x.ReviewedAt == DateTime.Now)
            .Where(x => x.Id == reviewCommitId)
            .ExecuteCommandAsync();

        // SignalR 通知
        if (_notify != null && commit != null)
            await _notify.NotifyReviewCompletedAsync(commit.RepositoryId, reviewCommitId, false, msg);
    }

    private async Task MarkSuccess(int reviewCommitId, string? msg = null)
    {
        var commit = await _db.Queryable<ReviewCommit>()
            .Where(x => x.Id == reviewCommitId).Select(x => new { x.RepositoryId, x.CommitSha }).FirstAsync();

        await _db.Updateable<ReviewCommit>()
            .SetColumns(x => x.Status == 2)
            .SetColumns(x => x.ReviewedAt == DateTime.Now)
            .Where(x => x.Id == reviewCommitId)
            .ExecuteCommandAsync();

        // SignalR 通知
        if (_notify != null && commit != null)
            await _notify.NotifyReviewCompletedAsync(commit.RepositoryId, reviewCommitId, true);
    }

    // ========== GitHub / Gitee 统一 API ==========

    /// <summary>根据仓库 URL 判断平台</summary>
    private static (string owner, string repo, string platform) ParseRepoUrl(string url)
    {
        var uri = new Uri(url.TrimEnd('/'));
        var host = uri.Host.ToLower();
        var parts = uri.AbsolutePath.Trim('/').Split('/', 2);
        var platform = host switch
        {
            "github.com" => "github",
            "gitee.com" => "gitee",
            _ => "gitee"
        };
        return (parts[0], parts[1], platform);
    }

    /// <summary>获取平台 API 基础地址</summary>
    private static string GetPlatformApiBase(string platform) => platform.ToLower() switch
    {
        "github" => "https://api.github.com",
        "gitee" => "https://gitee.com/api/v5",
        _ => "https://gitee.com/api/v5"
    };

    private async Task<(List<CommitFile> Files, string? ParentSha)> FetchCommitDiff(Repository repo, string sha)
    {
        var client = _httpClientFactory.CreateClient();

        var (owner, repoSlug, platform) = ParseRepoUrl(repo.RepoUrl);
        var apiBase = GetPlatformApiBase(platform);
        var token = repo.GiteeToken;

        // GitHub 用 header 认证（Bearer），Gitee 用 query param（token）
        if (platform == "github")
        {
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }
        else
        {
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Add("Authorization", $"token {token}");
        }

        // 获取该 commit 的 parent sha
        string? parentSha = null;
        string commitUrl = platform == "github"
            ? $"{apiBase}/repos/{owner}/{repoSlug}/commits/{sha}"
            : (string.IsNullOrEmpty(token)
                ? $"{apiBase}/repos/{owner}/{repoSlug}/commits/{sha}"
                : $"{apiBase}/repos/{owner}/{repoSlug}/commits/{sha}?access_token={token}");

        File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] FetchCommitDiff [{platform}]: GET {commitUrl}\n");
        var commitResp = await client.GetAsync(commitUrl);
        File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] FetchCommitDiff: commitResp={(int)commitResp.StatusCode}\n");
        if (!commitResp.IsSuccessStatusCode) return (new List<CommitFile>(), (string?)null);

        var commitJson = await commitResp.Content.ReadAsStringAsync();
        var commitInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(commitJson);
        var parents = commitInfo?["parents"] as JArray;
        parentSha = parents?.FirstOrDefault()?["sha"]?.ToString();
        File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] FetchCommitDiff: parentSha={parentSha ?? "null"}\n");

        // 使用 compare API 获取 diff
        // GitHub: /repos/{owner}/{repo}/compare/{base}...{head}
        // Gitee:  /repos/{owner}/{repo}/compare/{base}...{head}?access_token=xxx
        string diffUrl = platform == "github"
            ? $"{apiBase}/repos/{owner}/{repoSlug}/compare/{parentSha ?? sha}...{sha}"
            : (string.IsNullOrEmpty(token)
                ? $"{apiBase}/repos/{owner}/{repoSlug}/compare/{parentSha ?? sha}...{sha}"
                : $"{apiBase}/repos/{owner}/{repoSlug}/compare/{parentSha ?? sha}...{sha}?access_token={token}");

        File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] FetchCommitDiff [{platform}]: GET {diffUrl}\n");
        var diffResp = await client.GetAsync(diffUrl);
        File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] FetchCommitDiff: diffResp={(int)diffResp.StatusCode}\n");
        if (!diffResp.IsSuccessStatusCode) return (new List<CommitFile>(), (string?)null);

        var diffJson = await diffResp.Content.ReadAsStringAsync();
        var diffInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(diffJson) ?? new();
        var files = diffInfo["files"] as JArray ?? new JArray();
        File.AppendAllText("/tmp/review_worker.log", $"[{DateTime.Now}] FetchCommitDiff [{platform}]: files count={files.Count}\n");

        // GitHub: diff 在 files[].patch；Gitee: diff 在 files[].diff，统一处理
        var fileList = new List<CommitFile>();
        foreach (var f in files)
        {
            var diffVal = f["patch"]?.ToString() ?? f["diff"]?.ToString() ?? "";
            var filename = f["filename"]?.ToString() ?? f["new_path"]?.ToString() ?? "";
            fileList.Add(new CommitFile
            {
                filename = filename,
                status = f["status"]?.ToString() ?? "",
                additions = f["additions"]?.ToString(),
                deletions = f["deletions"]?.ToString(),
                diff = diffVal
            });
        }
        return (fileList, parentSha);
    }

    /// <summary>
    /// 获取指定 commit 下某文件的完整内容（用于"查看代码"）
    /// </summary>
    private async Task<string> FetchFileContent(Repository repo, string filename, string sha)
    {
        var client = _httpClientFactory.CreateClient();
        var (owner, repoSlug, platform) = ParseRepoUrl(repo.RepoUrl);
        var token = repo.GiteeToken;
        var apiBase = GetPlatformApiBase(platform);

        string url;
        if (platform == "github")
        {
            url = $"{apiBase}/repos/{owner}/{repoSlug}/contents/{Uri.EscapeDataString(filename)}?ref={sha}";
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        }
        else
        {
            var t = string.IsNullOrEmpty(token) ? "" : $"&access_token={token}";
            url = $"{apiBase}/repos/{owner}/{repoSlug}/contents/{Uri.EscapeDataString(filename)}?ref={sha}{t}";
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Add("Authorization", $"token {token}");
        }

        try
        {
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return "";
            var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(await resp.Content.ReadAsStringAsync());
            if (json == null) return "";
            var enc = json["encoding"]?.ToString();
            var contentStr = json["content"]?.ToString()?.Replace("\n", "") ?? "";
            if (enc == "base64" && !string.IsNullOrEmpty(contentStr))
            {
                var bytes = Convert.FromBase64String(contentStr);
                return Encoding.UTF8.GetString(bytes);
            }
            return contentStr;
        }
        catch
        {
            return "";
        }
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
文件名|起始行|结束行|问题类型|严重程度|问题描述|修复建议|相关代码（必须包含10-20行，从diff中截取的上下文）

示例：
src/Controller/User.cs|15|20|security|critical|存在SQL注入风险|使用参数化查询|var sql = ""SELECT * FROM users WHERE id="" + id;
    var cmd = new SqlCommand(sql, conn);
    cmd.Parameters.AddWithValue(""@id"", id);
src/utils/helper.ts|30|30|performance|major|重复计算|添加缓存|var cache = new Dictionary<string, object>();
if (cache.ContainsKey(key)) return cache[key];
var result = Compute(key);
cache[key] = result;

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
                    var parts = trimmed.Split('|', 8); // 限制8段，防止代码里的|被错误切割
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
                        // 用 Split("|", 8) 限制最多切8段，防止代码里的 | 被错误切割
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

    private async Task SaveReviewResults(int reviewCommitId, int repositoryId, string commitSha, List<ReviewIssue> issues, List<CommitFile> files)
    {
        var fileMap = files.ToDictionary(f => f.filename, f => f, StringComparer.OrdinalIgnoreCase);
        var entities = issues.Select(i =>
        {
            var f = fileMap.TryGetValue(i.file_path ?? "", out var cf) ? cf : null;
            var diffContent = f != null ? ComputeUnifiedDiff(f) : "";
            return new ReviewResult
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
                DiffContent = diffContent,
                Status = 0,
                CreateTime = DateTime.Now,
                IsDeleted = false
            };
        }).ToList();

        if (entities.Any())
            await _db.Insertable(entities).ExecuteCommandAsync();
    }

    /// <summary>
    /// 返回标准 unified diff 格式字符串，供 diff2html 渲染
    /// </summary>
    /// <summary>
    /// 返回标准 unified diff 字符串（带完整 header），供 diff2html 渲染
    /// </summary>
    private string ComputeUnifiedDiff(CommitFile f)
    {
        var raw = f.diff ?? "";
        var oldText = f.oldContent ?? "";
        var newText = f.newContent ?? "";

        var sb = new StringBuilder();
        sb.AppendLine($"diff --git a/{f.filename} b/{f.filename}");
        if (string.IsNullOrEmpty(oldText))
        {
            sb.AppendLine("new file mode");
            sb.AppendLine("--- /dev/null");
            sb.AppendLine($"+++ b/{f.filename}");
        }
        else
        {
            sb.AppendLine($"--- a/{f.filename}");
            sb.AppendLine($"+++ b/{f.filename}");
        }

        // API 返回了有效 hunk，直接拼上（GitHub 完整，Gitee 只有 hunk 体）
        if (!string.IsNullOrWhiteSpace(raw))
        {
            sb.Append(raw.TrimStart());
        }
        // API hunk 为空（Gitee 新增文件），用 LCS 算法从内容合成
        else if (!string.IsNullOrEmpty(newText))
        {
            var oldLines = oldText.Split('\n');
            var newLines = newText.Split('\n');
            var lcs = ComputeLcs(oldLines, newLines);
            int o = 0, n = 0;
            foreach (var (_, i, j) in lcs)
            {
                while (o < i) { sb.Append('-'); sb.AppendLine(oldLines[o++]); }
                while (n < j) { sb.Append('+'); sb.AppendLine(newLines[n++]); }
                sb.Append(' '); sb.AppendLine(oldLines[i]);
                o++; n++;
            }
            while (o < oldLines.Length) { sb.Append('-'); sb.AppendLine(oldLines[o++]); }
            while (n < newLines.Length) { sb.Append('+'); sb.AppendLine(newLines[n++]); }
        }

        return sb.ToString();
    }

    /// <summary>Returns tuples of (matched:bool, oldIndex, newIndex)</summary>
    private IEnumerable<(bool Matched, int OldIdx, int NewIdx)> ComputeLcs(string[] a, string[] b)
    {
        int m = a.Length, n = b.Length;
        var dp = new int[m + 1, n + 1];
        for (int i = 1; i <= m; i++)
            for (int j = 1; j <= n; j++)
                dp[i, j] = a[i - 1] == b[j - 1] ? dp[i - 1, j - 1] + 1 : Math.Max(dp[i - 1, j], dp[i, j - 1]);

        var result = new List<(bool, int, int)>();
        int x = m, y = n;
        while (x > 0 || y > 0)
        {
            if (x > 0 && y > 0 && a[x - 1] == b[y - 1])
            { result.Add((true, x - 1, y - 1)); x--; y--; }
            else if (y > 0 && (x == 0 || dp[x, y - 1] >= dp[x - 1, y]))
            { y--; }
            else
            { x--; }
        }
        result.Reverse();
        return result;
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

    public async Task<PagedResult<ReviewResult>> GetResultsPageAsync(int reviewCommitId, int repositoryId, int pageIndex, int pageSize, string? severity, int? status, string? issueType = null)
    {
        var query = _db.Queryable<ReviewResult>().Where(x => !x.IsDeleted);
        if (reviewCommitId > 0)
            query = query.Where(x => x.ReviewCommitId == reviewCommitId);
        if (repositoryId > 0)
            query = query.Where(x => x.RepositoryId == repositoryId);
        if (!string.IsNullOrEmpty(severity))
            query = query.Where(x => x.Severity == severity);
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        if (!string.IsNullOrEmpty(issueType))
            query = query.Where(x => x.IssueType == issueType);

        var total = await query.CountAsync();
        var data = await query
            .OrderBy(x => x.Severity, OrderByType.Desc)
            .OrderBy(x => x.CreateTime, OrderByType.Desc)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // 补齐处理人姓名（通过 HandlerUserId 关联 SysUser）
        var userIds = data.Where(x => x.HandlerUserId > 0).Select(x => x.HandlerUserId!.Value).Distinct().ToList();
        var userMap = new Dictionary<int, string>();
        if (userIds.Count > 0)
        {
            var users = await _db.Queryable<SysUser>().Where(x => userIds.Contains(x.Id)).ToListAsync();
            foreach (var u in users) userMap[u.Id] = u.RealName;
        }
        foreach (var item in data)
        {
            if (item.HandlerUserId > 0 && userMap.TryGetValue(item.HandlerUserId!.Value, out var name))
                item.HandlerName = name;
            else if (item.HandlerUserId > 0)
                item.HandlerName = "未知";
        }

        return new PagedResult<ReviewResult>
        {
            Data = data,
            Total = total,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public async Task<Result> ClaimIssueAsync(int resultId, int userId, string userName)
    {
        var result = await _db.Queryable<ReviewResult>()
            .Where(x => x.Id == resultId && !x.IsDeleted).FirstAsync();
        if (result == null) return Result.Fail("问题不存在");
        if (result.Status != 0) return Result.Fail("只有待处理状态可以认领");

        await AddLog(resultId, userId, "claim", result.Status, 1);

        result.Status = 1;
        result.HandlerUserId = userId;
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

        await AddLog(resultId, userId, status == 2 ? "fix" : "ignore",
            result.Status, status);

        result.Status = status;
        result.HandlerUserId = userId;
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

    private async Task AddLog(int resultId, int userId, string action, int from, int to)
    {
        await _db.Insertable(new HandlerLog
        {
            ReviewResultId = resultId,
            OperatorId = userId,
            OperatorUserId = userId,
            Action = action,
            FromStatus = from,
            ToStatus = to,
            CreateTime = DateTime.Now,
            Remark = "",
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

    /// <summary>仪表盘：最近7天趋势</summary>
    public async Task<Result<object>> GetTrendAsync(int repositoryId = 0)
    {
        var since = DateTime.Today.AddDays(-6);
        var query = _db.Queryable<ReviewResult>()
            .Where(x => !x.IsDeleted && x.CreateTime >= since);
        if (repositoryId > 0) query = query.Where(x => x.RepositoryId == repositoryId);

        var all = await query.ToListAsync();
        var dates = Enumerable.Range(0, 7).Select(i => since.AddDays(i).ToString("MM-dd")).ToList();
        var counts = dates.Select(d => all.Count(x => x.CreateTime.ToString("MM-dd") == d)).ToList();
        var criticalCounts = dates.Select(d => all.Count(x => x.CreateTime.ToString("MM-dd") == d && x.Severity == "critical")).ToList();
        var majorCounts = dates.Select(d => all.Count(x => x.CreateTime.ToString("MM-dd") == d && x.Severity == "major")).ToList();

        return Result<object>.Ok(new { dates, counts, criticalCounts, majorCounts });
    }

    /// <summary>仪表盘：仓库问题排名</summary>
    public async Task<Result<object>> GetRepoRankingAsync()
    {
        var rows = await _db.Queryable<ReviewResult>()
            .Where(x => !x.IsDeleted)
            .GroupBy(x => x.RepositoryId)
            .Select(x => new { repoId = x.RepositoryId, count = SqlFunc.AggregateCount(x.Id) })
            .ToListAsync();

        var repoMap = new Dictionary<int, string>();
        var repoIds = rows.Where(r => r.repoId > 0).Select(r => r.repoId).ToList();
        if (repoIds.Count > 0)
        {
            var repos = await _db.Queryable<Repository>()
                .Where(x => !x.IsDeleted && repoIds.Contains(x.Id))
                .ToListAsync();
            foreach (var r in repos) repoMap[r.Id] = r.RepoName;
        }

        var data = rows
            .Where(r => r.repoId > 0)
            .OrderByDescending(r => r.count)
            .Take(10)
            .Select(r => new { name = repoMap.GetValueOrDefault(r.repoId, "未知"), count = r.count })
            .ToList();

        return Result<object>.Ok(data);
    }

    /// <summary>仪表盘：最近审核任务</summary>
    public async Task<Result<object>> GetRecentTasksAsync(int limit = 10)
    {
        var tasks = await _db.Queryable<ReviewCommit>()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.CreateTime, OrderByType.Desc)
            .Take(limit)
            .ToListAsync();

        var repoIds = tasks.Select(t => t.RepositoryId).Distinct().ToList();
        var repoMap = new Dictionary<int, string>();
        if (repoIds.Count > 0)
        {
            var repos = await _db.Queryable<Repository>()
                .Where(x => !x.IsDeleted && repoIds.Contains(x.Id))
                .ToListAsync();
            foreach (var r in repos) repoMap[r.Id] = r.RepoName;
        }

        var data = tasks.Select(t => new {
            id = t.Id,
            repoName = repoMap.GetValueOrDefault(t.RepositoryId, "未知"),
            branchName = t.BranchName,
            commitSha = t.CommitSha.Length > 7 ? t.CommitSha[..7] : t.CommitSha,
            status = t.Status,
            createTime = t.CreateTime.ToString("MM-dd HH:mm"),
            issueCount = t.ResultCount
        }).ToList();

        return Result<object>.Ok(data);
    }

    /// <summary>仪表盘：仓库总览</summary>
    public async Task<Result<object>> GetRepoOverviewAsync()
    {
        var totalRepos = await _db.Queryable<Repository>().Where(x => !x.IsDeleted).CountAsync();
        var totalTasks = await _db.Queryable<ReviewCommit>().Where(x => !x.IsDeleted).CountAsync();
        var totalReviews = await _db.Queryable<ReviewCommit>().Where(x => !x.IsDeleted && x.Status == 2).CountAsync();
        var totalIssues = await _db.Queryable<ReviewResult>().Where(x => !x.IsDeleted).CountAsync();
        var todayIssues = await _db.Queryable<ReviewResult>()
            .Where(x => !x.IsDeleted && x.CreateTime >= DateTime.Today).CountAsync();

        return Result<object>.Ok(new {
            totalRepos,
            totalTasks,
            totalReviews,
            totalIssues,
            todayIssues
        });
    }

    /// <summary>仪表盘：处理效率（平均从认领到修复的时间）</summary>
    public async Task<Result<object>> GetHandlingStatsAsync()
    {
        var logs = await _db.Queryable<HandlerLog>()
            .Where(x => !x.IsDeleted && x.Action == "fix")
            .OrderBy(x => x.CreateTime, OrderByType.Desc)
            .Take(100)
            .ToListAsync();

        if (logs.Count == 0) return Result<object>.Ok(new { avgHours = 0, count = 0, details = new List<object>() });

        // 找 claim 日志算时间差
        var resultIds = logs.Select(l => l.ReviewResultId).Distinct().ToList();
        var claims = await _db.Queryable<HandlerLog>()
            .Where(x => !x.IsDeleted && x.Action == "claim" && resultIds.Contains(x.ReviewResultId))
            .ToListAsync();

        var claimMap = claims.GroupBy(c => c.ReviewResultId).ToDictionary(g => g.Key, g => g.First().CreateTime);

        var hours = new List<double>();
        var details = new List<object>();
        foreach (var log in logs)
        {
            if (claimMap.TryGetValue(log.ReviewResultId, out var claimTime))
            {
                var h = (log.CreateTime - claimTime).TotalHours;
                if (h >= 0 && h < 720) // 排除异常值（>30天）
                {
                    hours.Add(h);
                    details.Add(new { hours = Math.Round(h, 1), time = log.CreateTime.ToString("MM-dd HH:mm") });
                }
            }
        }

        return Result<object>.Ok(new {
            avgHours = hours.Count > 0 ? Math.Round(hours.Average(), 1) : 0,
            count = hours.Count,
            details = details.Take(10).ToList()
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
    /// <summary>父 commit 的文件内容</summary>
    public string? oldContent { get; set; }
    /// <summary>当前 commit 的文件内容</summary>
    public string? newContent { get; set; }
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

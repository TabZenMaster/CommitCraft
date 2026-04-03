using CodeReview.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.Net.Http;
using System.Text;

namespace CodeReview.Application.Service;

// ========== 后台审核 Worker（独立 Scope 中运行）==========
public class ReviewWorker
{
    private readonly ISqlSugarClient _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public ReviewWorker(ISqlSugarClient db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    public async Task ExecuteAsync(int reviewCommitId)
    {
        try
        {
            await _db.Updateable<ReviewCommit>()
                .SetColumns(x => x.Status == 1)
                .Where(x => x.Id == reviewCommitId)
                .ExecuteCommandAsync();

            var task = await _db.Queryable<ReviewCommit>()
                .Where(x => x.Id == reviewCommitId && !x.IsDeleted).FirstAsync();
            if (task == null) return;

            var repo = await _db.Queryable<Repository>()
                .Where(x => x.Id == task.RepositoryId).FirstAsync();
            if (repo == null) { await MarkFailed(reviewCommitId, "仓库不存在"); return; }

            var model = await _db.Queryable<ModelConfig>()
                .Where(x => x.Id == repo.ModelConfigId && !x.IsDeleted && x.Status == 1).FirstAsync();
            if (model == null) { await MarkFailed(reviewCommitId, "模型配置不存在或已停用"); return; }

            var diff = await FetchCommitDiff(repo, task.CommitSha);
            if (diff == null) { await MarkFailed(reviewCommitId, "无法获取 Commit Diff，请检查仓库地址和 Token"); return; }
            if (diff.Count == 0) { await MarkSuccess(reviewCommitId, "无文件变更"); return; }

            var issues = await CallAiReview(model, task, diff);
            if (issues == null) { await MarkFailed(reviewCommitId, "AI 调用失败"); return; }

            await SaveReviewResults(reviewCommitId, task.RepositoryId, task.CommitSha, issues);

            await _db.Updateable<Repository>()
                .SetColumns(x => x.LastReviewAt == DateTime.Now)
                .Where(x => x.Id == repo.Id).ExecuteCommandAsync();

            await MarkSuccess(reviewCommitId);
        }
        catch (Exception ex)
        {
            await MarkFailed(reviewCommitId, ex.Message);
        }
    }

    private async Task MarkFailed(int id, string msg)
    {
        await _db.Updateable<ReviewCommit>()
            .SetColumns(x => x.Status == 3)
            .SetColumns(x => x.ErrorMsg == msg)
            .SetColumns(x => x.ReviewedAt == DateTime.Now)
            .Where(x => x.Id == id).ExecuteCommandAsync();
    }

    private async Task MarkSuccess(int id, string? msg = null)
    {
        await _db.Updateable<ReviewCommit>()
            .SetColumns(x => x.Status == 2)
            .SetColumns(x => x.ReviewedAt == DateTime.Now)
            .Where(x => x.Id == id).ExecuteCommandAsync();
    }

    private async Task<List<CommitFile>> FetchCommitDiff(Repository repo, string sha)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        var commitUrl = $"https://gitee.com/api/v5/repos/{repo.Owner}/{repo.RepoName}/commits/{sha}?access_token={repo.GiteeToken}";
        var commitResp = await client.GetAsync(commitUrl);
        if (!commitResp.IsSuccessStatusCode) return new List<CommitFile>();

        var commitJson = await commitResp.Content.ReadAsStringAsync();
        var commitInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(commitJson);
        var parents = commitInfo?["parents"] as JArray;
        var parentSha = parents?.FirstOrDefault()?["sha"]?.ToString();

        var diffUrl = $"https://gitee.com/api/v5/repos/{repo.Owner}/{repo.RepoName}/compare/{parentSha ?? sha}...{sha}?access_token={repo.GiteeToken}";
        var diffResp = await client.GetAsync(diffUrl);
        if (!diffResp.IsSuccessStatusCode) return new List<CommitFile>();

        var diffJson = await diffResp.Content.ReadAsStringAsync();
        var diffInfo = JsonConvert.DeserializeObject<Dictionary<string, object>>(diffJson) ?? new();
        var files = diffInfo["files"] as JArray ?? new JArray();

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

    private async Task<List<ReviewIssue>?> CallAiReview(ModelConfig model, ReviewCommit task, List<CommitFile> files)
    {
        var diffText = string.Join("\n---\n", files.Select(f =>
            $"文件: {f.filename}\n变更:\n{f.diff}"));

        var systemPrompt = @"你是一位资深代码审核专家。你的职责是对代码变更进行严格审核，发现潜在问题并给出修复建议。

审核维度：
1. correctness（正确性）- 逻辑错误、边界条件、异常处理
2. security（安全性）- SQL注入、XSS、敏感信息泄露、权限问题
3. performance（性能）- 重复计算、不必要的循环、内存泄漏
4. maintainability（可维护性）- 重复代码、过长函数、耦合度高
5. best_practice（最佳实践）- 违背语言/框架惯用写法
6. code_style（代码风格）- 命名不规范、注释缺失
7. other（其他）- 不属于以上类型的其他问题

严重程度：
- critical（致命）：必须立即修复，否则引发严重bug或安全漏洞
- major（严重）：应在上线前修复
- minor（警告）：建议修复
- suggestion（建议）：可选优化

【重要】你必须且只能输出一个合法的JSON数组，不要输出任何其他内容。
JSON数组中每个元素代表一个发现的问题，格式如下：
{
  ""file_path"": ""文件相对路径"",
  ""line_start"": 起始行号（整数）,
  ""line_end"": 结束行号（整数）,
  ""issue_type"": ""问题类型"",
  ""severity"": ""严重程度"",
  ""description"": ""问题描述"",
  ""suggestion"": ""修复建议""
}

如果没有发现问题，返回空数组 []。";

        var userPrompt = $"请审核以下代码变更（Commit: {task.CommitSha[..Math.Min(8, task.CommitSha.Length)]}，信息: {task.CommitMessage}，提交人: {task.Committer}）：\n\n{diffText}";

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(120);

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

            var apiBase = model.ApiBase.TrimEnd('/');
            if (!apiBase.EndsWith("/v1")) apiBase += "/v1";

            var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBase}/chat/completions")
            {
                Content = content
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", model.ApiKey);

            var response = await client.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return null;

            dynamic? result = JsonConvert.DeserializeObject<dynamic>(raw);
            // MiniMax reasoning models: content is often empty [], actual response is in reasoning_content
            var reply = (string?)result?.choices?[0]?.message?.content;
            if (string.IsNullOrWhiteSpace(reply) || (reply?.Trim() == "[]"))
                reply = (string?)result?.choices?[0]?.message?.reasoning_content;
            if (string.IsNullOrWhiteSpace(reply)) return new List<ReviewIssue>();

            var jsonStr = reply.Trim();
            if (jsonStr.StartsWith("```"))
            {
                var start = jsonStr.IndexOf('[');
                var end = jsonStr.LastIndexOf(']');
                if (start >= 0 && end > start)
                    jsonStr = jsonStr[start..(end + 1)];
            }

            return JsonConvert.DeserializeObject<List<ReviewIssue>>(jsonStr) ?? new List<ReviewIssue>();
        }
        catch
        {
            return null;
        }
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
            Status = 0,
            CreateTime = DateTime.Now,
            IsDeleted = false
        }).ToList();

        if (entities.Any())
            await _db.Insertable(entities).ExecuteCommandAsync();
    }
}

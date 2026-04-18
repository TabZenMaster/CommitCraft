using CodeReview.Application;
using CodeReview.Application.IService;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CodeReview.Api.Controllers;

/// <summary>
/// 免登录快速审核接口（无需认证，全部新建不复用）
/// </summary>
[ApiController]
[Route("api/quick")]
public class QuickReviewController : ControllerBase
{
    private readonly HttpClient _http;
    private readonly ILogger<QuickReviewController> _logger;

    public QuickReviewController(IHttpClientFactory httpFactory, ILogger<QuickReviewController> logger)
    {
        _http = httpFactory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(180);
        _logger = logger;
    }

    /// <summary>获取仓库分支列表</summary>
    [HttpPost("branches")]
    public async Task<Result<object>> GetBranches([FromBody] QuickRepoDto dto)
    {
        try
        {
            var repoInfo = ParseRepoUrl(dto.RepoUrl);
            if (repoInfo == null) return Result<object>.Fail("无效的仓库地址");

            var url = "https://gitee.com/api/v5/repos/" + repoInfo.Value.Owner + "/" + repoInfo.Value.Repo + "/branches";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(dto.AccessToken))
                request.Headers.Add("Authorization", "token " + dto.AccessToken);

            var resp = await _http.SendAsync(request);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                return Result<object>.Fail("获取分支失败: " + err);
            }

            var json = await resp.Content.ReadAsStringAsync();
            var branches = JsonSerializer.Deserialize<JsonElement>(json);
            var result = new List<object>();
            foreach (var b in branches.EnumerateArray())
            {
                result.Add(new { name = b.GetProperty("name").GetString(), commit = b.GetProperty("commit").GetProperty("sha").GetString() });
            }
            return Result<object>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取分支失败");
            return Result<object>.Fail("获取分支失败: " + ex.Message);
        }
    }

    /// <summary>获取分支的最近提交列表</summary>
    [HttpPost("commits")]
    public async Task<Result<object>> GetCommits([FromBody] QuickCommitsDto dto)
    {
        try
        {
            var repoInfo = ParseRepoUrl(dto.RepoUrl);
            if (repoInfo == null) return Result<object>.Fail("无效的仓库地址");

            var url = "https://gitee.com/api/v5/repos/" + repoInfo.Value.Owner + "/" + repoInfo.Value.Repo
                + "/commits?sha=" + Uri.EscapeDataString(dto.BranchName) + "&per_page=20";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(dto.AccessToken))
                request.Headers.Add("Authorization", "token " + dto.AccessToken);

            var resp = await _http.SendAsync(request);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync();
                return Result<object>.Fail("获取提交记录失败: " + err);
            }

            var json = await resp.Content.ReadAsStringAsync();
            var commits = JsonSerializer.Deserialize<JsonElement>(json);
            var result = new List<object>();
            foreach (var c in commits.EnumerateArray())
            {
                var sha = c.GetProperty("sha").GetString() ?? "";
                var msg = c.GetProperty("commit").GetProperty("message").GetString() ?? "";
                var shortMsg = msg.Split('\n')[0];
                var date = c.GetProperty("commit").GetProperty("author").GetProperty("date").GetString() ?? "";
                var author = c.GetProperty("commit").GetProperty("author").GetProperty("name").GetString() ?? "";
                result.Add(new { sha = sha[..Math.Min(8, sha.Length)], fullSha = sha, shortMsg, date, author });
            }
            return Result<object>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取提交记录失败");
            return Result<object>.Fail("获取提交记录失败: " + ex.Message);
        }
    }

    /// <summary>获取提交涉及的文件列表</summary>
    [HttpPost("files")]
    public async Task<Result<object>> GetFiles([FromBody] QuickFilesDto dto)
    {
        try
        {
            var repoInfo = ParseRepoUrl(dto.RepoUrl);
            if (repoInfo == null) return Result<object>.Fail("无效的仓库地址");

            var diff = await GetCommitDiff(repoInfo.Value.Owner, repoInfo.Value.Repo, dto.CommitSha, dto.AccessToken);
            if (diff == null) return Result<object>.Fail("获取代码差异失败，请检查Token是否有读取仓库代码的权限");

            // diff 结构: "=== filename ===\npatch\n..."
            var files = new List<object>();
            var parts = diff.Split("=== ");
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                var idx = part.IndexOf('\n');
                if (idx < 0) continue;
                var filename = part[..idx].Trim();
                var patch = part[(idx + 1)..].Trim();
                files.Add(new { filename, patch, status = "modified" });
            }

            return Result<object>.Ok(new { files, commitSha = dto.CommitSha[..Math.Min(8, dto.CommitSha.Length)] });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取文件列表失败");
            return Result<object>.Fail("获取文件列表失败: " + ex.Message);
        }
    }

    /// <summary>单文件AI审核</summary>
    [HttpPost("review-file")]
    public async Task<Result<object>> ReviewFile([FromBody] ReviewFileDto dto)
    {
        try
        {
            var aiResult = await CallAiReview(dto.ModelApiUrl, dto.ModelName, dto.ApiKey,
                "文件: " + dto.Filename + "\n状态: " + dto.Status, dto.Patch);
            if (aiResult == null) return Result<object>.Fail("AI审核失败，请检查API地址是否以/chat/completions结尾");
            return Result<object>.Ok(new { filename = dto.Filename, result = aiResult });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "单文件审核失败: {File}", dto.Filename);
            return Result<object>.Fail("审核失败: " + ex.Message);
        }
    }

    /// <summary>测试 AI 接口连通性</summary>
    [HttpPost("test-ai")]
    public async Task<Result> TestAi([FromBody] TestAiDto dto)
    {
        try
        {
            var prompt = "请回复 OK，只回复 OK，不要多于一个字。";
            var body = new
            {
                model = dto.ModelName,
                messages = new[] { new { role = "user", content = prompt } },
                max_tokens = 10
            };

            var request = new HttpRequestMessage(HttpMethod.Post, dto.ModelApiUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrEmpty(dto.ApiKey))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", dto.ApiKey);

            var resp = await _http.SendAsync(request);
            var content = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return Result.Fail("接口返回错误: " + content + "（请确认API地址是否以 /chat/completions 结尾）");

            if (!content.Contains("OK") && !content.Contains("ok"))
                return Result.Fail("返回内容异常: " + content);

            return Result.Ok("接口可用");
        }
        catch (Exception ex)
        {
            return Result.Fail("连接失败: " + ex.Message);
        }
    }

    // ---- 私有方法 ----

    private (string Owner, string Repo)? ParseRepoUrl(string url)
    {
        var clean = url.Trim().TrimEnd('/').Replace("https://", "").Replace("http://", "");
        if (clean.StartsWith("gitee.com/")) clean = clean["gitee.com/".Length..];
        var parts = clean.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;
        return (parts[0], parts[1]);
    }

    private async Task<string?> GetLatestCommitSha(string owner, string repo, string branch, string? token)
    {
        var url = "https://gitee.com/api/v5/repos/" + owner + "/" + repo + "/commits?sha=" + Uri.EscapeDataString(branch) + "&per_page=1";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(token)) request.Headers.Add("Authorization", "token " + token);

        var resp = await _http.SendAsync(request);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync();
        var arr = JsonSerializer.Deserialize<JsonElement>(json);
        if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
            return arr[0].GetProperty("sha").GetString();
        return null;
    }

    private async Task<(string message, string committer)?> GetCommitInfo(string owner, string repo, string sha, string? token)
    {
        var url = "https://gitee.com/api/v5/repos/" + owner + "/" + repo + "/commits/" + sha;
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(token)) request.Headers.Add("Authorization", "token " + token);

        var resp = await _http.SendAsync(request);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync();
        var obj = JsonSerializer.Deserialize<JsonElement>(json);
        var message = obj.GetProperty("commit").GetProperty("message").GetString() ?? "";
        var committer = obj.GetProperty("committer").GetProperty("login").GetString() ?? "unknown";
        return (message.Split('\n')[0], committer);
    }

    private async Task<string?> GetCommitDiff(string owner, string repo, string sha, string? token)
    {
        var client = new HttpClient();
        if (!string.IsNullOrEmpty(token))
            client.DefaultRequestHeaders.Add("Authorization", "token " + token);

        // 1. 获取 commit 详情（含 parents）
        string commitUrl = "https://gitee.com/api/v5/repos/" + owner + "/" + repo + "/commits/" + sha;
        if (!string.IsNullOrEmpty(token))
            commitUrl += "?access_token=" + token;

        var commitResp = await client.GetAsync(commitUrl);
        if (!commitResp.IsSuccessStatusCode)
        {
            _logger.LogError("[GetCommitDiff] commit info failed: {Status}", commitResp.StatusCode);
            return null;
        }

        var commitJson = await commitResp.Content.ReadAsStringAsync();
        var commitObj = JsonSerializer.Deserialize<JsonElement>(commitJson);

        // 取 parent sha
        string? parentSha = null;
        if (commitObj.TryGetProperty("parents", out var parents) && parents.GetArrayLength() > 0)
        {
            parentSha = parents[0].GetProperty("sha").GetString();
        }

        // 2. 用 compare API 获取 diff：base...head
        var compareUrl = "https://gitee.com/api/v5/repos/" + owner + "/" + repo
            + "/compare/" + (parentSha ?? sha) + "..." + sha;
        if (!string.IsNullOrEmpty(token))
            compareUrl += "?access_token=" + token;

        var diffResp = await client.GetAsync(compareUrl);
        if (!diffResp.IsSuccessStatusCode)
        {
            var err = await diffResp.Content.ReadAsStringAsync();
            _logger.LogError("[GetCommitDiff] compare failed: {Status} body={Body}", diffResp.StatusCode, err);
            return null;
        }

        var diffJson = await diffResp.Content.ReadAsStringAsync();
        var diffObj = JsonSerializer.Deserialize<JsonElement>(diffJson);

        _logger.LogInformation("[GetCommitDiff] compare response keys: {Keys}", string.Join(",", diffObj.EnumerateObject().Select(p => p.Name)));
        if (diffObj.TryGetProperty("files", out var files) && files.GetArrayLength() > 0)
        {
            var firstFile = files[0];
            _logger.LogInformation("[GetCommitDiff] first file keys: {Keys}", string.Join(",", firstFile.EnumerateObject().Select(p => p.Name)));
            _logger.LogInformation("[GetCommitDiff] first file sample: {Json}", firstFile.GetRawText().Length > 300 ? firstFile.GetRawText()[..300] : firstFile.GetRawText());
        }

        var diffs = new List<string>();
        if (diffObj.TryGetProperty("files", out files))
        {
            _logger.LogInformation("[GetCommitDiff] files count={Count}, truncated={Truncated}",
                files.GetArrayLength(), diffObj.TryGetProperty("truncated", out var t) ? t.GetBoolean() : false);
            foreach (var f in files.EnumerateArray())
            {
                var filename = f.TryGetProperty("new_path", out var np) ? np.GetString() ?? "" : (f.TryGetProperty("filename", out var fn) ? fn.GetString() ?? "" : "");
                var patch = f.TryGetProperty("patch", out var pp) ? pp.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(patch)) continue; // 跳过二进制或无diff的文件
                diffs.Add("=== " + filename + " ===\n" + patch);
            }
        }
        else
        {
            _logger.LogWarning("[GetCommitDiff] no 'files' field in response, keys={Keys}", string.Join(",", diffObj.EnumerateObject().Select(p => p.Name)));
        }

        var result = string.Join("\n", diffs);
        _logger.LogInformation("[GetCommitDiff] final diff length={Len}", result.Length);
        return result;
    }

    private async Task<string?> CallAiReview(string apiUrl, string modelName, string apiKey, string commitMessage, string diff)
    {
        // 限制 diff 大小，避免超时（取前 8000 字符）
        var truncatedDiff = diff.Length > 8000 ? diff[..8000] + "\n... (diff过长已截断)" : diff;

        var prompt = BuildReviewPrompt(commitMessage, truncatedDiff);

        var body = new
        {
            model = modelName,
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = 4096,
            temperature = 0.1
        };

        using var aiClient = new HttpClient();
        aiClient.Timeout = TimeSpan.FromSeconds(180);
        using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        var resp = await aiClient.SendAsync(request);
        if (!resp.IsSuccessStatusCode)
        {
            var errBody = await resp.Content.ReadAsStringAsync();
            _logger.LogError("[CallAiReview] failed: {Status} body={Body}", resp.StatusCode, errBody);
            return null;
        }

        var content = await resp.Content.ReadAsStringAsync();
        var obj = JsonSerializer.Deserialize<JsonElement>(content);
        if (obj.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            return choices[0].GetProperty("message").GetProperty("content").GetString();
        return null;
    }

    private static string BuildReviewPrompt(string commitMessage, string diff)
    {
        return "你是一个专业的代码审查员。请审查以下代码变更。\n\n" +
               "提交信息: " + commitMessage + "\n\n" +
               "代码差异:\n" + diff + "\n\n" +
               "请从以下几个方面审查：\n" +
               "1. 安全性 - 是否有安全漏洞\n" +
               "2. 正确性 - 是否有逻辑错误或bug\n" +
               "3. 性能 - 是否有性能问题\n" +
               "4. 可维护性 - 代码是否清晰可维护\n" +
               "5. 最佳实践 - 是否遵循编码规范\n\n" +
               "如果没有发现问题，请回复\"未发现明显问题\"，不要返回任何其他内容。\n\n" +
               "审查结果（严格pipe分隔格式，每行一个问题，禁止其他内容）：\n" +
               "文件名|起始行|结束行|类型|严重程度|问题描述|修复建议\n" +
               "示例：\n" +
               "src/Service/UserService.cs|42|48|security|critical|存在SQL注入风险|使用参数化查询替代字符串拼接\n" +
               "src/utils/Helper.cs|10|15|correctness|minor|变量未初始化|在声明时赋予默认值";
    }
}

public class QuickRepoDto
{
    public string RepoUrl { get; set; } = "";
    public string AccessToken { get; set; } = "";
}

public class QuickReviewDto
{
    public string ModelApiUrl { get; set; } = "";
    public string ModelName { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string RepoUrl { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string BranchName { get; set; } = "";
    public string CommitSha { get; set; } = "";
}

public class QuickFilesDto
{
    public string RepoUrl { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string CommitSha { get; set; } = "";
}

public class TestAiDto
{
    public string ModelApiUrl { get; set; } = "";
    public string ModelName { get; set; } = "";
    public string ApiKey { get; set; } = "";
}

public class QuickCommitsDto
{
    public string RepoUrl { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string BranchName { get; set; } = "";
}

public class ReviewFileDto
{
    public string ModelApiUrl { get; set; } = "";
    public string ModelName { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Filename { get; set; } = "";
    public string Status { get; set; } = "";
    public string Patch { get; set; } = "";
}

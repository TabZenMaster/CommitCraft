using CodeReview.Application;
using CodeReview.Application.IService;
using CodeReview.Domain.Entities;
using Newtonsoft.Json;
using SqlSugar;
using System.Net.Http;

namespace CodeReview.Application.Service;

public class RepositoryService : IRepositoryService
{
    private readonly ISqlSugarClient _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public RepositoryService(ISqlSugarClient db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<List<Repository>> GetListAsync()
    {
        var repos = await _db.Queryable<Repository>()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.CreateTime, OrderByType.Desc)
            .ToListAsync();

        var modelIds = repos.Select(r => r.ModelConfigId).Distinct().ToList();
        var models = (await _db.Queryable<ModelConfig>()
            .Where(x => !x.IsDeleted && modelIds.Contains(x.Id))
            .ToListAsync())
            .ToDictionary(x => x.Id, x => x.Name);

        foreach (var repo in repos)
        {
            repo.ModelName = models.GetValueOrDefault(repo.ModelConfigId);
            repo.GiteeToken = MaskToken(repo.GiteeToken); // 脱敏
        }

        return repos;
    }

    public async Task<Repository?> GetByIdAsync(int id)
    {
        var repo = await _db.Queryable<Repository>().Where(x => x.Id == id && !x.IsDeleted).FirstAsync();
        if (repo != null)
            repo.GiteeToken = CryptoHelper.Decrypt(repo.GiteeToken);
        return repo;
    }

    public async Task<bool> AddAsync(Repository model)
    {
        model.CreateTime = DateTime.Now;
        model.IsDeleted = false;
        model.LastReviewAt = null;
        if (string.IsNullOrEmpty(model.GiteeToken))
            return false;

        // 从仓库地址解析出 owner 和 slug
        var parsed = ParseRepoUrl(model.RepoUrl);
        if (parsed == null)
            return false;
        model.Owner = parsed.Value.owner;
        model.RepoPath = parsed.Value.slug;

        // 获取仓库显示名
        var giteeInfo = await FetchGiteeRepoInfoAsync(parsed.Value.platform, model.Owner, model.RepoPath, model.GiteeToken);
        if (giteeInfo != null)
            model.RepoName = giteeInfo.Value.name;

        model.GiteeToken = CryptoHelper.Encrypt(model.GiteeToken);
        return await _db.Insertable(model).ExecuteCommandAsync() > 0;
    }

    public async Task<bool> UpdateAsync(Repository model)
    {
        var old = await GetByIdAsync(model.Id);
        if (old == null) return false;

        // 如果仓库地址变了，重新解析并拉取显示名
        if (model.RepoUrl != old.RepoUrl)
        {
            var parsed = ParseRepoUrl(model.RepoUrl);
            if (parsed != null)
            {
                model.Owner = parsed.Value.owner;
                model.RepoPath = parsed.Value.slug;
                var giteeInfo = await FetchGiteeRepoInfoAsync(parsed.Value.platform, model.Owner, model.RepoPath, old.GiteeToken);
                if (giteeInfo != null)
                    model.RepoName = giteeInfo.Value.name;
            }
        }

        // 脱敏值或空值不更新 Token，保持原密文
        if (model.GiteeToken == "******")
        {
            // GetByIdAsync 返回解密后的值，需直接从 DB 取原始密文
            var raw = await _db.Queryable<Repository>()
                .Where(x => x.Id == model.Id)
                .Select(x => x.GiteeToken)
                .FirstAsync();
            model.GiteeToken = raw ?? model.GiteeToken;
        }
        else
        {
            model.GiteeToken = CryptoHelper.Encrypt(model.GiteeToken);
        }
        return await _db.Updateable(model).ExecuteCommandAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int id) =>
        await _db.Updateable<Repository>()
            .SetColumns(x => x.IsDeleted == true)
            .Where(x => x.Id == id)
            .ExecuteCommandAsync() > 0;

    public async Task<Result<object>> TestConnectionAsync(int repoId)
    {
        var repo = await GetByIdAsync(repoId);
        if (repo == null)
            return Result<object>.Fail("仓库不存在");

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            if (!string.IsNullOrEmpty(repo.GiteeToken))
                client.DefaultRequestHeaders.Add("Authorization", $"token {repo.GiteeToken}");
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            var apiBase = GetApiBase(repo.RepoUrl);
            var resp = await client.GetAsync($"{apiBase}/repos/{repo.Owner}/{repo.RepoPath}");
            if (!resp.IsSuccessStatusCode)
                return Result<object>.Fail($"平台返回 HTTP {(int)resp.StatusCode}，请检查仓库地址和 Token 权限");

            var json = await resp.Content.ReadAsStringAsync();
            var info = JsonConvert.DeserializeObject<dynamic>(json);
            return Result<object>.Ok(new
            {
                name = (string?)info?.name,
                fullName = (string?)info?.full_name ?? (string?)info?.name,
                description = (string?)info?.description,
                language = (string?)info?.language,
                message = "仓库连接正常"
            });
        }
        catch (Exception ex)
        {
            return Result<object>.Fail($"连接异常: {ex.Message}");
        }
    }

    /// <summary>从 Gitee 拉取仓库最近提交列表</summary>
    public async Task<Result<object>> GetCommitsAsync(int repoId, string? branch = null, int page = 1, int perPage = 20)
    {
        var repo = await GetByIdAsync(repoId);
        if (repo == null)
            return Result<object>.Fail("仓库不存在");

        try
        {
            var client = _httpClientFactory.CreateClient();
            if (!string.IsNullOrEmpty(repo.GiteeToken))
                client.DefaultRequestHeaders.Add("Authorization", $"token {repo.GiteeToken}");
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            var apiBase = GetApiBase(repo.RepoUrl);
            var url = $"{apiBase}/repos/{repo.Owner}/{repo.RepoPath}/commits";
            if (!string.IsNullOrEmpty(branch))
                url += $"?sha={Uri.EscapeDataString(branch)}&page={page}&per_page={perPage}";
            else
                url += $"?page={page}&per_page={perPage}";
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
                return Result<object>.Fail($"Gitee API 返回 {resp.StatusCode}");

            var json = await resp.Content.ReadAsStringAsync();
            var commits = JsonConvert.DeserializeObject<List<dynamic>>(json) ?? new();

            var result = commits.Select(c => new
            {
                sha = (string?)c?.sha,
                message = ((string?)c?.commit?.message ?? "").Split('\n')[0], // 取第一行
                committer = (string?)c?.committer?.name ?? (string?)c?.author?.login ?? "",
                committedAt = ParseGiteeDate(c?.commit?.author?.date),
                url = (string?)c?.html_url ?? ""
            }).ToList();

            return Result<object>.Ok(result);
        }
        catch (Exception ex)
        {
            return Result<object>.Fail($"拉取失败: {ex.Message}");
        }
    }

    /// <summary>获取仓库分支列表</summary>
    public async Task<Result<object>> GetBranchesAsync(int repoId)
    {
        var repo = await GetByIdAsync(repoId);
        if (repo == null)
            return Result<object>.Fail("仓库不存在");

        try
        {
            var client = _httpClientFactory.CreateClient();
            if (!string.IsNullOrEmpty(repo.GiteeToken))
                client.DefaultRequestHeaders.Add("Authorization", $"token {repo.GiteeToken}");
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            var parsed = ParseRepoUrl(repo.RepoUrl);
            if (parsed == null)
                return Result<object>.Fail("仓库地址格式错误");

            var apiBase = GetPlatformApiBase(parsed.Value.platform);
            var url = $"{apiBase}/repos/{repo.Owner}/{repo.RepoPath}/branches";
            var resp = await client.GetAsync(url);
            if (!resp.IsSuccessStatusCode)
                return Result<object>.Fail($"获取分支失败 HTTP {(int)resp.StatusCode}");

            var json = await resp.Content.ReadAsStringAsync();
            var branches = JsonConvert.DeserializeObject<dynamic>(json);
            var result = new List<object>();
            if (branches is IEnumerable<dynamic> branchList)
            {
                foreach (var b in branchList)
                    result.Add(new { name = (string?)b?.name ?? "" });
            }
            return Result<object>.Ok(result);
        }
        catch (Exception ex)
        {
            return Result<object>.Fail($"获取分支异常: {ex.Message}");
        }
    }

    /// <summary>获取平台 API 基础地址（从 URL 解析平台）</summary>
    private static string GetApiBase(string url)
    {
        var host = new Uri(url.TrimEnd('/')).Host.ToLower();
        return host switch
        {
            "github.com" => "https://api.github.com",
            "gitee.com" => "https://gitee.com/api/v5",
            _ => "https://gitee.com/api/v5"
        };
    }

    /// <summary>根据平台名获取 API 地址</summary>
    private static string GetPlatformApiBase(string platform) =>
        platform.ToLower() switch
        {
            "github" => "https://api.github.com",
            "gitee" => "https://gitee.com/api/v5",
            _ => "https://gitee.com/api/v5"
        };

    /// <summary>解析仓库地址，返回 (owner, slug, platform)</summary>
    private static (string owner, string slug, string platform)? ParseRepoUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            var uri = new Uri(url.TrimEnd('/'));
            var host = uri.Host.ToLower();
            var parts = uri.AbsolutePath.Trim('/').Split('/', 2);
            if (parts.Length < 2) return null;
            var platform = host switch
            {
                "github.com" => "github",
                "gitee.com" => "gitee",
                _ => "custom"
            };
            return (parts[0], parts[1], platform);
        }
        catch { return null; }
    }

    /// <summary>从平台 API 获取仓库信息</summary>
    private async Task<(string name, string description)?> FetchGiteeRepoInfoAsync(string platform, string owner, string repoSlug, string token)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var apiBase = GetPlatformApiBase(platform);
            if (!string.IsNullOrEmpty(token))
                client.DefaultRequestHeaders.Add("Authorization", $"token {token}");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            var resp = await client.GetAsync($"{apiBase}/repos/{owner}/{repoSlug}");
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var info = JsonConvert.DeserializeObject<dynamic>(json);
            // GitHub 和 Gitee 返回的字段名略有差异
            var name = (string?)info?.name ?? (string?)info?.full_name ?? repoSlug;
            return (name, (string?)info?.description ?? "");
        }
        catch { return null; }
    }

    /// <summary>从平台刷新仓库名称</summary>
    public async Task<Result<object>> RefreshRepoNameAsync(int repoId)
    {
        var repo = await GetByIdAsync(repoId);
        if (repo == null) return Result<object>.Fail("仓库不存在");

        var parsed = ParseRepoUrl(repo.RepoUrl);
        if (parsed == null) return Result<object>.Fail("仓库地址格式错误");
        var giteeInfo = await FetchGiteeRepoInfoAsync(parsed.Value.platform, repo.Owner, repo.RepoPath, repo.GiteeToken);
        if (giteeInfo == null)
            return Result<object>.Fail($"无法获取仓库信息，请检查仓库地址和 Token 是否正确");

        repo.RepoName = giteeInfo.Value.name;
        await _db.Updateable(repo).ExecuteCommandAsync();

        return Result<object>.Ok(new { name = repo.RepoName, owner = repo.Owner, path = repo.RepoPath });
    }

    /// <summary>根据仓库ID从数据库取Token并查询仓库名称（编辑时用）</summary>
    public async Task<Result<object>> FetchRepoNameAsync(int repoId, string? overrideUrl = null)
    {
        var repo = await GetByIdAsync(repoId);
        if (repo == null)
            return Result<object>.Fail("仓库不存在");

        var url = overrideUrl ?? repo.RepoUrl;
        var parsed = ParseRepoUrl(url);
        if (parsed == null)
            return Result<object>.Fail("仓库地址格式错误");

        var info = await FetchGiteeRepoInfoAsync(parsed.Value.platform, parsed.Value.owner, parsed.Value.slug, repo.GiteeToken);
        if (info == null)
            return Result<object>.Fail("无法获取仓库信息，请检查仓库地址和 Token 是否正确");

        return Result<object>.Ok(new {
            name = info.Value.name,
            owner = parsed.Value.owner,
            path = parsed.Value.slug
        });
    }

    private static DateTime? ParseGiteeDate(object? value)
    {
        if (value == null) return null;
        var str = value.ToString();
        if (string.IsNullOrWhiteSpace(str)) return null;
        if (DateTime.TryParse(str, out var dt)) return dt;
        return null;
    }

    private static string MaskToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return "***";
        return "******"; // 固定标记，UpdateAsync 通过此值判断"不更新 Token"
    }

    public async Task<string?> GetPlainGiteeTokenAsync(int repoId)
    {
        var repo = await GetByIdAsync(repoId);
        return repo?.GiteeToken; // GetByIdAsync already decrypts
    }
}

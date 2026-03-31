using CodeReview.Application.IService;
using CodeReview.Domain.Entities;
using Newtonsoft.Json;
using SqlSugar;
using System.Net.Http;
using System.Text;

namespace CodeReview.Application.Service;

public class ModelConfigService : IModelConfigService
{
    private readonly ISqlSugarClient _db;

    public ModelConfigService(ISqlSugarClient db) => _db = db;

    private static string FixApiBase(string baseUrl)
    {
        baseUrl = baseUrl.TrimEnd('/');
        if (!baseUrl.EndsWith("/v1")) baseUrl += "/v1";
        return baseUrl;
    }

    public async Task<List<ModelConfig>> GetListAsync()
    {
        var list = await _db.Queryable<ModelConfig>()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.CreateTime, OrderByType.Desc)
            .ToListAsync();

        // 列表返回时脱敏 API Key，只显示前4后4位
        foreach (var m in list)
            m.ApiKey = MaskApiKey(m.ApiKey);

        return list;
    }

    public async Task<ModelConfig?> GetByIdAsync(int id)
    {
        var model = await _db.Queryable<ModelConfig>().Where(x => x.Id == id && !x.IsDeleted).FirstAsync();
        if (model != null)
            model.ApiKey = CryptoHelper.Decrypt(model.ApiKey);
        return model;
    }

    public async Task<bool> AddAsync(ModelConfig model)
    {
        model.CreateTime = DateTime.Now;
        model.IsDeleted = false;
        if (string.IsNullOrEmpty(model.ApiKey) || model.ApiKey.Contains("***"))
            return false;
        model.ApiKey = CryptoHelper.Encrypt(model.ApiKey); // 加密存储
        return await _db.Insertable(model).ExecuteCommandAsync() > 0;
    }

    public async Task<bool> UpdateAsync(ModelConfig model)
    {
        if (model.ApiKey == "******")
        {
            // 标记值不更新，保持原密文
            var old = await _db.Queryable<ModelConfig>()
                .Where(x => x.Id == model.Id)
                .Select(x => x.ApiKey)
                .FirstAsync();
            model.ApiKey = old ?? model.ApiKey;
        }
        else
        {
            model.ApiKey = CryptoHelper.Encrypt(model.ApiKey);
        }
        return await _db.Updateable(model).ExecuteCommandAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int id) =>
        await _db.Updateable<ModelConfig>()
            .SetColumns(x => x.IsDeleted == true)
            .Where(x => x.Id == id)
            .ExecuteCommandAsync() > 0;

    public async Task<Result<object>> TestConnectionAsync(int modelConfigId)
    {
        var model = await GetByIdAsync(modelConfigId);
        if (model == null)
            return Result<object>.Fail("模型配置不存在");

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {model.ApiKey}");

            var body = new
            {
                model = model.Name,
                messages = new[]
                {
                    new { role = "user", content = "Hi, please reply with exactly one word: OK" }
                },
                max_tokens = 10,
                temperature = 0
            };

            var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync($"{FixApiBase(model.ApiBase)}/chat/completions", content);
            var text = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                return Result<object>.Fail($"调用失败 HTTP {resp.StatusCode}: {text[..Math.Min(200, text.Length)]}");

            return Result<object>.Ok(new { message = "模型连通正常", raw = text });
        }
        catch (Exception ex)
        {
            return Result<object>.Fail($"连接异常: {ex.Message}");
        }
    }

    private static string MaskApiKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length <= 8) return "***";
        return key[..4] + "***" + key[^4..];
    }
}

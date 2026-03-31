using System.Security.Cryptography;
using System.Text;

namespace CodeReview.Application;

/// <summary>
/// AES-256-CBC 加密解密工具
/// Key 从环境变量 AES_KEY 获取（优先）；未配置时使用硬编码默认密钥
/// </summary>
public static class CryptoHelper
{
    // ⚠️ 生产环境请更换为随机生成的32字符密钥，不要使用此默认值
    private static readonly byte[] _key;

    static CryptoHelper()
    {
        var envKey = Environment.GetEnvironmentVariable("AES_KEY");
        if (!string.IsNullOrEmpty(envKey) && envKey.Length >= 32)
        {
            _key = Encoding.UTF8.GetBytes(envKey[..32]);
        }
        else
        {
            // 硬编码默认密钥（32字节），请在生产环境通过 AES_KEY 环境变量替换
            _key = Encoding.UTF8.GetBytes("CodeReviewAES2024SecretKey32By".PadRight(32, 'X'));
        }
    }

    /// <summary>加密明文，返回 Base64 密文</summary>
    public static string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plain = Encoding.UTF8.GetBytes(plainText);
        var encrypted = encryptor.TransformFinalBlock(plain, 0, plain.Length);
        // IV 前置拼接到密文
        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
        return Convert.ToBase64String(result);
    }

    /// <summary>解密密文（Base64），返回明文</summary>
    public static string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return cipherText;
        try
        {
            var full = Convert.FromBase64String(cipherText);
            using var aes = Aes.Create();
            aes.Key = _key;
            // 提取前16字节 IV
            var iv = new byte[16];
            var encrypted = new byte[full.Length - 16];
            Buffer.BlockCopy(full, 0, iv, 0, 16);
            Buffer.BlockCopy(full, 16, encrypted, 0, encrypted.Length);
            aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return cipherText; // 兼容旧明文数据
        }
    }
}

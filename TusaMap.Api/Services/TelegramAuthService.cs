using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TusaMap.Api.Models;

namespace TusaMap.Api.Services;

public interface ITelegramAuthService
{
    TelegramUser? ValidateInitData(string? initData);
}

public class TelegramAuthService : ITelegramAuthService
{
    private readonly string _botToken;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TelegramAuthService(IConfiguration configuration)
    {
        _botToken = configuration["Telegram:BotToken"] ?? "";
    }

    public TelegramUser? ValidateInitData(string? initData)
    {
        if (string.IsNullOrEmpty(initData) || string.IsNullOrEmpty(_botToken))
            return null;

        var parsed = ParseQueryString(initData);
        if (!parsed.TryGetValue("hash", out var hash) || string.IsNullOrEmpty(hash))
            return null;

        var dataCheckString = string.Join("\n",
            parsed
                .Where(p => p.Key != "hash")
                .OrderBy(p => p.Key)
                .Select(p => $"{p.Key}={p.Value}"));

        using var hmac1 = new HMACSHA256(Encoding.UTF8.GetBytes("WebAppData"));
        var secretKey = hmac1.ComputeHash(Encoding.UTF8.GetBytes(_botToken));
        using var hmac2 = new HMACSHA256(secretKey);
        var computedHash = hmac2.ComputeHash(Encoding.UTF8.GetBytes(dataCheckString));
        var hashHex = Convert.ToHexString(computedHash).ToLowerInvariant();

        if (hash != hashHex)
            return null;

        var userJson = parsed.GetValueOrDefault("user");
        if (string.IsNullOrEmpty(userJson))
            return null;

        try
        {
            var user = JsonSerializer.Deserialize<TelegramUser>(userJson, JsonOptions);
            return user;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.Split('&'))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0) continue;
            var key = Uri.UnescapeDataString(pair[..eq].Replace('+', ' '));
            var value = Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
            dict[key] = value;
        }
        return dict;
    }
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace AiCicdAzureLab.Api.Services;

public sealed record CaptchaChallenge(Guid CaptchaId, string ImageSvg);

public sealed class CaptchaService
{
    private const string AllowedCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 5;
    private const int MaximumAttempts = 3;
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private readonly IMemoryCache cache;
    private readonly TimeProvider timeProvider;

    public CaptchaService(IMemoryCache cache, TimeProvider? timeProvider = null)
    {
        this.cache = cache;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public CaptchaChallenge Create()
    {
        var captchaId = Guid.NewGuid();
        var answer = CreateAnswer();
        var expiresAt = timeProvider.GetUtcNow().Add(Lifetime);

        cache.Set(
            GetCacheKey(captchaId),
            new CaptchaEntry(answer, MaximumAttempts, expiresAt),
            new MemoryCacheEntryOptions { AbsoluteExpiration = expiresAt });

        return new CaptchaChallenge(captchaId, CreateSvg(answer));
    }

    public bool Verify(Guid captchaId, string? answer)
    {
        if (captchaId == Guid.Empty || !cache.TryGetValue<CaptchaEntry>(GetCacheKey(captchaId), out var entry) || entry is null)
        {
            return false;
        }

        if (entry.ExpiresAt <= timeProvider.GetUtcNow())
        {
            Invalidate(captchaId);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(answer) &&
            string.Equals(entry.Answer, answer.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            Invalidate(captchaId);
            return true;
        }

        entry.RemainingAttempts--;
        if (entry.RemainingAttempts <= 0)
        {
            Invalidate(captchaId);
        }

        return false;
    }

    public void Invalidate(Guid captchaId)
    {
        if (captchaId != Guid.Empty)
        {
            cache.Remove(GetCacheKey(captchaId));
        }
    }

    private static string CreateAnswer()
    {
        var characters = new char[CodeLength];
        for (var index = 0; index < characters.Length; index++)
        {
            characters[index] = AllowedCharacters[RandomNumberGenerator.GetInt32(AllowedCharacters.Length)];
        }

        return new string(characters);
    }

    private static string CreateSvg(string answer)
    {
        var builder = new StringBuilder();
        builder.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"220\" height=\"72\" viewBox=\"0 0 220 72\" role=\"img\" aria-label=\"圖形驗證碼\">");
        builder.Append("<rect width=\"220\" height=\"72\" rx=\"8\" fill=\"#f3f7fb\"/>");

        for (var index = 0; index < 3; index++)
        {
            var y1 = RandomNumberGenerator.GetInt32(12, 60);
            var y2 = RandomNumberGenerator.GetInt32(12, 60);
            builder.Append($"<line x1=\"8\" y1=\"{y1}\" x2=\"212\" y2=\"{y2}\" stroke=\"#9aabc0\" stroke-width=\"1.5\" opacity=\"0.65\"/>");
        }

        for (var index = 0; index < answer.Length; index++)
        {
            var x = 24 + (index * 38);
            var y = RandomNumberGenerator.GetInt32(42, 56);
            var rotation = RandomNumberGenerator.GetInt32(-12, 13);
            var color = new[] { "#005a9e", "#614385", "#9a3d2f", "#146c43" }[RandomNumberGenerator.GetInt32(4)];
            builder.Append($"<text x=\"{x}\" y=\"{y}\" fill=\"{color}\" font-family=\"Arial, sans-serif\" font-size=\"34\" font-weight=\"700\" transform=\"rotate({rotation} {x} {y})\">{answer[index]}</text>");
        }

        builder.Append("</svg>");
        return builder.ToString();
    }

    private static string GetCacheKey(Guid captchaId) => $"captcha:{captchaId:N}";

    private sealed class CaptchaEntry(string answer, int remainingAttempts, DateTimeOffset expiresAt)
    {
        public string Answer { get; } = answer;
        public int RemainingAttempts { get; set; } = remainingAttempts;
        public DateTimeOffset ExpiresAt { get; } = expiresAt;
    }
}

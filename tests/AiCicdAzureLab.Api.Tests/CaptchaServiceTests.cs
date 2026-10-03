using System.Text.RegularExpressions;
using AiCicdAzureLab.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace AiCicdAzureLab.Api.Tests;

public class CaptchaServiceTests
{
    [Fact]
    public void Create_generates_five_allowed_characters_and_svg()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CaptchaService(cache);

        var challenge = service.Create();
        var answer = ExtractAnswer(challenge.ImageSvg);

        Assert.Matches("^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{5}$", answer);
        Assert.Contains("<svg", challenge.ImageSvg);
        Assert.Contains("<text", challenge.ImageSvg);
    }

    [Fact]
    public void Verify_accepts_case_insensitive_answer_once()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CaptchaService(cache);
        var challenge = service.Create();

        Assert.True(service.Verify(challenge.CaptchaId, ExtractAnswer(challenge.ImageSvg).ToLowerInvariant()));
        Assert.False(service.Verify(challenge.CaptchaId, ExtractAnswer(challenge.ImageSvg)));
    }

    [Fact]
    public void Verify_invalidates_challenge_after_three_failed_attempts()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new CaptchaService(cache);
        var challenge = service.Create();

        Assert.False(service.Verify(challenge.CaptchaId, "WRONG"));
        Assert.False(service.Verify(challenge.CaptchaId, "WRONG"));
        Assert.False(service.Verify(challenge.CaptchaId, "WRONG"));
        Assert.False(service.Verify(challenge.CaptchaId, ExtractAnswer(challenge.ImageSvg)));
    }

    [Fact]
    public void Verify_rejects_expired_and_invalidated_challenges()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var clock = new TestTimeProvider(DateTimeOffset.Parse("2026-10-03T00:00:00Z"));
        var service = new CaptchaService(cache, clock);
        var expiredChallenge = service.Create();
        var expiredAnswer = ExtractAnswer(expiredChallenge.ImageSvg);

        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.False(service.Verify(expiredChallenge.CaptchaId, expiredAnswer));

        var invalidatedChallenge = service.Create();
        service.Invalidate(invalidatedChallenge.CaptchaId);
        Assert.False(service.Verify(invalidatedChallenge.CaptchaId, ExtractAnswer(invalidatedChallenge.ImageSvg)));
    }

    private static string ExtractAnswer(string imageSvg) =>
        string.Concat(Regex.Matches(imageSvg, "<text[^>]*>([A-Z0-9])</text>")
            .Select(match => match.Groups[1].Value));

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}

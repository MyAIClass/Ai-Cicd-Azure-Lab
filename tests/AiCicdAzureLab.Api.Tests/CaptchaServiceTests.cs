using AiCicdAzureLab.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace AiCicdAzureLab.Api.Tests;

public class CaptchaServiceTests
{
    [Fact]
    public void Create_returns_a_token_and_an_svg_without_plain_text_answer()
    {
        var service = CreateService(() => "ABCDE");

        var challenge = service.Create();
        var image = service.RenderImage(challenge.Token);

        Assert.False(string.IsNullOrWhiteSpace(challenge.Token));
        Assert.Contains($"/api/captcha/{challenge.Token}/image", challenge.ImageUrl);
        Assert.StartsWith("<svg", image);
        Assert.Contains("<rect", image);
        Assert.DoesNotContain("<text", image, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ABCDE", image, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAndConsume_accepts_case_insensitive_answer_only_once()
    {
        var service = CreateService(() => "ABCDE");
        var challenge = service.Create();

        Assert.True(service.ValidateAndConsume(challenge.Token, "abcde"));
        Assert.False(service.ValidateAndConsume(challenge.Token, "abcde"));
    }

    [Fact]
    public void ValidateAndConsume_rejects_wrong_answer_and_consumes_the_challenge()
    {
        var service = CreateService(() => "ABCDE");
        var challenge = service.Create();

        Assert.False(service.ValidateAndConsume(challenge.Token, "WRONG"));
        Assert.False(service.ValidateAndConsume(challenge.Token, "ABCDE"));
    }

    [Fact]
    public async Task Expired_challenge_cannot_be_validated_or_rendered()
    {
        var service = CreateService(() => "ABCDE", TimeSpan.FromMilliseconds(50));
        var challenge = service.Create();
        await Task.Delay(150);

        Assert.False(service.ValidateAndConsume(challenge.Token, "ABCDE"));
        Assert.Throws<KeyNotFoundException>(() => service.RenderImage(challenge.Token));
    }

    private static CaptchaService CreateService(
        Func<string> codeGenerator,
        TimeSpan? lifetime = null) =>
        new(
            new MemoryCache(new MemoryCacheOptions()),
            codeGenerator,
            lifetime ?? CaptchaService.DefaultLifetime);
}

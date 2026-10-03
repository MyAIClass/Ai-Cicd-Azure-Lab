using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AiCicdAzureLab.Api.Tests;

public class CaptchaApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task Captcha_and_greeting_accept_a_correct_answer_once()
    {
        var challenge = await GetCaptchaAsync();
        var answer = ExtractAnswer(challenge.ImageSvg).ToLowerInvariant();

        var response = await client.PostAsJsonAsync("/api/greeting", new
        {
            name = "小明",
            captchaId = challenge.CaptchaId,
            captchaAnswer = answer
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("你好，小明！", await response.Content.ReadAsStringAsync());

        var repeatedResponse = await client.PostAsJsonAsync("/api/greeting", new
        {
            name = "小明",
            captchaId = challenge.CaptchaId,
            captchaAnswer = answer
        });
        Assert.Equal(HttpStatusCode.BadRequest, repeatedResponse.StatusCode);
    }

    [Fact]
    public async Task Greeting_rejects_invalid_or_missing_captcha_data()
    {
        var challenge = await GetCaptchaAsync();
        var invalidResponse = await client.PostAsJsonAsync("/api/greeting", new
        {
            name = "小明",
            captchaId = challenge.CaptchaId,
            captchaAnswer = "WRONG"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        var missingResponse = await client.PostAsJsonAsync("/api/greeting", new { name = "小明" });
        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);
    }

    [Fact]
    public async Task Requesting_a_new_captcha_invalidates_the_previous_one()
    {
        var firstChallenge = await GetCaptchaAsync();
        var secondChallenge = await GetCaptchaAsync(firstChallenge.CaptchaId);

        Assert.NotEqual(firstChallenge.CaptchaId, secondChallenge.CaptchaId);

        var response = await client.PostAsJsonAsync("/api/greeting", new
        {
            name = "小明",
            captchaId = firstChallenge.CaptchaId,
            captchaAnswer = ExtractAnswer(firstChallenge.ImageSvg)
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<CaptchaResponse> GetCaptchaAsync(Guid? previousCaptchaId = null)
    {
        var url = previousCaptchaId.HasValue
            ? $"/api/captcha?previousCaptchaId={previousCaptchaId.Value}"
            : "/api/captcha";
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var challenge = await response.Content.ReadFromJsonAsync<CaptchaResponse>();
        Assert.NotNull(challenge);
        Assert.NotEqual(Guid.Empty, challenge.CaptchaId);
        Assert.Contains("<svg", challenge.ImageSvg);
        return challenge;
    }

    private static string ExtractAnswer(string imageSvg) =>
        string.Concat(Regex.Matches(imageSvg, "<text[^>]*>([A-Z0-9])</text>")
            .Select(match => match.Groups[1].Value));

    private sealed record CaptchaResponse(Guid CaptchaId, string ImageSvg);
}

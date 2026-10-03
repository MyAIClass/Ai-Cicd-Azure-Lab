using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace AiCicdAzureLab.Api.Tests;

public class CaptchaApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task Captcha_endpoint_returns_dynamic_svg_without_plain_text_answer()
    {
        var challenge = await GetCaptchaAsync();
        var response = await client.GetAsync(challenge.ImageUrl);
        var image = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("<svg", image);
        Assert.DoesNotContain("<text", image, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Greeting_rejects_invalid_or_missing_captcha_data()
    {
        var invalidResponse = await client.PostAsJsonAsync("/api/greeting", new
        {
            name = "小明",
            captchaToken = "invalid",
            captchaAnswer = "WRONG"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        var missingResponse = await client.PostAsJsonAsync("/api/greeting", new { name = "小明" });
        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);
    }

    private async Task<CaptchaResponse> GetCaptchaAsync()
    {
        var challenge = await client.GetFromJsonAsync<CaptchaResponse>("/api/captcha");
        Assert.NotNull(challenge);
        Assert.False(string.IsNullOrWhiteSpace(challenge.Token));
        Assert.StartsWith("/api/captcha/", challenge.ImageUrl);
        return challenge;
    }

    private sealed record CaptchaResponse(string Token, string ImageUrl);
}

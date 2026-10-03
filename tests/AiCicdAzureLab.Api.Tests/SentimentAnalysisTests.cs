using System.Net;
using System.Net.Http.Json;
using System.Text;
using AiCicdAzureLab.Api.Services;
using Azure.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AiCicdAzureLab.Api.Tests;

public class SentimentAnalysisServiceTests
{
    [Fact]
    public async Task Local_demo_analyzer_returns_positive_result_without_azure()
    {
        var service = new LocalSentimentAnalysisService();

        var result = await service.AnalyzeAsync("服務很好，速度快速又方便");

        Assert.Equal("positive", result.Label);
        Assert.True(result.Polarity > 0);
        Assert.Contains("本機示範", result.Summary);
    }

    [Fact]
    public async Task Parses_and_validates_a_positive_result()
    {
        var client = CreateClient("{\"choices\":[{\"message\":{\"content\":\"{\\\"polarity\\\":0.8,\\\"label\\\":\\\"positive\\\",\\\"confidence\\\":0.95,\\\"summary\\\":\\\"服務令人滿意。\\\"}\"}}]}");
        var service = CreateService(client);

        var result = await service.AnalyzeAsync("服務很好");

        Assert.Equal(0.8, result.Polarity);
        Assert.Equal("positive", result.Label);
        Assert.Equal(0.95, result.Confidence);
        Assert.Equal("服務令人滿意。", result.Summary);
    }

    [Fact]
    public async Task Rejects_inconsistent_model_label()
    {
        var client = CreateClient("{\"choices\":[{\"message\":{\"content\":\"{\\\"polarity\\\":-0.8,\\\"label\\\":\\\"positive\\\",\\\"confidence\\\":0.9,\\\"summary\\\":\\\"內容負面。\\\"}\"}}]}");
        var service = CreateService(client);

        var exception = await Assert.ThrowsAsync<SentimentAnalysisException>(() => service.AnalyzeAsync("服務很差"));

        Assert.Contains("不一致", exception.Message);
    }

    [Fact]
    public async Task Converts_upstream_failure_to_safe_exception()
    {
        var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)));
        var service = CreateService(client);

        var exception = await Assert.ThrowsAsync<SentimentAnalysisException>(() => service.AnalyzeAsync("服務很差"));

        Assert.Equal("情感分析服務目前無法使用。", exception.Message);
    }

    private static SentimentAnalysisService CreateService(HttpClient client)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AZURE_OPENAI_ENDPOINT"] = "https://example.openai.azure.com",
                ["AZURE_OPENAI_DEPLOYMENT"] = "sentiment"
            })
            .Build();

        return new SentimentAnalysisService(client, new TestTokenCredential(), configuration, NullLogger<SentimentAnalysisService>.Instance);
    }

    private static HttpClient CreateClient(string responseBody) =>
        new(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        }));

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private sealed class TestTokenCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("test-token", DateTimeOffset.UtcNow.AddMinutes(5));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AccessToken("test-token", DateTimeOffset.UtcNow.AddMinutes(5)));
    }
}

public class SentimentApiTests : IClassFixture<SentimentApiFactory>
{
    private readonly HttpClient client;

    public SentimentApiTests(SentimentApiFactory factory)
    {
        client = factory.CreateClient();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("甲")]
    public async Task Rejects_empty_or_too_short_text(string? text)
    {
        var response = await client.PostAsJsonAsync("/api/sentiment/analyze", new { text });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rejects_overlong_text()
    {
        var response = await client.PostAsJsonAsync(
            "/api/sentiment/analyze",
            new { text = new string('甲', SentimentAnalysisService.MaxTextLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Returns_analysis_result()
    {
        var response = await client.PostAsJsonAsync("/api/sentiment/analyze", new { text = "服務很好" });
        var result = await response.Content.ReadFromJsonAsync<SentimentAnalysisResult>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("positive", result?.Label);
        Assert.Equal(0.8, result?.Polarity);
    }
}

public sealed class SentimentApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISentimentAnalysisService>();
            services.AddSingleton<ISentimentAnalysisService, FakeSentimentAnalysisService>();
        });
    }
}

public sealed class FakeSentimentAnalysisService : ISentimentAnalysisService
{
    public Task<SentimentAnalysisResult> AnalyzeAsync(string text, CancellationToken cancellationToken = default) =>
        Task.FromResult(new SentimentAnalysisResult(0.8, "positive", 0.95, "服務令人滿意。"));
}

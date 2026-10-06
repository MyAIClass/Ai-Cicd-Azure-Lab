using System.IO.Pipelines;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AiCicdAzureLab.Api.Services;
using Azure.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
    public async Task Sends_v1_chat_completion_request_with_foundry_token_scope()
    {
        Uri? requestUri = null;
        string? authorization = null;
        string? requestBody = null;
        var client = new HttpClient(new StubHandler(request =>
        {
            requestUri = request.RequestUri;
            authorization = request.Headers.Authorization?.ToString();
            requestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\":[{\"message\":{\"content\":\"{\\\"polarity\\\":0.8,\\\"label\\\":\\\"positive\\\",\\\"confidence\\\":0.95,\\\"summary\\\":\\\"服務令人滿意。\\\"}\"}}]}",
                    Encoding.UTF8,
                    "application/json")
            };
        }));
        var credential = new TestTokenCredential();
        var service = CreateService(client, credential);

        await service.AnalyzeAsync("服務很好");

        Assert.Equal("https://example.openai.azure.com/openai/v1/chat/completions", requestUri?.ToString());
        Assert.Equal("Bearer test-token", authorization);
        using var document = JsonDocument.Parse(requestBody!);
        Assert.Equal("sentiment", document.RootElement.GetProperty("model").GetString());
        Assert.Equal(1024, document.RootElement.GetProperty("max_completion_tokens").GetInt32());
        Assert.False(document.RootElement.TryGetProperty("temperature", out _));
        Assert.Equal(new[] { "https://ai.azure.com/.default" }, credential.RequestedScopes);
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

    private static SentimentAnalysisService CreateService(HttpClient client, TestTokenCredential? credential = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AZURE_OPENAI_ENDPOINT"] = "https://example.openai.azure.com",
                ["AZURE_OPENAI_DEPLOYMENT"] = "sentiment"
            })
            .Build();

        return new SentimentAnalysisService(client, credential ?? new TestTokenCredential(), configuration, NullLogger<SentimentAnalysisService>.Instance);
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
        public string[] RequestedScopes { get; private set; } = [];

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("test-token", DateTimeOffset.UtcNow.AddMinutes(5));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            RequestedScopes = requestContext.Scopes.ToArray();
            return ValueTask.FromResult(new AccessToken("test-token", DateTimeOffset.UtcNow.AddMinutes(5)));
        }
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

public class SentimentApiRateLimitTests : IClassFixture<SentimentApiFactory>
{
    private readonly HttpClient client;

    public SentimentApiRateLimitTests(SentimentApiFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Limits_sentiment_requests_per_client()
    {
        for (var requestNumber = 0; requestNumber < 10; requestNumber++)
        {
            var response = await client.PostAsJsonAsync("/api/sentiment/analyze", new { text = "服務很好" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var rejectedResponse = await client.PostAsJsonAsync("/api/sentiment/analyze", new { text = "服務很好" });

        Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
    }

    [Fact]
    public async Task Client_rejections_do_not_consume_application_instance_quota()
    {
        using var factory = new SentimentApiFactory();

        for (var requestNumber = 0; requestNumber < 10; requestNumber++)
        {
            var statusCode = await AnalyzeFrom(factory, "192.0.2.1");
            Assert.Equal(HttpStatusCode.OK, statusCode);
        }

        for (var requestNumber = 0; requestNumber < 15; requestNumber++)
        {
            var statusCode = await AnalyzeFrom(factory, "192.0.2.1");
            Assert.Equal(HttpStatusCode.TooManyRequests, statusCode);
        }

        for (var requestNumber = 0; requestNumber < 10; requestNumber++)
        {
            var statusCode = await AnalyzeFrom(factory, "192.0.2.2");
            Assert.Equal(HttpStatusCode.OK, statusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, await AnalyzeFrom(factory, "192.0.2.2"));
    }

    [Fact]
    public async Task Limits_sentiment_requests_per_application_instance()
    {
        using var factory = new SentimentApiFactory();
        using var client = factory.CreateClient();
        var globalLimiter = factory.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Value.GlobalLimiter;
        Assert.NotNull(globalLimiter);

        for (var requestNumber = 0; requestNumber < 25; requestNumber++)
        {
            var remoteIpAddress = $"192.0.2.{requestNumber + 1}";
            using var lease = await globalLimiter.AcquireAsync(CreateSentimentRequestContext(remoteIpAddress));
            Assert.True(lease.IsAcquired);
        }

        using var rejectedLease = await globalLimiter.AcquireAsync(CreateSentimentRequestContext("192.0.2.100"));

        Assert.False(rejectedLease.IsAcquired);
    }

    private static async Task<HttpStatusCode> AnalyzeFrom(SentimentApiFactory factory, string remoteIpAddress)
    {
        var requestBody = Encoding.UTF8.GetBytes("{\"text\":\"服務很好\"}");
        using var requestStream = new MemoryStream(requestBody);
        var requestBodyReader = PipeReader.Create(requestStream, new StreamPipeReaderOptions(leaveOpen: true));
        var context = await factory.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIpAddress);
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = "/api/sentiment/analyze";
            context.Request.ContentType = "application/json";
            context.Request.ContentLength = requestBody.Length;
            context.Request.Body = requestStream;
            context.Features.Set<IRequestBodyPipeFeature>(
                new TestRequestBodyPipeFeature(requestBodyReader));
            context.Features.Set<IHttpRequestBodyDetectionFeature>(new TestRequestBodyDetectionFeature());
        });

        await requestBodyReader.CompleteAsync();
        return (HttpStatusCode)context.Response.StatusCode;
    }

    private static HttpContext CreateSentimentRequestContext(string remoteIpAddress)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/sentiment/analyze";
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIpAddress);
        return context;
    }

    private sealed class TestRequestBodyPipeFeature(PipeReader reader) : IRequestBodyPipeFeature
    {
        public PipeReader Reader { get; } = reader;
    }

    private sealed class TestRequestBodyDetectionFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
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

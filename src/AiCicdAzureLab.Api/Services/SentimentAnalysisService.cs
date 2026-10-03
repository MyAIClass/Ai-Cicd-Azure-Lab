using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;

namespace AiCicdAzureLab.Api.Services;

public interface ISentimentAnalysisService
{
    Task<SentimentAnalysisResult> AnalyzeAsync(string text, CancellationToken cancellationToken = default);
}

public sealed class SentimentAnalysisService : ISentimentAnalysisService
{
    public const int MinTextLength = 2;
    public const int MaxTextLength = 500;
    private const string CognitiveServicesScope = "https://cognitiveservices.azure.com/.default";
    private readonly HttpClient httpClient;
    private readonly TokenCredential credential;
    private readonly IConfiguration configuration;
    private readonly ILogger<SentimentAnalysisService> logger;

    public SentimentAnalysisService(HttpClient httpClient, TokenCredential credential, IConfiguration configuration, ILogger<SentimentAnalysisService> logger)
    {
        this.httpClient = httpClient;
        this.credential = credential;
        this.configuration = configuration;
        this.logger = logger;
    }

    public async Task<SentimentAnalysisResult> AnalyzeAsync(string text, CancellationToken cancellationToken = default)
    {
        var endpoint = configuration["AZURE_OPENAI_ENDPOINT"]?.TrimEnd('/');
        var deployment = configuration["AZURE_OPENAI_DEPLOYMENT"];
        var apiVersion = configuration["AZURE_OPENAI_API_VERSION"] ?? "2024-10-21";
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment))
        {
            throw new SentimentAnalysisException("情感分析服務尚未設定。", isConfigurationError: true);
        }

        AccessToken token;
        try
        {
            token = await credential.GetTokenAsync(new TokenRequestContext([CognitiveServicesScope]), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Unable to obtain Azure OpenAI access token.");
            throw new SentimentAnalysisException("無法連線至情感分析服務。", exception);
        }

        var requestUri = $"{endpoint}/openai/deployments/{Uri.EscapeDataString(deployment)}/chat/completions?api-version={Uri.EscapeDataString(apiVersion)}";
        var requestBody = new
        {
            messages = new object[]
            {
                new { role = "system", content = "你是繁體中文評論情感分析器。只輸出 JSON，不要 Markdown 或其他文字。JSON 必須包含 polarity（-1 到 1 的數字）、label（negative、neutral 或 positive）、confidence（0 到 1 的數字）、summary（不超過 80 字的繁體中文簡述）。polarity <= -0.2 時 label 必須是 negative，polarity >= 0.2 時必須是 positive，其餘為 neutral。" },
                new { role = "user", content = text }
            },
            temperature = 0,
            response_format = new { type = "json_object" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "Azure OpenAI sentiment request failed.");
            throw new SentimentAnalysisException("情感分析服務目前無法使用。", exception);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Azure OpenAI sentiment request returned HTTP {StatusCode}.", response.StatusCode);
            throw new SentimentAnalysisException("情感分析服務目前無法使用。");
        }

        try
        {
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            var content = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            var modelResult = JsonSerializer.Deserialize<ModelSentiment>(content ?? "", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return ValidateAndCreateResult(modelResult);
        }
        catch (SentimentAnalysisException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Azure OpenAI returned an invalid sentiment response.");
            throw new SentimentAnalysisException("情感分析服務回傳了無效結果。", exception);
        }
    }

    private static SentimentAnalysisResult ValidateAndCreateResult(ModelSentiment? result)
    {
        if (result is null || !double.IsFinite(result.Polarity) || result.Polarity is < -1 or > 1 ||
            !double.IsFinite(result.Confidence) || result.Confidence is < 0 or > 1 ||
            string.IsNullOrWhiteSpace(result.Label) || string.IsNullOrWhiteSpace(result.Summary) || result.Summary.Length > 80)
        {
            throw new SentimentAnalysisException("情感分析服務回傳了無效結果。");
        }

        var expectedLabel = result.Polarity <= -0.2 ? "negative" : result.Polarity >= 0.2 ? "positive" : "neutral";
        if (!string.Equals(result.Label, expectedLabel, StringComparison.OrdinalIgnoreCase))
        {
            throw new SentimentAnalysisException("情感分析服務回傳了不一致結果。");
        }

        return new SentimentAnalysisResult(result.Polarity, expectedLabel, result.Confidence, result.Summary.Trim());
    }

    private sealed record ModelSentiment(double Polarity, string? Label, double Confidence, string? Summary);
}

public sealed record SentimentAnalysisResult(double Polarity, string Label, double Confidence, string Summary);

public sealed class SentimentAnalysisException : Exception
{
    public SentimentAnalysisException(string message, Exception? innerException = null, bool isConfigurationError = false) : base(message, innerException)
    {
        IsConfigurationError = isConfigurationError;
    }

    public bool IsConfigurationError { get; }
}

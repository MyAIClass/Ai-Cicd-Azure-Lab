using AiCicdAzureLab.Api.Services;
using Azure.Core;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    var corsAllowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .GetChildren()
        .Select(section => section.Value?.Trim())
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Select(origin => origin!.TrimEnd('/'))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    policy
        .WithOrigins(corsAllowedOrigins)
        .WithMethods("GET", "POST")
        .WithHeaders("Content-Type");
}));
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CaptchaService>();
var azureOpenAiConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["AZURE_OPENAI_ENDPOINT"])
    && !string.IsNullOrWhiteSpace(builder.Configuration["AZURE_OPENAI_DEPLOYMENT"]);

if (builder.Environment.IsDevelopment() && !azureOpenAiConfigured)
{
    builder.Services.AddSingleton<ISentimentAnalysisService, LocalSentimentAnalysisService>();
}
else
{
    builder.Services.AddSingleton<TokenCredential, DefaultAzureCredential>();
    builder.Services.AddHttpClient<ISentimentAnalysisService, SentimentAnalysisService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(15);
    });
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseRouting();
app.UseCors("Frontend");
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health")
    .WithTags("System");

app.MapPost("/api/greeting", (GreetingRequest? request, CaptchaService captchaService) =>
{
    if (request is null)
    {
        return Results.BadRequest(new { error = "請提供完整的請求內容。" });
    }

    if (!GreetingService.IsValidName(request.Name))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["name"] = [$"名稱長度不可超過 {GreetingService.MaxNameLength} 個字元。"]
        });
    }

    if (!captchaService.ValidateAndConsume(request.CaptchaToken, request.CaptchaAnswer))
    {
        return Results.BadRequest(new { error = "驗證碼錯誤或已過期，請重新取得驗證碼。" });
    }

    return Results.Ok(GreetingService.Create(request.Name));
})
    .WithName("Greeting")
    .WithTags("Demo");

app.MapGet("/api/greeting", () => Results.StatusCode(StatusCodes.Status405MethodNotAllowed))
    .ExcludeFromDescription();

app.MapGet("/api/captcha", (CaptchaService captchaService) =>
    Results.Ok(captchaService.Create()))
    .WithName("CreateCaptcha")
    .WithTags("Security");

app.MapGet("/api/captcha/{token}/image", (string token, CaptchaService captchaService) =>
{
    try
    {
        return Results.Content(captchaService.RenderImage(token), "image/svg+xml");
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
})
    .WithName("CaptchaImage")
    .WithTags("Security");

app.MapGet("/api/challenge", () =>
    Results.Ok(ChallengeService.Draw()))
    .WithName("Challenge")
    .WithTags("Demo");

app.MapGet("/api/challenges", () =>
    Results.Ok(ChallengeService.GetCandidates()))
    .WithName("ChallengeCandidates")
    .WithTags("Demo");

app.MapGet("/api/daily-quote", () =>
    Results.Ok(DailyQuoteService.Create(DateOnly.FromDateTime(DateTime.UtcNow))))
    .WithName("DailyQuote")
    .WithTags("Demo");

app.MapPost("/api/sentiment/analyze", async (
    SentimentRequest? request,
    ISentimentAnalysisService sentimentService,
    CancellationToken cancellationToken) =>
{
    var text = request?.Text?.Trim();
    if (string.IsNullOrWhiteSpace(text) || text.Length < SentimentAnalysisService.MinTextLength)
    {
        return Results.BadRequest(new { error = "評論至少需要 2 個字元。" });
    }

    if (text.Length > SentimentAnalysisService.MaxTextLength)
    {
        return Results.BadRequest(new { error = $"評論不可超過 {SentimentAnalysisService.MaxTextLength} 個字元。" });
    }

    try
    {
        return Results.Ok(await sentimentService.AnalyzeAsync(text, cancellationToken));
    }
    catch (SentimentAnalysisException exception)
    {
        return exception.IsConfigurationError
            ? Results.Problem("情感分析服務尚未完成設定。", statusCode: StatusCodes.Status503ServiceUnavailable)
            : Results.Problem("情感分析服務目前無法使用。", statusCode: StatusCodes.Status502BadGateway);
    }
})
    .WithName("AnalyzeSentiment")
    .WithTags("AI");

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;

public sealed record GreetingRequest(
    string? Name,
    string? CaptchaToken,
    string? CaptchaAnswer);

public sealed record SentimentRequest(string? Text);

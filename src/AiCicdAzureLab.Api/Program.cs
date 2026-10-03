using AiCicdAzureLab.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CaptchaService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health")
    .WithTags("System");

app.MapGet("/api/captcha", (Guid? previousCaptchaId, CaptchaService captchaService) =>
{
    if (previousCaptchaId.HasValue)
    {
        captchaService.Invalidate(previousCaptchaId.Value);
    }

    return Results.Ok(captchaService.Create());
})
    .WithName("Captcha")
    .WithTags("Demo");

app.MapPost("/api/greeting", (GreetingRequest request, CaptchaService captchaService) =>
{
    if (!captchaService.Verify(request.CaptchaId, request.CaptchaAnswer))
    {
        return Results.BadRequest(new { error = "驗證碼無效、已過期或嘗試次數已用盡。" });
    }

    return Results.Ok(GreetingService.Create(request.Name));
})
    .WithName("Greeting")
    .WithTags("Demo");

app.MapGet("/api/challenge", () =>
    Results.Ok(ChallengeService.Draw()))
    .WithName("Challenge")
    .WithTags("Demo");

app.MapGet("/api/daily-quote", () =>
    Results.Ok(DailyQuoteService.Create(DateOnly.FromDateTime(DateTime.UtcNow))))
    .WithName("DailyQuote")
    .WithTags("Demo");

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;

public sealed record GreetingRequest(string? Name, Guid CaptchaId, string? CaptchaAnswer);

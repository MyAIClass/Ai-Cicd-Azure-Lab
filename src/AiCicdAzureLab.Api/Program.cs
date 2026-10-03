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

app.MapGet("/api/daily-quote", () =>
    Results.Ok(DailyQuoteService.Create(DateOnly.FromDateTime(DateTime.UtcNow))))
    .WithName("DailyQuote")
    .WithTags("Demo");

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;

public sealed record GreetingRequest(
    string? Name,
    string? CaptchaToken,
    string? CaptchaAnswer);

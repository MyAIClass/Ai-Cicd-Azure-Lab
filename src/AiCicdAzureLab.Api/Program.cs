using AiCicdAzureLab.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("Health")
    .WithTags("System");

app.MapGet("/api/greeting", (string? name) =>
    Results.Ok(GreetingService.Create(name)))
    .WithName("Greeting")
    .WithTags("Demo");

app.MapGet("/api/challenge", () =>
    Results.Ok(ChallengeService.Draw()))
    .WithName("Challenge")
    .WithTags("Demo");

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;

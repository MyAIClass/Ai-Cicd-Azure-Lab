namespace AiCicdAzureLab.Api.Services;

public sealed record GreetingResponse(
    string Message,
    string Service,
    DateTimeOffset Timestamp);

public static class GreetingService
{
    public const int MaxNameLength = 50;

    public static bool IsValidName(string? name) =>
        string.IsNullOrWhiteSpace(name) || name.Trim().Length <= MaxNameLength;

    public static GreetingResponse Create(string? name)
    {
        var displayName = string.IsNullOrWhiteSpace(name)
            ? "課程學員"
            : name.Trim();

        return new GreetingResponse(
            Message: $"你好，{displayName}！",
            Service: "ai-cicd-azure-lab",
            Timestamp: DateTimeOffset.UtcNow);
    }
}

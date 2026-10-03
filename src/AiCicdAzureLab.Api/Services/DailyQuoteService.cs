namespace AiCicdAzureLab.Api.Services;

public sealed record DailyQuoteResponse(
    DateOnly Date,
    string Quote,
    string Service);

public static class DailyQuoteService
{
    private static readonly string[] Quotes =
    [
        "嗨，打起精神，你是最棒的！",
        "今天也比昨天更靠近目標一點點。",
        "先完成，再完美；你已經做得很好了。"
    ];

    public static DailyQuoteResponse Create(DateOnly date)
    {
        var quote = Quotes[date.DayOfYear % Quotes.Length];

        return new DailyQuoteResponse(
            Date: date,
            Quote: quote,
            Service: "ai-cicd-azure-lab");
    }
}

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
        "先完成，再完美；你已經做得很好了。",
        "每一次練習，都是替未來的自己累積力量。",
        "遇到問題不可怕，願意動手嘗試就是突破的開始。",
        "把大目標拆成小步驟，前進就會變得很踏實。",
        "相信自己的節奏，穩穩走也能走得很遠。",
        "今天的努力不一定立刻開花，但一定正在扎根。",
        "保持好奇、勇敢提問，你會發現更多可能。",
        "你不需要一次做到完美，只要比昨天更進步。"
    ];

    public static DailyQuoteResponse Create(DateOnly date)
    {
        var quote = Quotes[Random.Shared.Next(Quotes.Length)];

        return new DailyQuoteResponse(
            Date: date,
            Quote: quote,
            Service: "ai-cicd-azure-lab");
    }
}
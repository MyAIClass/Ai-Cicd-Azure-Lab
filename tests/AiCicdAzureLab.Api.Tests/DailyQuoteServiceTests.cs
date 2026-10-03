using AiCicdAzureLab.Api.Services;
using Xunit;

namespace AiCicdAzureLab.Api.Tests;

public class DailyQuoteServiceTests
{
    [Fact]
    public void Create_returns_a_quote_for_the_requested_date()
    {
        var date = new DateOnly(2026, 10, 3);

        var response = DailyQuoteService.Create(date);

        Assert.Equal(date, response.Date);
        Assert.Equal("ai-cicd-azure-lab", response.Service);
        Assert.Contains(response.Quote, new[]
        {
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
        });
    }

    [Fact]
    public void Create_returns_quotes_from_the_ten_quote_pool()
    {
        var date = new DateOnly(2026, 10, 3);

        var quotes = Enumerable.Range(0, 100)
            .Select(_ => DailyQuoteService.Create(date).Quote)
            .ToHashSet();

        Assert.NotEmpty(quotes);
        Assert.True(quotes.Count <= 10);
    }
}
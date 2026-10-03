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
            "先完成，再完美；你已經做得很好了。"
        });
    }

    [Fact]
    public void Create_returns_the_same_quote_for_the_same_date()
    {
        var date = new DateOnly(2026, 10, 3);

        var firstResponse = DailyQuoteService.Create(date);
        var secondResponse = DailyQuoteService.Create(date);

        Assert.Equal(firstResponse, secondResponse);
    }
}

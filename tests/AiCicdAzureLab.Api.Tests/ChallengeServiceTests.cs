using AiCicdAzureLab.Api.Services;
using Xunit;

namespace AiCicdAzureLab.Api.Tests;

public class ChallengeServiceTests
{
    [Fact]
    public void Draw_returns_a_challenge_with_title_and_description()
    {
        var response = ChallengeService.Draw();

        Assert.False(string.IsNullOrWhiteSpace(response.Title));
        Assert.False(string.IsNullOrWhiteSpace(response.Description));
    }
}

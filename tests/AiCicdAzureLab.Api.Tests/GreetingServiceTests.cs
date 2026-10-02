using AiCicdAzureLab.Api.Services;
using Xunit;

namespace AiCicdAzureLab.Api.Tests;

public class GreetingServiceTests
{
    [Fact]
    public void Create_uses_default_name_when_name_is_empty()
    {
        var response = GreetingService.Create(" ");

        Assert.Equal("你好，課程學員！", response.Message);
        Assert.Equal("ai-cicd-azure-lab", response.Service);
    }

    [Fact]
    public void Create_trims_and_uses_custom_name()
    {
        var response = GreetingService.Create("  小明  ");

        Assert.Equal("你好，小明！", response.Message);
    }
}

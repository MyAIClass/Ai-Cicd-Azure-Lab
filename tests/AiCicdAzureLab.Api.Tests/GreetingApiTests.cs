using System.Net;
using System.Net.Http.Json;
using AiCicdAzureLab.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace AiCicdAzureLab.Api.Tests;

public class GreetingApiTests : IClassFixture<GreetingApiFactory>
{
    private readonly HttpClient client;

    public GreetingApiTests(GreetingApiFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Post_without_captcha_returns_bad_request()
    {
        var response = await client.PostAsJsonAsync("/api/greeting", new { name = "小明" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Legacy_get_greeting_is_not_available()
    {
        var response = await client.GetAsync("/api/greeting?name=小明");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Fact]
    public async Task Post_with_correct_captcha_returns_greeting_and_consumes_captcha()
    {
        var captcha = await GetCaptcha();

        var response = await client.PostAsJsonAsync(
            "/api/greeting",
            new GreetingRequest("小明", captcha.Token, "abcde"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var greeting = await response.Content.ReadFromJsonAsync<GreetingResponse>();
        Assert.Equal("你好，小明！", greeting?.Message);

        var reusedResponse = await client.PostAsJsonAsync(
            "/api/greeting",
            new GreetingRequest("小明", captcha.Token, "ABCDE"));

        Assert.Equal(HttpStatusCode.BadRequest, reusedResponse.StatusCode);
    }

    [Fact]
    public async Task Post_with_wrong_captcha_returns_bad_request()
    {
        var captcha = await GetCaptcha();

        var response = await client.PostAsJsonAsync(
            "/api/greeting",
            new GreetingRequest("小明", captcha.Token, "WRONG"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Captcha_endpoint_returns_svg_image()
    {
        var captcha = await GetCaptcha();

        var imageResponse = await client.GetAsync(captcha.ImageUrl);
        var image = await imageResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, imageResponse.StatusCode);
        Assert.Equal("image/svg+xml", imageResponse.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("<svg", image);
    }

    [Fact]
    public async Task Post_with_overlong_name_returns_bad_request()
    {
        var name = new string('甲', GreetingService.MaxNameLength + 1);

        var response = await client.PostAsJsonAsync(
            "/api/greeting",
            new GreetingRequest(name, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<CaptchaChallenge> GetCaptcha()
    {
        var captcha = await client.GetFromJsonAsync<CaptchaChallenge>("/api/captcha");
        Assert.NotNull(captcha);
        return captcha!;
    }
}

public sealed class GreetingApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<CaptchaService>();
            services.AddSingleton(serviceProvider => new CaptchaService(
                serviceProvider.GetRequiredService<IMemoryCache>(),
                () => "ABCDE",
                CaptchaService.DefaultLifetime));
        });
    }
}

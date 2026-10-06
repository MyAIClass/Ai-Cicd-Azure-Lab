using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AiCicdAzureLab.Api.Tests;

public class CorsApiTests : IClassFixture<CorsApiFactory>
{
    private readonly HttpClient client;

    public CorsApiTests(CorsApiFactory factory)
    {
        client = factory.CreateClient();
    }

    [Theory]
    [InlineData("https://myaiclass.github.io")]
    [InlineData("https://student.github.io")]
    public async Task Health_endpoint_allows_configured_pages_origins(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", origin);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedOrigins),
            $"CORS response header was missing. Response headers: {response.Headers}");
        Assert.Equal(origin, Assert.Single(allowedOrigins!));
    }

    [Fact]
    public async Task Health_endpoint_does_not_allow_unconfigured_origins()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "https://untrusted.example");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Json_post_preflight_allows_post_and_content_type_for_configured_origin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/greeting");
        request.Headers.Add("Origin", "https://student.github.io");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "https://student.github.io",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        var allowedMethods = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods"));
        Assert.Contains("POST", allowedMethods.Split(',', StringSplitOptions.TrimEntries));
        var allowedHeaders = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers"));
        Assert.Contains(
            "Content-Type",
            allowedHeaders.Split(',', StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }
}

public sealed class CorsApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = "https://myaiclass.github.io",
                ["Cors:AllowedOrigins:1"] = "https://student.github.io"
            }));
    }
}

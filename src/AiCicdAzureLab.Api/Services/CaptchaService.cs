using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace AiCicdAzureLab.Api.Services;

public sealed record CaptchaChallenge(string Token, string ImageUrl);

public sealed class CaptchaService
{
    public const int CodeLength = 5;
    public static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(5);

    private const string AllowedCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly IMemoryCache cache;
    private readonly Func<string> codeGenerator;
    private readonly TimeSpan lifetime;
    private readonly object validationLock = new();

    public CaptchaService(IMemoryCache cache)
        : this(cache, CreateCode, DefaultLifetime)
    {
    }

    public CaptchaService(IMemoryCache cache, Func<string> codeGenerator, TimeSpan lifetime)
    {
        this.cache = cache;
        this.codeGenerator = codeGenerator;
        this.lifetime = lifetime;
    }

    public CaptchaChallenge Create()
    {
        var token = Guid.NewGuid().ToString("N");
        var code = codeGenerator();

        cache.Set(GetCacheKey(token), code, lifetime);

        return new CaptchaChallenge(
            Token: token,
            ImageUrl: $"/api/captcha/{token}/image");
    }

    public bool ValidateAndConsume(string? token, string? answer)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(answer))
        {
            return false;
        }

        lock (validationLock)
        {
            if (!cache.TryGetValue<string>(GetCacheKey(token), out var expectedAnswer)
                || string.IsNullOrWhiteSpace(expectedAnswer))
            {
                return false;
            }

            // 驗證碼只允許使用一次；即使答錯也消耗，避免反覆猜測同一張圖。
            cache.Remove(GetCacheKey(token));

            return string.Equals(expectedAnswer, answer.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    public string RenderImage(string token)
    {
        if (!cache.TryGetValue<string>(GetCacheKey(token), out var answer)
            || string.IsNullOrWhiteSpace(answer))
        {
            throw new KeyNotFoundException("Captcha has expired or does not exist.");
        }

        return CaptchaSvgRenderer.Render(answer);
    }

    private static string GetCacheKey(string token) => $"captcha:{token}";

    private static string CreateCode() => string.Concat(
        Enumerable.Range(0, CodeLength)
            .Select(_ => AllowedCharacters[RandomNumberGenerator.GetInt32(AllowedCharacters.Length)]));
}

internal static class CaptchaSvgRenderer
{
    private static readonly IReadOnlyDictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        ['2'] = ["11110", "00001", "00010", "00100", "01000", "10000", "11111"],
        ['3'] = ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
        ['4'] = ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
        ['5'] = ["11111", "10000", "10000", "11110", "00001", "00001", "11110"],
        ['6'] = ["01110", "10000", "10000", "11110", "10001", "10001", "01110"],
        ['7'] = ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
        ['8'] = ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
        ['9'] = ["01110", "10001", "10001", "01111", "00001", "00001", "01110"],
        ['A'] = ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
        ['B'] = ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
        ['C'] = ["01111", "10000", "10000", "10000", "10000", "10000", "01111"],
        ['D'] = ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
        ['E'] = ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
        ['F'] = ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
        ['G'] = ["01111", "10000", "10000", "10111", "10001", "10001", "01111"],
        ['H'] = ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
        ['J'] = ["00001", "00001", "00001", "00001", "10001", "10001", "01110"],
        ['K'] = ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
        ['L'] = ["10000", "10000", "10000", "10000", "10000", "10000", "11111"],
        ['M'] = ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
        ['N'] = ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
        ['P'] = ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
        ['Q'] = ["01110", "10001", "10001", "10001", "10101", "10010", "01101"],
        ['R'] = ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
        ['S'] = ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
        ['T'] = ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
        ['U'] = ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
        ['V'] = ["10001", "10001", "10001", "10001", "10001", "01010", "00100"],
        ['W'] = ["10001", "10001", "10001", "10101", "10101", "11011", "10001"],
        ['X'] = ["10001", "10001", "01010", "00100", "01010", "10001", "10001"],
        ['Y'] = ["10001", "10001", "01010", "00100", "00100", "00100", "00100"],
        ['Z'] = ["11111", "00001", "00010", "00100", "01000", "10000", "11111"]
    };

    public static string Render(string answer)
    {
        var random = Random.Shared;
        var svg = new StringBuilder()
            .Append("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 260 90\" role=\"img\" aria-label=\"驗證碼圖片\">")
            .Append("<rect width=\"260\" height=\"90\" rx=\"10\" fill=\"#f3f4f6\"/>");

        for (var i = 0; i < 8; i++)
        {
            svg.Append($"<path d=\"M {random.Next(0, 250)} {random.Next(5, 85)} Q {random.Next(40, 220)} {random.Next(0, 90)} {random.Next(10, 260)} {random.Next(5, 85)}\" stroke=\"#{random.Next(0x777777, 0xdddddd):x6}\" fill=\"none\" stroke-width=\"{random.Next(1, 3)}\"/>");
        }

        for (var i = 0; i < 45; i++)
        {
            svg.Append($"<circle cx=\"{random.Next(5, 255)}\" cy=\"{random.Next(5, 85)}\" r=\"{random.Next(1, 4)}\" fill=\"#{random.Next(0x888888, 0xdddddd):x6}\"/>");
        }

        for (var characterIndex = 0; characterIndex < answer.Length; characterIndex++)
        {
            var glyph = Glyphs[answer[characterIndex]];
            var originX = 18 + characterIndex * 47;
            var originY = random.Next(16, 25);
            var rotation = random.Next(-12, 13);
            var color = $"#{random.Next(0x172554, 0x7c2d12):x6}";

            svg.Append($"<g transform=\"translate({originX} {originY}) rotate({rotation} 13 25)\" fill=\"{color}\">");
            for (var row = 0; row < glyph.Length; row++)
            {
                for (var column = 0; column < glyph[row].Length; column++)
                {
                    if (glyph[row][column] == '1')
                    {
                        svg.Append($"<rect x=\"{column * 5}\" y=\"{row * 7}\" width=\"5\" height=\"7\" rx=\"1\"/>");
                    }
                }
            }

            svg.Append("</g>");
        }

        return svg.Append("</svg>").ToString();
    }
}

namespace AiCicdAzureLab.Api.Services;

/// <summary>
/// Development-only fallback so the UI can be demonstrated without Azure OpenAI.
/// This is intentionally simple and is not intended for production sentiment analysis.
/// </summary>
public sealed class LocalSentimentAnalysisService : ISentimentAnalysisService
{
    private static readonly string[] PositiveWords =
    [
        "好", "棒", "讚", "喜歡", "滿意", "開心", "快速", "方便", "推薦", "優秀", "成功", "感謝", "謝謝", "完美", "愉快"
    ];

    private static readonly string[] NegativeWords =
    [
        "差", "糟", "爛", "討厭", "失望", "生氣", "憤怒", "慢", "麻煩", "錯誤", "失敗", "抱怨", "浪費", "不滿", "問題"
    ];

    public Task<SentimentAnalysisResult> AnalyzeAsync(string text, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var positiveCount = PositiveWords.Count(text.Contains);
        var negativeCount = NegativeWords.Count(text.Contains);
        var difference = positiveCount - negativeCount;
        var polarity = Math.Clamp(difference * 0.35, -1, 1);
        var label = polarity <= -0.2 ? "negative" : polarity >= 0.2 ? "positive" : "neutral";
        var confidence = difference == 0 ? 0.55 : Math.Min(0.95, 0.65 + Math.Abs(difference) * 0.1);
        var summary = label switch
        {
            "positive" => "本機示範分析判斷這段評論帶有正向情緒。",
            "negative" => "本機示範分析判斷這段評論帶有負向情緒。",
            _ => "本機示範分析判斷這段評論較為中性。"
        };

        return Task.FromResult(new SentimentAnalysisResult(polarity, label, confidence, summary));
    }
}

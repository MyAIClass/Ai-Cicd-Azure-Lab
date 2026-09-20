namespace AiCicdAzureLab.Api.Services;

public sealed record ChallengeResponse(
    string Title,
    string Description);

public static class ChallengeService
{
    private static readonly ChallengeResponse[] Challenges =
    [
        new("健康檢查員", "找出 /health API，確認服務是否正常"),
        new("前端設計師", "修改網站的主色彩"),
        new("測試工程師", "替一個功能新增自動測試"),
        new("Git 探險家", "建立一個新的 Git branch"),
        new("Docker 玩家", "成功建立 ai-cicd-azure-lab Docker Image")
    ];

    public static ChallengeResponse Draw()
    {
        var index = Random.Shared.Next(Challenges.Length);
        return Challenges[index];
    }
}
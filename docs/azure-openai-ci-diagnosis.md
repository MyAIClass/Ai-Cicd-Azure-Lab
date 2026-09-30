# Azure OpenAI CI 失敗診斷（第四階段）

本階段在 `.NET CI` 失敗後，以 Azure OpenAI 協助整理可能原因與下一步驗證方式。診斷只提供建議：**不會修改程式碼、重跑工作流程、核准 Pull Request 或改變 CI 的失敗結果**。

## OIDC 與 GitHub Environment 設定

建立獨立的 GitHub Environment：`azure-openai-diagnosis`。建立專用的 Microsoft Entra ID service principal 與只允許此 Environment 使用的 Federated Credential，再於 Azure OpenAI 資源範圍授予 **Cognitive Services OpenAI User** 角色。

這個 service principal 不應有 ACR、Container Apps、Subscription 或 Resource Group 的部署權限。Workflow 以 OIDC 取得短期權杖，**不要建立、保存或提交 Azure OpenAI API Key**。

在 `azure-openai-diagnosis` Environment 設定以下 Variables：

| Variable | 說明 |
| --- | --- |
| `AZURE_CLIENT_ID` | CI 診斷專用 service principal 的 Application (client) ID |
| `AZURE_TENANT_ID` | Microsoft Entra tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure OpenAI 所在 Subscription ID |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI Endpoint |
| `AZURE_OPENAI_DEPLOYMENT` | 使用的模型 Deployment 名稱 |

Variable 值由講師或資源管理者維護，不要寫入 Repository、PR 留言或課程作業。

## Workflow 行為

[`ai-ci-diagnosis.yml`](../.github/workflows/ai-ci-diagnosis.yml) 只監聽 `.NET CI` 的 `workflow_run` 完成事件，且只在結論為 `failure` 時執行：

1. 使用 GitHub CLI 讀取該次執行的失敗步驟日誌，以 `--repo "$GITHUB_REPOSITORY"` 明確指定 Repository，不需 checkout 失敗分支的程式碼。
2. 遮罩常見 Token、`Authorization`、Cookie、Key、Secret 與 Password 內容，並將送往模型的日誌限制為 12,000 bytes。`Authorization:` 不分大小寫，會遮罩其後直到行尾的完整內容（包含 Bearer、Basic 或 Digest 的憑證），而非只遮罩驗證方式的名稱。
3. 使用 OIDC 登入 Azure，呼叫 Azure OpenAI v1 Chat Completions API。
4. 要求模型以繁體中文整理可能原因、可確認的證據、最小修正、應驗證的測試與仍需人工確認事項。
5. 若 CI run 關聯一個或多個 PR，新增或更新每個 PR 的 `🤖 Azure OpenAI CI 失敗診斷` 留言；一般 branch push 不會留言。

日誌與模型輸出皆可能包含不正確內容。模型被要求將日誌視為不可信資料，人工仍必須核對失敗 step、原始碼與測試，修正後重新執行 CI。

正規表示式遮罩無法保證辨識所有機密格式；仍應避免將真實憑證或個人資料寫入 CI 日誌。

本機可執行 `python -B -m unittest discover -s tests -p 'test_ai_*_workflow.py'`。日誌測試會以模擬的 `gh` 執行 workflow 內的 Bash／GNU sed，檢查 Repository 參數、完整憑證遮罩、12,000 bytes 上限及下載失敗時停止處理；不會呼叫 GitHub 或 Azure。Windows 需讓 Git Bash 的 `bash.exe` 可從 PATH 找到。

## 預演與驗收

1. 確認 `azure-openai-diagnosis` Environment 的五個 Variables 與 OIDC 角色設定完成。
2. 建立只修改測試預期值的示範 PR，使 `.NET CI` 的 xUnit 測試失敗；不要在 `main` 保留故意失敗的測試。
3. 確認 `Azure OpenAI CI diagnosis` 在 `.NET CI` 失敗後執行，且 PR 出現診斷留言。
4. 檢查留言是否含「可能原因」、「可確認的證據」、「最小修正」、「應驗證的測試」與「仍需人工確認」五個段落，並人工核對其內容。
5. 修正測試、重新 push，確認 `.NET CI` 通過；診斷 workflow 不應在成功 CI run 執行。
6. 暫時使用不存在的 `AZURE_OPENAI_DEPLOYMENT` 預演失敗路徑，確認 PR 留言改為要求人工診斷，且 CI 原本的失敗結果維持不變；測試後立即還原設定。

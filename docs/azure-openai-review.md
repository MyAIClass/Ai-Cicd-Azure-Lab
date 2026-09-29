# Azure OpenAI Pull Request 審查（第三階段）

本階段讓 GitHub Actions 在 Pull Request 開啟或更新時，自動取得程式碼差異，透過 OIDC 登入 Azure 並呼叫 Azure OpenAI，將審查建議留言回 Pull Request。這個 Workflow **只提供建議，不會阻擋或自動核准合併**，也不會修改程式碼。

## PR 審查專用的 OIDC 設定

Workflow 使用獨立的 `azure-openai-review` Environment 與 Microsoft Entra ID service principal，登入方式同樣是 OIDC，不新增任何長期金鑰。這個 service principal 只授予 Azure OpenAI 資源的呼叫權限，不得使用部署用的 ACR 或 Container Apps 權限。

請為這個 service principal 建立只允許 `pull_request_target` workflow 使用的 Federated Credential，並授予它呼叫 Azure OpenAI 的權限：

- 在 Azure OpenAI 資源（例如本課程使用的 `myaoaifordemo`）上，指派 **Cognitive Services OpenAI User** 角色給該 service principal，範圍限定在這個 Azure OpenAI 資源，不要開放到整個 Resource Group 或 Subscription；不要將部署用的 `demo` service principal 權限授予它。

不需要建立或保存 Azure OpenAI API Key；Workflow 透過 `az account get-access-token --resource https://cognitiveservices.azure.com` 取得短期 Microsoft Entra ID 權杖，並以 Bearer token 呼叫 Chat Completions REST API。

## GitHub Environment 設定

在 `azure-openai-review` Environment 新增以下 Variables：

| Variable | 內容 |
| --- | --- |
| `AZURE_OPENAI_ENDPOINT` | Azure OpenAI 資源的 Endpoint，例如 `https://myaoaifordemo.openai.azure.com/` |
| `AZURE_OPENAI_DEPLOYMENT` | 要使用的模型 Deployment 名稱，例如 `gpt-6-luna` |

這兩個值不是密碼，可視為一般設定；仍建議只放在 GitHub Environment Variables，不寫進程式碼或提交到 Repository。

## Workflow 行為

[`.github/workflows/ai-review.yml`](../.github/workflows/ai-review.yml) 使用 `pull_request_target`，在預設分支的 workflow 內容中執行，因此 Pull Request 不能藉由修改 workflow 取得權限。它只讀取 diff，不 checkout 或執行 Pull Request 的程式碼；外部 fork 也能安全取得審查結果。

工作順序：

1. 檢查必要的 Environment Variables 是否齊全。
2. 取出這次 PR 相對於目標分支的差異，排除 `bin/`、`obj/` 與圖片檔案。
3. 以正規表示式移除看起來像 API Key、Token、Secret 或密碼的內容，並將差異截斷在合理長度，避免外洩機密或超出模型與費用限制。
4. 使用 `azure/login@v2` 透過 OIDC 登入 Azure，取得 Cognitive Services 範圍的存取權杖。
5. 呼叫 Azure OpenAI v1 Chat Completions API，使用適用於推理模型的 `max_completion_tokens`，請模型以繁體中文條列可能的邏輯錯誤、缺少的測試、敏感資訊外洩風險、不安全輸入處理與效能疑慮。
6. 將審查結果整理成 Pull Request 留言；若同一個 PR 已有先前的審查留言，會更新既有留言而不是重複新增。
7. 呼叫失敗或逾時時，會留言說明「自動審查失敗，請人工審查」，並讓 AI 審查檢查顯示失敗以提醒維護者；獨立的 .NET CI 檢查不受影響。Actions log 僅顯示 HTTP 狀態、錯誤代碼與參數，不公開可能含有 PR 內容的完整錯誤回應。

## 安全設計

- 只讀取 diff，不會執行、模擬或匯入 PR 中的程式碼。
- 傳送前先過濾常見的機密樣式（`sk-`、`ghp_`、`*KEY=`、`*SECRET=`、`*TOKEN=`、`*PASSWORD=`），降低機密外洩風險；仍建議學員與講師在 PR 中避免放入真實機密。
- 送給模型的內容長度有上限，避免整個 Repository 或超大 diff 被整段送出。
- AI 產生的內容只出現在 PR 留言，不會自動修改程式碼、不會自動核准或合併 PR。
- Workflow 呼叫失敗不會讓 CI 或部署失敗，只會在留言中說明需要人工審查。

## 尚待確認的 Azure 設定

- 在 Azure OpenAI 資源 `myaoaifordemo` 上，將 **Cognitive Services OpenAI User** 角色指派給 `azure-openai-review` Environment 使用的 service principal。
- 確認課程使用的模型 Deployment 名稱（目前規劃為 `gpt-6-luna`）已經部署完成，且該 Deployment 支援 Chat Completions API。
- 確認 `azure-openai-review` Environment 已新增 `AZURE_OPENAI_ENDPOINT` 與 `AZURE_OPENAI_DEPLOYMENT` 兩個 Variables，且其 service principal 沒有部署權限。

這份文件與 Workflow 不會代為指派 Azure 角色或建立 Azure OpenAI Deployment。

## 預演檢查

- 建立一個小型 Pull Request，確認 Actions 出現 `Azure OpenAI PR review` 這個 workflow run。
- 確認 PR 留言出現以 `🤖 Azure OpenAI 審查建議` 開頭的留言。
- 再次 push 同一個 PR，確認留言是更新既有留言，而不是新增第二則留言。
- 暫時把 `AZURE_OPENAI_DEPLOYMENT` 改成不存在的名稱，確認 Workflow 會留言「自動審查失敗，請人工審查」，AI 審查檢查標示失敗，且不影響獨立的 .NET CI 測試結果。測試後還原設定。
- 確認 Azure OpenAI 資源的存取記錄或計量中，能看到來自這個 Workflow 的呼叫。

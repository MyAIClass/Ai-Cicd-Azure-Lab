# 六小時課程操作腳本

本文件是講師帶領「AI 串接 CI/CD 與 Azure 部署」課程的現場腳本。內容以本 Repository 的目前實作為準：.NET 8 Minimal API、原生 HTML/CSS/JavaScript、xUnit 測試、GitHub Actions、Docker，以及 Azure Container Registry（ACR）與 Azure Container Apps（ACA）。

建議先閱讀 [`README.md`](../README.md) 與 [`azure-setup.md`](azure-setup.md)，並在課前完整走過一次部署流程。課堂上的 Azure 資源名稱、Subscription ID、Endpoint 與身分識別資訊一律使用 GitHub Variables、Secrets、OIDC 或 Managed Identity，不要寫入 Repository。

## 課程目標與完成條件

課程結束時，學員應能：

- 說明 Git commit、branch、Pull Request、CI 與 CD 的關係。
- 在本機啟動 API，使用瀏覽器或 `curl` 驗證 `/health` 與 `/api/greeting`。
- 修改 C# 程式與 xUnit 測試，透過 Pull Request 觸發 GitHub Actions。
- 從 CI 日誌判斷 restore、test 或 Docker build 哪一階段失敗。
- 說明 Docker image、ACR 與 ACA 各自負責的工作。
- 使用 OIDC 讓 GitHub Actions 登入 Azure，而不是把長期 Azure 金鑰放進 Repository。
- 以 AI 協助理解程式碼或日誌，並知道如何人工驗證 AI 建議。

建議的課程完成條件如下：

1. 學員至少完成一個成功的 Pull Request。
2. 學員看過一次「測試失敗，合併被阻擋」的結果，並能說明失敗原因。
3. 講師完成一次成功的 Docker build 與 ACA 更新。
4. 學員能在瀏覽器看到部署後的 `/` 頁面，且 `/health` 回傳 `status` 為 `ok`。

## 課前準備

### 講師需要準備

- GitHub Repository，並確認學員具有建立 branch 與 Pull Request 的權限。
- 一個課程專用 Azure Resource Group，預先建立 ACR、Container Apps Environment、Container App，以及 Application Insights 或 Log Analytics。
- GitHub Actions 使用的 Microsoft Entra ID App Registration、Federated Credential 與最小必要權限。
- GitHub Environment `demo`，並依 [`azure-setup.md`](azure-setup.md) 設定必要的 Variables。
- 一個可成功執行的 Pull Request 範例，以及一個故意失敗的測試範例。
- 課堂投影或共用螢幕，讓學員看得到 GitHub Actions、Container App 與日誌。
- 如果要示範 Azure OpenAI，準備不含個資、密碼、Token、Cookie 或連線字串的範例程式碼與錯誤日誌。

### 課前驗收清單

在學員進場前，講師至少執行以下驗收：

```powershell
dotnet test tests/AiCicdAzureLab.Api.Tests/AiCicdAzureLab.Api.Tests.csproj
docker build -t ai-cicd-azure-lab .
```

再確認：

- `.github/workflows/ci.yml` 在 `push` 與 `pull_request` 都會觸發。
- CI 具備 `contents: read` 權限，且測試和 Docker build 都成功。
- Azure 部署範本仍放在 `.github/workflow-templates/azure-deploy.yml`；它不會自行執行。
- 只有完成 Azure 與 OIDC 設定後，才將範本複製為 `.github/workflows/azure-deploy.yml`。
- Container App 的 ingress target port 是 `8080`，因為 Dockerfile 設定 `ASPNETCORE_HTTP_PORTS=8080`。
- Container App 可以從 ACR 拉取 image，且部署完成後能存取 `/health`。
- 課程結束的停用、刪除或成本控管方式已先確認。

### 學員設備與替代方案

基本需求是 GitHub 帳號與可執行 Git 的環境。建議準備下列其中一種：

- GitHub Codespaces：統一環境，適合第一次接觸 .NET 的學員。
- 本機環境：安裝 .NET 8 SDK；若要做 Docker 單元，另需 Docker Desktop。
- 只觀摩 Azure 的學員：由講師操作 Azure，學員使用 GitHub Actions 與部署結果觀察流程。

若學員無法執行 Docker，不要讓個別環境問題中斷課程；可由講師共用成功的 CI Docker build 日誌，並把實作重點放在 image、registry 與部署的關係。

## 六小時時間表

時間可依班級程度調整。每個單元結束時，先讓學員完成「學員操作」再進行下一段示範。

| 時間 | 單元 | 講師重點 | 學員產出 |
| --- | --- | --- | --- |
| 00:00–00:20 | 開場與環境檢查 | 說明目標、帳號、分組與安全原則 | 確認能開啟 Repository |
| 00:20–00:55 | Git、PR、CI/CD | 用本 Repository 走一次變更流程 | 說出一次變更的生命週期 |
| 00:55–01:35 | 本機 API | 啟動服務、呼叫端點、閱讀程式結構 | 成功呼叫 `/health` 與 greeting API |
| 01:35–02:25 | 第一次 Pull Request | 修改 greeting、寫測試、看 CI | 建立一個可通過的 PR |
| 02:25–02:45 | 休息 | — | — |
| 02:45–03:20 | 失敗情境與除錯 | 讓測試失敗，從日誌定位問題 | 說明為何不能合併 |
| 03:20–04:05 | Docker | 解析 Dockerfile、build、run | 在 `localhost:8080` 看到服務 |
| 04:05–04:45 | Azure 部署架構 | 說明 ACR、ACA、OIDC、Managed Identity | 畫出 image 流程 |
| 04:45–05:25 | 部署與驗證 | 執行 workflow、檢查 revision、health | 驗證雲端服務 |
| 05:25–05:50 | AI 輔助審查與日誌分析 | 示範提問、遮罩機密、人工確認 | 產生一份可驗證的建議 |
| 05:50–06:00 | 回顧與清理 | 重點問答、停用資源 | 完成課後檢查表 |

## 課堂流程

### 1. 開場：先建立共同語言（00:00–00:20）

#### 講師說明

本課程不是只把程式「放上雲端」，而是練習一條可重複、可驗證的交付流程：

```text
修改程式
  → commit / push
  → Pull Request
  → GitHub Actions 執行 restore、test、Docker build
  → 人工審查
  → 合併 main
  → Azure 建立或更新 Container App revision
  → /health 與 Application Insights 驗證
```

強調三個原則：

1. CI 通過是必要條件，不代表程式一定符合需求。
2. AI 的輸出是建議，不是自動核准；人仍要檢查差異、測試與安全風險。
3. Azure 權限與機密要最小化，課程不使用寫死在程式碼中的長期共用金鑰。

#### 學員操作

- 開啟 Repository 的 `README.md`、`src/`、`tests/`、`.github/workflows/` 與 `docs/`。
- 找到 `GET /health`、`GET /api/greeting`、CI workflow 與 Dockerfile。
- 兩人一組回答：「如果測試失敗，為什麼不能直接部署？」

### 2. 本機執行 API（00:55–01:35）

#### 講師示範

在 Repository 根目錄執行：

```powershell
dotnet restore tests/AiCicdAzureLab.Api.Tests/AiCicdAzureLab.Api.Tests.csproj --configfile NuGet.Config
dotnet test tests/AiCicdAzureLab.Api.Tests/AiCicdAzureLab.Api.Tests.csproj --no-restore
dotnet run --project src/AiCicdAzureLab.Api/AiCicdAzureLab.Api.csproj
```

啟動後依終端機顯示的連接埠測試。若是 `5000`，可使用：

```powershell
Invoke-RestMethod http://localhost:5000/health
Invoke-RestMethod "http://localhost:5000/api/greeting?name=小明"
```

預期結果：

```json
{"status":"ok"}
```

greeting 回應會包含 `Message`、`Service`、`Timestamp`；`name` 前後空白會被移除，沒有名稱時會使用「課程學員」。

#### 程式導讀

- `Program.cs` 設定 exception handler、靜態檔案與兩個 API endpoint。
- `GreetingService.cs` 封裝 greeting 商業邏輯，方便單元測試。
- `GreetingServiceTests.cs` 驗證空白名稱與自訂名稱兩種案例。
- `wwwroot/` 是由同一個 ASP.NET Core 服務提供的前端，不需要額外啟動 Node.js。

#### 學員操作

1. 修改 `name`，觀察 API 回應。
2. 不帶 `name` 呼叫 API，確認預設名稱。
3. 開啟 `/`，從瀏覽器操作前端，再開啟瀏覽器開發者工具觀察 API request。
4. 停止服務後，說明為什麼瀏覽器頁面與 API 會同時失效。

### 3. 第一次 Pull Request 與成功 CI（01:35–02:25）

#### 建議任務

請學員將 greeting 的服務名稱改成課程指定值，或新增一個小而明確的行為，例如：

- 調整首頁說明文字。
- 為 greeting 增加新的測試案例。
- 修改回應訊息，但同步更新測試的預期結果。

不要在第一次練習同時修改 API 格式、部署設定與大量前端程式，以免難以判斷 CI 失敗原因。

#### 學員操作流程

```powershell
git switch -c feature/update-greeting
dotnet test tests/AiCicdAzureLab.Api.Tests/AiCicdAzureLab.Api.Tests.csproj
git status
git add src tests
git commit -m "更新 greeting 示範"
git push -u origin feature/update-greeting
```

接著在 GitHub 建立 Pull Request，填寫：

- 變更目的：想解決什麼問題。
- 驗證方式：執行了哪些測試或端點檢查。
- 風險：是否修改 API response、設定或部署行為。

#### 講師帶看 CI

`.github/workflows/ci.yml` 在 `push` 與 `pull_request` 觸發，順序是：

1. `actions/checkout@v4` 取出程式碼。
2. `actions/setup-dotnet@v4` 設定 .NET 8。
3. `dotnet restore` 還原相依套件。
4. `dotnet test --configuration Release --no-restore` 執行測試。
5. `docker build` 建立 image，但目前 CI 不會將 image 推送到 registry。

提醒學員：GitHub Actions 的 Docker build 成功，只代表 image 能建立；要部署到 Azure，還需要額外的 ACR push 與 ACA update 工作。

### 4. 故意失敗與除錯（02:45–03:20）

#### 建議失敗方式

由講師示範或請學員在獨立 branch 將測試中的預期值改錯，例如把：

```csharp
Assert.Equal("你好，課程學員！", response.Message);
```

暫時改成錯誤文字，然後 push 到 Pull Request。不要把故意失敗的版本合併到 `main`。

#### 引導學員閱讀日誌

請學員依序回答：

1. 哪一個 job 失敗？
2. 哪一個 step 失敗？
3. 失敗是 restore、test、Docker build 還是權限問題？
4. 錯誤訊息指出「實際值」和「預期值」各是什麼？
5. 修正程式或測試後，是否重新執行本機測試？

#### 常見誤判

- 測試失敗不一定是 GitHub Actions 壞掉，先在本機重現。
- Docker build 通過不代表 API 行為正確，因為目前 Dockerfile 只建置 API 專案，不會執行 xUnit 測試。
- PR 顯示可以合併不代表已完成產品驗收，仍需檢查需求、資安與部署結果。

### 5. Docker：從程式到 image（03:20–04:05）

#### 講師導讀 Dockerfile

- 第一階段使用 `mcr.microsoft.com/dotnet/sdk:8.0` 還原並 publish。
- 第二階段使用較小的 `mcr.microsoft.com/dotnet/aspnet:8.0` 執行應用程式。
- `ASPNETCORE_HTTP_PORTS=8080` 與 `EXPOSE 8080` 對應 ACA 的 ingress target port。
- 這是 multi-stage build，執行階段不包含 SDK 與原始碼建置工具。

#### 學員操作

確認 Docker Desktop 正在執行後：

```powershell
docker build -t ai-cicd-azure-lab .
docker run --rm -p 8080:8080 ai-cicd-azure-lab
```

另開終端機驗證：

```powershell
Invoke-RestMethod http://localhost:8080/health
Invoke-RestMethod "http://localhost:8080/api/greeting?name=Docker"
```

完成後以 `Ctrl+C` 停止容器。若要排查建置快取或 NuGet fallback folder 問題，可重建：

```powershell
docker build --no-cache -t ai-cicd-azure-lab .
```

### 6. Azure 架構與安全登入（04:05–04:45）

#### 用一張圖說明元件責任

```text
GitHub Actions
  ├─ OIDC 短期權杖 → Microsoft Entra ID
  ├─ az acr build → Azure Container Registry
  └─ az containerapp update → Azure Container Apps
                                  └─ 拉取 ACR image 並建立新 revision
```

- ACR 保存 container image。
- ACA 執行 container，提供 ingress 與 revision。
- Container App 的 Managed Identity 負責從 ACR 拉取 image。
- Application Insights 或 Log Analytics 保存監控與日誌。
- OIDC 讓 GitHub Actions 以聯邦身分取得短期 Azure 權杖；不需要把 client secret 放在 Repository。

#### 講師檢查設定

部署前確認 GitHub Environment `demo` 的 Variables 至少包含：

```text
AZURE_CLIENT_ID
AZURE_TENANT_ID
AZURE_SUBSCRIPTION_ID
AZURE_RESOURCE_GROUP
AZURE_CONTAINER_REGISTRY
AZURE_CONTAINER_REGISTRY_LOGIN_SERVER
AZURE_CONTAINER_APP
```

`AZURE_OPENAI_ENDPOINT` 與 `AZURE_OPENAI_DEPLOYMENT` 只有在課程實際示範 Azure OpenAI 時才需要，且不得把 API Key 或 Token 寫入 workflow、程式碼、日誌或投影片。

### 7. 執行 Azure 部署與驗證（04:45–05:25）

#### 啟用部署 Workflow

先確認 Azure 與 OIDC 設定完成，再執行：

```powershell
Copy-Item .github/workflow-templates/azure-deploy.yml .github/workflows/azure-deploy.yml
git add .github/workflows/azure-deploy.yml
git commit -m "啟用 Azure 部署 workflow"
git push origin main
```

也可以從 GitHub Actions 使用 `workflow_dispatch` 手動執行。部署範本的兩個核心步驟是：

```text
az acr build --registry <registry> --image ai-cicd-azure-lab:<commit-sha> .
az containerapp update --image <login-server>/ai-cicd-azure-lab:<commit-sha>
```

使用 `${{ github.sha }}` 作為 image tag，讓每個部署都能對應到特定 commit，方便追蹤與回復。

#### 驗證順序

1. GitHub Actions：確認 Azure login、ACR build、Container App update 都成功。
2. Azure Container App：確認最新 revision 已建立且處於可接收流量狀態。
3. 應用程式 URL：先呼叫 `/health`，再開啟 `/` 與 `/api/greeting`。
4. 監控：從 Application Insights 或 Log Analytics 查看請求與例外。
5. 回到 PR：記錄部署的 commit SHA 與驗證結果。

若 `/health` 失敗，先檢查 container 是否啟動、target port 是否為 `8080`、image 是否存在，以及 Container App 是否能從 ACR 拉取 image；不要先猜測應用程式程式碼有問題。

### 8. AI 輔助審查與錯誤診斷（05:25–05:50）

目前 Repository 的 CI 與部署 Workflow 沒有內建 AI 呼叫；本單元是安全使用 AI 的方法示範，不要把「概念示範」描述成已完成的自動化功能。

#### 建議示範題目

將已遮罩的 diff 或錯誤日誌交給模型，要求模型：

1. 先摘要變更目的。
2. 列出可能的功能、測試、效能與安全風險。
3. 為每個風險提供驗證方式，而不是直接要求模型下結論。
4. 標示不確定性，避免把推測當成事實。

可使用下列提示詞作為起點：

```text
你是程式碼審查助手。請只根據以下已遮罩的 diff 與測試結果回答。
請分成「觀察到的事實、可能風險、建議驗證、尚缺少的資訊」四段。
不要假設未提供的 Azure 設定，也不要建議提交任何秘密值。
```

#### 人工確認清單

- AI 提到的檔案與行為是否真的存在？
- 建議是否有對應的測試或可重現步驟？
- 是否誤把範例值當成正式的 endpoint、帳號或權限？
- 是否把個資、Token、Cookie、連線字串或完整敏感日誌送給模型？
- 最終 PR 是否仍由人員負責核准與合併？

## 常見問題排除

| 現象 | 優先檢查 | 建議處理 |
| --- | --- | --- |
| `dotnet restore` 失敗 | .NET SDK、`NuGet.Config`、網路 | 先確認 `dotnet --info`，再重跑 restore |
| 本機測試失敗 | 實際值與預期值、是否使用舊 build | 先讀 assertion，再修正程式或測試並重跑 |
| GitHub Actions 沒有觸發 | workflow 路徑、branch、Actions 權限 | 確認檔案位於 `.github/workflows/` 且已 push |
| Docker build 失敗 | Docker daemon、`.dockerignore`、NuGet restore | 確認 Docker Desktop，再用 `--no-cache` 重建 |
| ACA 顯示無法拉取 image | ACR login server、image tag、Managed Identity 權限 | 確認 image 使用同一個 `${{ github.sha }}`，並檢查 ACR pull 權限 |
| ACA 啟動但網站無法連線 | ingress、target port、container logs | 確認 ingress port 為 `8080`，再查看 revision 日誌 |
| OIDC login 失敗 | Federated Credential 的 Repository、branch、Environment | 比對 GitHub Environment 與 Entra ID 條件，不要改成長期密碼 |
| 找不到 Azure OpenAI 回應 | endpoint、deployment、模型權限 | 將 AI 單元退回離線範例；不要在課堂現場新增未驗證的金鑰 |

## 課後清理與安全檢查

課程結束前，講師與學員共同確認：

- 沒有提交 `.env`、API Key、密碼、Token、Cookie 或私人憑證。
- 故意失敗的 branch 或 PR 已關閉、修正或明確標註，不會誤合併到 `main`。
- 不再需要的 Azure Container App、Container Apps Environment、ACR、Application Insights 或 Log Analytics 已停止、刪除或套用成本控管。
- 不再需要的 GitHub Actions Workflow 已停用，或只保留課程需要的觸發條件。
- 課程用 Entra ID 權限仍限制在課程 Resource Group；若使用過簡化示範用 Secret，已撤銷或輪替。
- 學員知道如何在課後重新執行本機測試與 Docker build。

## 講師課後回顧

記錄以下資訊，供下一梯次改善：

- 哪個步驟最常因環境差異卡住？
- 學員能否從 CI 日誌找到第一個真正的錯誤？
- 是否有足夠時間讓學員自行建立並修正 PR？
- Azure 資源的等待時間與費用是否符合課程安排？
- AI 示範是否使用了完全去識別化的資料？
- 哪些步驟應該更新到 `README.md` 或 `docs/azure-setup.md`？

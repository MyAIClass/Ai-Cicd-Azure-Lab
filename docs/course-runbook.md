# 六小時課程操作腳本

本腳本以本 Repository 的實際內容為準，帶領學員完成「修改程式碼 → 測試 → Pull Request → CI → Docker → Azure 部署觀察 → AI 輔助診斷」的完整流程。建議講師先完成課前預演，再依時間表逐段操作；Azure 與 AI 階段可以由講師示範，學員不需要擁有 Azure 管理權限。

## 一、課程資訊

### 課程目標

完成課程後，學員應能：

- 說明 Git branch、Pull Request、CI 與 CD 在交付流程中的責任。
- 修改 GreetingService，並為行為變更補上測試。
- 在本機執行 .NET 測試、啟動 API，以及用 Docker 建立和執行 Container Image。
- 從 GitHub Actions 的結果判斷測試或 Docker build 是否成功。
- 說明 Azure Container Registry、Azure Container Apps、Application Insights 之間的關係。
- 使用經過清理的錯誤資訊請 AI 協助分析，並以測試與人工審查驗證建議。

### 建議時間表

| 時間 | 主題 | 學員產出 |
| --- | --- | --- |
| 00:00–00:25 | 課程說明與環境檢查 | 確認 Repository、.NET 與 Docker 可用 |
| 00:25–01:15 | Git、API 與專案導覽 | 找到 API、服務類別與測試 |
| 01:15–02:05 | 本機修改與測試 | 完成一次 API 行為修改 |
| 02:05–02:20 | 休息 | — |
| 02:20–03:15 | Pull Request 與 CI | 建立 PR 並讀懂 CI 結果 |
| 03:15–04:00 | 故障注入與 Docker | 觀察失敗 CI，完成 Docker 本機驗證 |
| 04:00–04:15 | 休息 | — |
| 04:15–05:15 | Azure Container Apps 部署流程 | 看懂 image 推送與部署版本 |
| 05:15–05:45 | AI 輔助審查與日誌診斷 | 產生、驗證一份 AI 建議 |
| 05:45–06:00 | 回顧、清理與問答 | 完成檢核表，停止課程資源 |

## 二、課前準備

### 講師準備

- 建立課程專用 GitHub Repository，確認學員有 read 與建立 branch/PR 的權限。
- 準備一個可供學員 fork 或 clone 的乾淨版本，並保留 main branch 的保護規則。
- 確認 .github/workflows/ci.yml 已存在，且能在 push 與 pull_request 時執行。
- 依 azure-setup.md 建立或確認共用 Azure 環境；至少預演一次成功部署。
- 準備一個能通過 CI 的 PR，以及一個故意造成測試失敗的 PR。失敗示範結束後要還原，不要合併故障版本。
- 準備可安全分享的測試錯誤訊息與 Docker log。移除帳號、Token、Endpoint、內部網域與個人資料。
- 確認學員知道 Azure 階段是觀察與理解流程；不要把 Subscription Owner 或長期共用金鑰交給學員。

### 學員環境

至少需要：

- GitHub 帳號，以及瀏覽 Repository、建立 branch 和 Pull Request 的權限。
- Git、.NET 8 SDK。
- Docker Desktop 或可使用 Docker Engine 的環境。
- 可使用 PowerShell、Terminal 或 Codespaces。

若使用 Codespaces，講師應先確認組織政策、可用額度及學員是否能執行 Docker。學員不需要安裝 Azure CLI；Azure 部署由 GitHub Actions 或講師環境執行。

## 三、課前驗證

學員先取得程式碼並切換到自己的工作分支：

~~~powershell
git clone https://github.com/<帳號>/<Repository>.git
Set-Location <Repository>
git switch -c feature/update-greeting
dotnet --info
docker version
~~~

接著執行 Repository 的標準驗證：

~~~powershell
dotnet test tests/AiCicdAzureLab.Api.Tests/AiCicdAzureLab.Api.Tests.csproj
docker build -t ai-cicd-azure-lab .
~~~

預期結果：測試全部通過，Docker image 建立完成。若 NuGet 還原失敗，使用專案指定的設定檔重新還原：

~~~powershell
dotnet restore tests/AiCicdAzureLab.Api.Tests/AiCicdAzureLab.Api.Tests.csproj --configfile NuGet.Config
dotnet test tests/AiCicdAzureLab.Api.Tests/AiCicdAzureLab.Api.Tests.csproj --no-restore
~~~

## 四、課堂操作流程

### 1. 專案與交付流程導覽（25 分鐘）

先讓學員在 GitHub 上找到以下檔案，再對照實際內容說明責任：

| 檔案 | 說明 |
| --- | --- |
| src/AiCicdAzureLab.Api/Program.cs | 註冊 static files，並定義 /health 與 /api/greeting |
| src/AiCicdAzureLab.Api/Services/GreetingService.cs | 封裝問候訊息的商業邏輯 |
| tests/AiCicdAzureLab.Api.Tests/GreetingServiceTests.cs | 驗證空白名稱與自訂名稱 |
| Dockerfile | 建立 .NET publish image，並以 port 8080 啟動 |
| .github/workflows/ci.yml | 在 push/PR 執行 restore、test 與 Docker build |
| .github/workflow-templates/azure-deploy.yml | Azure 部署範本；尚未設定時不會自動執行 |

請強調流程中的關係：

~~~text
修改程式碼 → push branch → Pull Request → GitHub Actions CI
                                      ├─ 測試
                                      └─ Docker build
                                                ↓
                                     Azure Container Registry
                                                ↓
                                      Azure Container Apps
~~~

### 2. 本機啟動與 API 操作（30 分鐘）

執行 API：

~~~powershell
dotnet run --project src/AiCicdAzureLab.Api/AiCicdAzureLab.Api.csproj
~~~

依終端機顯示的網址測試；若使用預設設定，也可以嘗試：

~~~powershell
Invoke-RestMethod http://localhost:5000/health
Invoke-RestMethod "http://localhost:5000/api/greeting?name=小明"
~~~

預期可看到：

- /health 回傳 status 為 ok。
- /api/greeting?name=小明 回傳包含「你好，小明！」、服務名稱與 UTC timestamp 的 JSON。
- 開啟 / 可看到前端頁面，前端會呼叫同一個 API。

若實際連接埠不是 5000，以 dotnet run 輸出的網址為準。讓學員觀察 GreetingService.Create 如何處理 null、空白字串與前後空白。

### 3. 修改 API 並補測試（50 分鐘）

建議題目：將預設問候語從「你好，課程學員！」改成班級指定文字，或增加一個不影響既有 API 的格式規則。操作順序如下：

1. 先閱讀現有測試，說明測試描述的是「可觀察行為」而不是實作細節。
2. 修改 GreetingService.cs。
3. 同步更新或新增 GreetingServiceTests.cs，先讓測試表達新需求。
4. 執行測試並確認通過。
5. 重新啟動 API，實際呼叫 endpoint 確認結果。

範例驗證：

~~~powershell
dotnet test tests/AiCicdAzureLab.Api.Tests/AiCicdAzureLab.Api.Tests.csproj
Invoke-RestMethod "http://localhost:5000/api/greeting?name=  小明  "
~~~

課堂提醒：只修改前端文字而沒有更新 API 測試，不能算完整的行為變更；反之，若只改測試來配合錯誤實作，也不能算修正。測試、程式碼與手動驗證三者要互相一致。

### 4. 建立 Pull Request 並觀察 CI（55 分鐘）

~~~powershell
git status
git diff
git add src tests
git commit -m "更新問候訊息"
git push -u origin feature/update-greeting
~~~

在 GitHub 建立 Pull Request 時，請填寫：

- 目的：這次修改要解決什麼需求。
- 變更：修改了哪些檔案，是否新增測試。
- 驗證：本機執行的 dotnet test、Docker build 或 endpoint 結果。
- 風險：是否改變 API 回應格式、環境設定或部署行為。

帶學員打開 Actions 頁面，依序確認：

1. Checkout 與 .NET 8 設定成功。
2. dotnet restore 使用 NuGet.Config 成功。
3. xUnit 測試成功。
4. Docker image 成功建立。

只有必要檢查通過且完成人工審查後才合併 PR。若 CI 失敗，先閱讀失敗 step 與錯誤行，再在 branch 修正並重新 push；不要直接關閉檢查或以「本機可以」取代 CI 證據。

### 5. 故意造成失敗並復原（45 分鐘）

由講師示範在測試中暫時改成錯誤預期值，例如把預期的服務名稱改成不存在的值，然後 commit/push。讓學員觀察：

- CI 仍會執行，但 test step 會失敗。
- GitHub 會在 PR 顯示失敗檢查，受保護的 main 不應允許合併。
- 錯誤訊息包含失敗測試名稱、expected 與 actual，這些資訊可協助定位問題。

復原步驟：

~~~powershell
git diff
# 還原剛才的錯誤預期值，或用編輯器修回正確內容
dotnet test tests/AiCicdAzureLab.Api.Tests/AiCicdAzureLab.Api.Tests.csproj
git add tests
git commit -m "修正測試預期值"
git push
~~~

修正後等待 CI 重新通過，再關閉或合併示範 PR。不要把故意失敗的測試保留在 main。

### 6. Docker build 與執行（45 分鐘）

講解 Dockerfile 的兩階段建置：第一階段使用 SDK 還原與 publish，第二階段只保留 runtime 與發佈結果，以減少執行 image 內容。

~~~powershell
docker build -t ai-cicd-azure-lab .
docker run --rm --name ai-cicd-lab -p 8080:8080 ai-cicd-azure-lab
~~~

另開終端機驗證：

~~~powershell
Invoke-RestMethod http://localhost:8080/health
Invoke-RestMethod "http://localhost:8080/api/greeting?name=Docker"
~~~

講師可示範以下診斷指令：

~~~powershell
docker ps
docker logs ai-cicd-lab
docker stop ai-cicd-lab
~~~

若本機 Docker build 遇到 NuGet fallback package folder 或 ResolvePackageAssets，先確認 .dockerignore 沒有排除必要原始碼，再執行：

~~~powershell
docker build --no-cache -t ai-cicd-azure-lab .
~~~

### 7. Azure 部署流程（60 分鐘）

先說明各元件用途：

- GitHub Actions：執行 CI/CD 工作流程。
- Azure Container Registry (ACR)：保存 Docker image。
- Azure Container Apps：執行容器並提供 ingress。
- Application Insights / Log Analytics：集中查看請求、錯誤與執行紀錄。
- OIDC + Microsoft Entra ID：讓 GitHub Actions 取得短期 Azure 權杖，不在 Repository 保存長期密鑰。

課前已完成 Azure 設定時，依下列順序示範：

1. 將 .github/workflow-templates/azure-deploy.yml 複製為 .github/workflows/azure-deploy.yml，確認只在講師指定的 branch 或 workflow_dispatch 執行。
2. 檢查 GitHub demo Environment 的 Variables 是否已設定，值由講師管理，不在課堂投影片或 Repository 顯示。
3. 確認 workflow 使用 id-token: write 和 azure/login@v2，沒有把 Azure API Key 寫進 YAML。
4. 執行 workflow，觀察 az acr build 以 commit SHA 建立 image。
5. 觀察 az containerapp update 將同一個 SHA image 部署到 Container App。
6. 開啟 Container App URL，呼叫 /health 與 /api/greeting。
7. 在 Application Insights 或 Log Analytics 查詢成功請求及一筆示範錯誤。

部署前檢核：Resource Group、ACR、Container App 名稱與 ingress port 8080 必須彼此對應；Container App 的 Managed Identity 必須能從 ACR 拉取 image。若 Azure 尚未預建完成，本段只展示 workflow 與架構，不臨時建立額外資源。

### 8. AI 輔助審查與錯誤診斷（30 分鐘）

本 Repository 沒有自動呼叫 Azure OpenAI 的程式碼；本段示範的是安全的人工輔助流程，不代表 AI 結果會自動核准 PR 或直接修改正式環境。

建議流程：

1. 從 PR diff 或 CI log 擷取最小必要內容。
2. 移除 Token、Cookie、Authorization header、個人資料、完整 Endpoint 與內部網域。
3. 告知模型語言、測試命令、錯誤訊息與預期行為。
4. 要求模型列出「可能原因、證據、建議驗證方式」，不要只要求直接給結論。
5. 由學員人工檢查建議，新增或修改測試後重新執行 CI。

可使用的提示詞範例：

~~~text
你是 .NET CI/CD 助教。請分析以下已移除機密的 xUnit 失敗訊息。
請分成：可能原因、從錯誤訊息可確認的證據、最小修正、應新增的測試、仍需人工確認的風險。
不要假設你能存取 Repository、Azure 或任何憑證。

測試命令：dotnet test ...
預期行為：空白名稱應使用預設名稱
錯誤訊息：<貼上已清理的錯誤>
~~~

### 9. 課程回顧與交付（15 分鐘）

請每位學員用自己的話回答：

- 測試在 PR 和部署前分別扮演什麼角色？
- 為什麼 Docker image 應使用 commit SHA 或其他可追蹤版本，而不是只使用 latest？
- ACR 與 Container Apps 的責任有何不同？
- OIDC 解決了哪一種憑證風險？
- AI 建議要經過哪些驗證才可以採用？

建議學員交付一個 PR，內容至少包含一項 API 行為修改、一項對應測試，以及 PR 描述中的本機驗證結果。

## 五、故障排除速查

| 現象 | 先檢查 | 建議處理 |
| --- | --- | --- |
| dotnet 找不到 | dotnet --info | 安裝 .NET 8 SDK，重新開啟終端機 |
| NuGet 還原失敗 | NuGet.Config、網路與 SDK 版本 | 使用 --configfile NuGet.Config 重試 |
| 測試失敗 | 失敗測試的 expected/actual | 先判斷是程式碼錯誤還是測試預期未更新 |
| Docker daemon 無法連線 | docker version 的 Server 區段 | 啟動 Docker Desktop 或確認 Engine 可用 |
| Container 回應 connection refused | port mapping 與 ASPNETCORE_HTTP_PORTS | 使用 -p 8080:8080，不要把主機 port 和容器 port 混淆 |
| PR 沒有 CI | branch、workflow 路徑與 Actions 設定 | 確認 .github/workflows/ci.yml 已推送且 Actions 未被停用 |
| Azure login 失敗 | GitHub Environment、OIDC permission、Federated Credential | 由講師檢查設定，不要改用硬編碼 API Key |
| Container App 無法拉取 image | ACR image/tag 與 Managed Identity 權限 | 確認 image 使用正確 commit SHA，並檢查 AcrPull |
| Azure 產生非預期費用 | 資源狀態與 Log Analytics 保留設定 | 立即通知講師，依清理清單停止或刪除課程資源 |

## 六、安全與課後清理

課程全程遵守以下原則：

- 不把 API Key、密碼、Token、Cookie、私人連線資訊或 .env 提交到 Repository。
- Azure 資源名稱、Subscription ID、Endpoint 與帳號資訊只放在 GitHub Variables、Secrets 或受控設定中。
- Azure 權限限制在課程專用 Resource Group，不授予學員 Subscription Owner。
- AI 輸出只是建議；人工確認、測試與 CI 通過是合併和部署前的必要條件。
- 示範失敗的 branch/PR 在課後關閉或刪除，確認 main 沒有故意失敗的程式碼。

課程結束由講師依序確認：

1. Container App 不再需要服務時停止或刪除。
2. 不再需要的 ACR、Application Insights、Log Analytics 與 Resource Group 依課程政策清理。
3. 停用或移除 Azure deploy workflow，撤銷不再使用的 Federated Credential 與權限。
4. 清除 GitHub Environment 中不再需要的 Variables/Secrets。
5. 檢查 GitHub Actions、Azure Activity Log 與 Repository history，確認沒有意外提交機密。

完整 Azure 預建順序與權限說明請參考 [azure-setup.md](azure-setup.md)；本機開發和 Docker 指令請參考 [README.md](../README.md)。

## 課程原則

AI 的輸出是建議，不是自動核准。部署前仍要通過測試與人工確認，並避免把個人資料、密碼或憑證送給模型。

# AI CI/CD Azure Lab

這是一個可直接推送到 GitHub 的課程示範 Repository，用來示範如何以 GitHub Actions 串接 Azure，完成從程式碼提交、測試、AI 輔助檢查到雲端部署的流程。

本版本使用 .NET 8 ASP.NET Core Minimal API 作為後端，使用 HTML、CSS 與原生 JavaScript 作為前端。前端原始檔放在同一個 Repository 的 `wwwroot`；本機與 Azure Container Apps 仍可由同一個 ASP.NET Core 服務提供頁面與 API，正式展示也可由 GitHub Pages 發佈前端，再由瀏覽器呼叫 Container Apps API。

## 課程目標

- 使用 C# 建立簡單的 REST API
- 使用 HTML、CSS 與 JavaScript 呼叫 API
- 使用 GitHub Repository、Branch 與 Pull Request
- 使用 GitHub Actions 執行 .NET 測試與 Docker build
- 理解 Azure Container Registry 與 Azure Container Apps 的部署位置
- 認識 Azure OpenAI 在程式碼審查與錯誤診斷中的角色
- 使用 OIDC 讓 GitHub Actions 安全登入 Azure，避免散布長期 API Key

## 系統架構

GitHub Push / Pull Request
→ GitHub Actions
→ dotnet restore、dotnet test、Docker build
→ Azure Container Registry
→ Azure Container Apps
→ Application Insights / Log Analytics（後續階段可選）

應用程式本身包含：

- GET /health：健康檢查
- GET /api/captcha：取得一次性驗證碼資訊
- GET /api/captcha/{token}/image：取得驗證圖 SVG
- POST /api/greeting：驗證碼正確後呼叫 C# API
- GET /api/daily-quote：取得今日小語
- GET /api/challenge：取得今日課程任務；首頁會先從任務候選清單隨機輪播標題 6 秒，再顯示抽取結果
- GET /api/challenges：取得今日課程任務的候選清單
- POST /api/sentiment/analyze：分析評論的正負向情緒
- /：由 ASP.NET Core 提供的 HTML 前端（本機與 Container Apps 相容入口）
- GitHub Pages：正式靜態前端入口；瀏覽器透過 CORS 呼叫上述 API

前端目前採用豆沙色主題，主要色票定義於 `src/AiCicdAzureLab.Api/wwwroot/styles.css`：

- 頁面背景：`#F4E7E4`
- 按鈕與強調色：`#B76E79`
- 按鈕 hover：`#9E5963`
- 卡片背景：`#FFF9F7`
- 主要文字：`#4A3636`

## 專案結構

    ai-cicd-azure-lab/
    ├─ .github/
    │  ├─ workflows/
    │  │  ├─ ci.yml
    │  │  ├─ azure-deploy.yml
    │  │  ├─ pages-deploy.yml
    │  │  └─ ai-review.yml
    │  └─ workflow-templates/
    │     └─ azure-deploy.yml
    ├─ src/
    │  └─ AiCicdAzureLab.Api/
    │     ├─ Services/CaptchaService.cs
    │     ├─ Services/GreetingService.cs
    │     ├─ Program.cs
    │     ├─ AiCicdAzureLab.Api.csproj
    │     └─ wwwroot/
    │        ├─ index.html
    │        ├─ api-config.js
    │        ├─ app.js
    │        └─ styles.css
    ├─ tests/
    │  └─ AiCicdAzureLab.Api.Tests/
    ├─ docs/
    ├─ scripts/
    ├─ .dockerignore
    ├─ .env.example
    ├─ .gitignore
    ├─ AGENTS.md
    ├─ Dockerfile
    └─ README.md

## 本機執行

需求：

- .NET 8 SDK
- Windows、macOS 或 Linux

Windows PowerShell：

    dotnet restore tests/AiCicdAzureLab.Api.Tests/AiCicdAzureLab.Api.Tests.csproj --configfile NuGet.Config
    dotnet test tests/AiCicdAzureLab.Api.Tests/AiCicdAzureLab.Api.Tests.csproj --no-restore
    dotnet run --project src/AiCicdAzureLab.Api/AiCicdAzureLab.Api.csproj

啟動後開啟終端機顯示的網址，或使用：

- http://localhost:5000/
- http://localhost:5000/health
- 首頁會自動取得驗證圖；輸入圖片中的文字後，按「驗證並送出」呼叫 API。

實際連接埠可能依 .NET 開發環境設定而不同。

## 即時評論情感分析

首頁的「用戶評論情感分析」會在停止輸入約 0.5 秒後呼叫 `POST /api/sentiment/analyze`，以 Azure OpenAI 判斷評論的情緒分數（-1 到 1）、正負向標籤、信心度與繁體中文摘要，並用 SVG 指針度量計即時呈現。評論不會保存於瀏覽器、API 或資料庫；每次分析都會呼叫 Azure OpenAI，可能產生成本。

啟用此功能需要設定：

- `AZURE_OPENAI_ENDPOINT`：Azure OpenAI 資源 Endpoint
- `AZURE_OPENAI_DEPLOYMENT`：Chat Completions 模型部署名稱

API 使用 Azure OpenAI v1 Chat Completions endpoint（`/openai/v1/chat/completions`），不需設定日期型 API version。驗證使用 Microsoft Entra ID bearer token 與 `https://ai.azure.com/.default` scope，不使用或保存長期共用 API Key。本機可先執行 `az login`，Azure Container Apps 則需啟用 Managed Identity，並授予該身分對 Azure OpenAI 資源的適當推理權限。

在 `Development` 環境中，如果沒有設定 Azure OpenAI，系統會自動切換到本機示範分析器。它使用簡單的繁體中文正負向關鍵詞估算分數，方便展示度量計與前端互動，不代表正式的語意模型結果。設定 Azure OpenAI 後重新啟動服務，就會改用 Azure OpenAI 分析；非 Development 環境不會啟用本機示範模式。

## 圖形驗證碼

首頁的問候功能使用自製 SVG 圖形驗證碼。`GET /api/captcha` 會產生一次性驗證碼資訊，圖片則由 `GET /api/captcha/{token}/image` 動態產生；前端必須在 `POST /api/greeting` 的 JSON 內容送出 `name`、`captchaToken` 與 `captchaAnswer` 才能取得問候結果。

驗證碼不分大小寫，有效期限為 5 分鐘，成功或答錯一次後即失效；名稱長度上限為 50 個字元。答案只保存在單一服務執行個體的記憶體中，因此這是課程示範用的簡易防機器人機制，不是正式 CAPTCHA、WAF 或 Rate Limiting 的替代方案。

## 使用 Docker 執行

使用前請先安裝並啟動 Docker Desktop，確認 Docker Engine（daemon）正在執行。

    docker build -t ai-cicd-azure-lab .
    docker run --rm -p 8080:8080 ai-cicd-azure-lab

開啟 http://localhost:8080/。

若 Docker 顯示 Windows NuGet fallback package folder 或 ResolvePackageAssets 錯誤，請確認使用目前的 .dockerignore，並重新建置：

    docker build --no-cache -t ai-cicd-azure-lab .

## GitHub Actions

.github/workflows/ci.yml 會在 push 與 pull_request 時自動執行：

1. 設定 .NET 8
2. 還原相依套件
3. 執行 xUnit 測試
4. 執行 Python workflow 與前端靜態網站檢查
5. 驗證 AI 審查請求格式
6. 建立 Docker Image

實際部署 workflow 位於 `.github/workflows/azure-deploy.yml`，會在 `main` 分支 push 或手動觸發時，先執行測試，再透過 OIDC 將 image 推送至 ACR 並更新既有 Container App。啟用部署前，請依 [docs/azure-setup.md](docs/azure-setup.md) 設定 GitHub `demo` Environment、Entra Federated Credential 與最小範圍的 Azure 權限。未完成設定時，CI 不受影響；部署 workflow 會在設定檢查階段停止。

GitHub Pages 前端部署方式、Repository Variable、Container Apps CORS 設定與學員 fork 的設定步驟，請參閱 [docs/github-pages.md](docs/github-pages.md)。Pages workflow 只會在 `main` 的 Azure deploy workflow 成功後發佈該次程式碼版本。

`.github/workflow-templates/azure-deploy.yml` 是課堂教學參考檔，不會由本 Repository 執行，且只提供手動觸發。它保留供學員閱讀、比較或帶到其他 Repository 示範；實際部署請使用 `.github/workflows/azure-deploy.yml`，並依目標 Repository 重新設定 OIDC subject 與 Azure 權限。

`.github/workflows/ai-review.yml` 會在 Pull Request 開啟或更新時執行，沿用相同的 OIDC 設定登入 Azure，呼叫 Azure OpenAI 為這次差異產生審查建議，並留言回 Pull Request。這個 workflow 只提供建議，不會自動核准或修改程式碼；若 AI 呼叫失敗，審查檢查會顯示失敗並提示人工審查，獨立的 .NET CI 不受影響。啟用前請依 [docs/azure-openai-review.md](docs/azure-openai-review.md) 補上 `AZURE_OPENAI_ENDPOINT`、`AZURE_OPENAI_DEPLOYMENT` 兩個 Environment Variables，並確認該 service principal 已取得 Azure OpenAI 資源的呼叫權限。

`.github/workflows/ai-ci-diagnosis.yml` 會在 `.NET CI` 失敗後讀取失敗步驟日誌，遮罩常見機密並限制內容長度後，透過 Azure OpenAI 產生診斷建議；若 `.NET CI` 通過，則以成功的說明工作標示「無需診斷」，不會呼叫 Azure OpenAI。若失敗執行與 Pull Request 有關，workflow 會更新該 PR 的診斷留言；它不會自動修正程式碼、重新執行 CI 或變更失敗結果。啟用前請依 [docs/azure-openai-ci-diagnosis.md](docs/azure-openai-ci-diagnosis.md) 設定獨立的 OIDC Environment。

## 推送到 GitHub

在 GitHub 建立空白 Repository 後，於本資料夾執行：

    git add .
    git commit -m "改用 C# API 與 HTML JavaScript 前端"
    git remote add origin https://github.com/<帳號>/<Repository>.git
    git push -u origin main

請將帳號與 Repository 替換成實際值；不要把密碼、Token 或 Azure API Key 寫入指令、README 或版本庫。

## 安全原則

- 不提交 .env、API Key、密碼、Token 或私人憑證。
- 優先使用 GitHub Actions OIDC 與 Microsoft Entra ID 登入 Azure。
- Azure 權限限制在課程專用 Resource Group，不授予 Subscription Owner。
- AI 審查結果先作為建議，不讓 AI 自動合併或直接覆寫正式程式碼。
- 課程結束後停用或刪除課程用的 Azure 資源，避免持續產生成本。

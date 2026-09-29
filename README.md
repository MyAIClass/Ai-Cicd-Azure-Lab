# AI CI/CD Azure Lab

這是一個可直接推送到 GitHub 的課程示範 Repository，用來示範如何以 GitHub Actions 串接 Azure，完成從程式碼提交、測試、AI 輔助檢查到雲端部署的流程。

本版本使用 .NET 8 ASP.NET Core Minimal API 作為後端，使用 HTML、CSS 與原生 JavaScript 作為前端。前後端由同一個 ASP.NET Core 服務提供，因此學員只需要啟動一個服務即可完成示範。

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
- GET /api/greeting?name=小明：C# API
- /：HTML 與 JavaScript 前端

## 專案結構

    ai-cicd-azure-lab/
    ├─ .github/
    │  ├─ workflows/
    │  │  ├─ ci.yml
    │  │  ├─ azure-deploy.yml
    │  │  └─ ai-review.yml
    │  └─ workflow-templates/
    │     └─ azure-deploy.yml
    ├─ src/
    │  └─ AiCicdAzureLab.Api/
    │     ├─ Services/GreetingService.cs
    │     ├─ Program.cs
    │     ├─ AiCicdAzureLab.Api.csproj
    │     └─ wwwroot/
    │        ├─ index.html
    │        ├─ app.js
    │        └─ styles.css
    ├─ tests/
    │  └─ AiCicdAzureLab.Api.Tests/
    ├─ docs/
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
- http://localhost:5000/api/greeting?name=小明

實際連接埠可能依 .NET 開發環境設定而不同。

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
4. 驗證 AI 審查請求格式
5. 建立 Docker Image

實際部署 workflow 位於 `.github/workflows/azure-deploy.yml`，會在 `main` 分支 push 或手動觸發時，先執行測試，再透過 OIDC 將 image 推送至 ACR 並更新既有 Container App。啟用部署前，請依 [docs/azure-setup.md](docs/azure-setup.md) 設定 GitHub `demo` Environment、Entra Federated Credential 與最小範圍的 Azure 權限。未完成設定時，CI 不受影響；部署 workflow 會在設定檢查階段停止。

`.github/workflow-templates/azure-deploy.yml` 是課堂教學參考檔，不會由本 Repository 執行，且只提供手動觸發。它保留供學員閱讀、比較或帶到其他 Repository 示範；實際部署請使用 `.github/workflows/azure-deploy.yml`，並依目標 Repository 重新設定 OIDC subject 與 Azure 權限。

`.github/workflows/ai-review.yml` 會在 Pull Request 開啟或更新時執行，沿用相同的 OIDC 設定登入 Azure，呼叫 Azure OpenAI 為這次差異產生審查建議，並留言回 Pull Request。這個 workflow 只提供建議，不會自動核准或修改程式碼；若 AI 呼叫失敗，審查檢查會顯示失敗並提示人工審查，獨立的 .NET CI 不受影響。啟用前請依 [docs/azure-openai-review.md](docs/azure-openai-review.md) 補上 `AZURE_OPENAI_ENDPOINT`、`AZURE_OPENAI_DEPLOYMENT` 兩個 Environment Variables，並確認該 service principal 已取得 Azure OpenAI 資源的呼叫權限。

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

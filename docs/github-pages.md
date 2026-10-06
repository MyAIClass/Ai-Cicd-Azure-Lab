# GitHub Pages 前端部署

本 Repository 的靜態前端來源是 `src/AiCicdAzureLab.Api/wwwroot/`。GitHub Pages 發佈這個目錄；瀏覽器透過 HTTPS 呼叫 Azure Container Apps 上的 `/health` 與 `/api/*`。本機及 Container Apps 仍使用同一份前端，預設呼叫同源 API。

## 首次設定

1. 在 Repository 的 **Settings → Pages → Build and deployment** 將 Source 設為 **GitHub Actions**。
2. 在 **Settings → Secrets and variables → Actions → Variables** 新增 `API_BASE_URL`，值設為目前 Container App 的 HTTPS ingress 根網址。它是會編入公開 JavaScript 的 API 位址，不是秘密，不要建立成 Secret。
3. 在 Azure Container App 設定環境變數 `Cors__AllowedOrigins__0=https://myaiclass.github.io`，然後部署含有 CORS 支援的 API 版本。此值只包含來源的協定與網域，不含 `/Ai-Cicd-Azure-Lab/` 路徑。
4. 確認 Azure deploy workflow 在 `main` 成功後，`GitHub Pages deploy` workflow 會建置 `wwwroot`、產生公開的 `api-config.js`，並將網站部署至 `https://myaiClass.github.io/Ai-Cicd-Azure-Lab/`。

Pages workflow 會驗證 `API_BASE_URL` 是不含路徑、查詢字串或片段的 HTTPS 網址。若變數缺少或格式不正確，建置會失敗且不會發佈新版本。若 Azure deploy 失敗，Pages 保留上一次成功發佈的版本。

## 學員 fork

每個 fork 都要在自己的 Repository 設定 Pages Source 與 `API_BASE_URL`。若學員使用自己的 Container App，請在該 App 設定 `Cors__AllowedOrigins__0=https://<GitHub 帳號>.github.io`。若同一個 API 要支援其他 Pages 網站，可依序增加 `Cors__AllowedOrigins__1`、`Cors__AllowedOrigins__2` 等設定；每個值都只填來源網域，不加 repository 路徑，也不要設定 `*`。

API 的 CORS policy 只允許列出的來源、`GET`／`POST` 及 `Content-Type`，不使用 Cookie 憑證。CORS 會限制瀏覽器跨來源讀取，不會阻擋直接呼叫公開 API 的其他用戶端，也不取代後端驗證或授權。

## 故障排除

- Pages 建置失敗：確認 `API_BASE_URL` 是 Actions **Variable**，且值為 HTTPS ingress 根網址。
- 網頁樣式或 JavaScript 回應 404：確認靜態資源路徑維持相對路徑，Pages 網址含 repository 名稱路徑。
- 瀏覽器顯示 CORS 錯誤：確認 Container Apps 的 `Cors__AllowedOrigins__0` 包含 Pages 的來源網域，再確認 Azure deploy 成功。
- 網頁載入但 API 連線失敗：確認 `API_BASE_URL` 指向目前 Container App，並檢查 App 的 `/health`。

情感分析功能會呼叫 Azure OpenAI，與原本 ACA 網站相同，可能產生成本。將前端移到 Pages 不會讓 API 私有化。

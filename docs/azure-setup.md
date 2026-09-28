# Azure Container Apps 部署設定（第一階段）

本階段讓 GitHub Actions 以 GitHub Actions OIDC 登入 Microsoft Entra ID，將 Docker image 推送至 Azure Container Registry (ACR)，再更新既有的 Azure Container App。Workflow 不會建立或刪除 Azure 資源。

## 部署範圍

開始前由 Azure 管理者準備並確認以下既有資源：

- 課程專用 Resource Group。
- ACR，使用標準 Azure RBAC 權限模式。
- Container Apps Environment。
- Container App，HTTP ingress 的 target port 設為 `8080`。
- Container App 已啟用 system-assigned 或 user-assigned managed identity，並可透過該身分從 ACR 拉取 image。

第一階段不需要 Azure OpenAI、Application Insights 或額外監控資源；可在後續課程階段另行規劃。

## Microsoft Entra ID OIDC

1. 建立或選用專供此 Repository 部署的 Microsoft Entra ID app registration 與 service principal。
2. 在該 app registration 新增 Federated Credential：
   - **Issuer**：`https://token.actions.githubusercontent.com`
   - **Subject identifier**：`repo:MyAIClass/Ai-Cicd-Azure-Lab:environment:demo`
   - **Audience**：`api://AzureADTokenExchange`
3. 將下列 Azure 角色指派給該 service principal：
   - `AcrPush`：範圍限於課程 ACR。
   - `Contributor`：範圍限於要更新的單一 Container App 資源。
4. 將 `AcrPull` 指派給 Container App 使用的 managed identity，範圍限於課程 ACR。Container App 的 ACR 登錄設定必須使用同一個 managed identity，不能依賴 ACR admin 帳密。

不要將 Azure client secret、ACR 密碼或其他長期憑證加入 Repository 或 GitHub Variables/Secrets。OIDC 使用短期權杖；Workflow 只需設定 `id-token: write` 權限。

## GitHub Environment 設定

在 GitHub Repository 的 **Settings → Environments** 建立名為 `demo` 的 Environment，並設定以下 Variables：

| Variable | 內容 |
| --- | --- |
| `AZURE_CLIENT_ID` | Entra app registration 的 Application (client) ID |
| `AZURE_TENANT_ID` | Microsoft Entra tenant ID |
| `AZURE_SUBSCRIPTION_ID` | 課程 Azure subscription ID |
| `AZURE_RESOURCE_GROUP` | Container App 與 ACR 所在的 Resource Group 名稱 |
| `AZURE_CONTAINER_REGISTRY` | ACR 的 registry name，不含網域 |
| `AZURE_CONTAINER_APP` | 既有 Container App 名稱 |

未知值請由 Azure 管理者確認後填入，不要猜測。Workflow 會用 Azure CLI 從 ACR 查詢 login server，因此不需要另設 `AZURE_CONTAINER_REGISTRY_LOGIN_SERVER`。如果 ACR 位於不同 Resource Group，需同步調整 Workflow，增加獨立的 ACR Resource Group 設定。

可為 `demo` Environment 加上部署核准者限制，並確保只有受信任的 Repository 維護者可以修改 Environment Variables 及觸發正式部署。

## Workflow 行為

[`.github/workflows/azure-deploy.yml`](../.github/workflows/azure-deploy.yml) 是本 Repository 實際執行的部署 workflow，會在 `main` 分支 push 或手動觸發時執行，且會拒絕非 `main` 分支的手動部署。工作順序如下：

1. 檢查必要的 Environment Variables。
2. 還原套件並執行 .NET 測試；測試失敗時不會登入 Azure。
3. 使用 `azure/login@v2` 透過 OIDC 登入 Azure。
4. 安裝 Azure Container Apps CLI extension。
5. 建置並推送 `ai-cicd-azure-lab:<commit SHA>` 至 ACR。
6. 將既有 Container App 更新至該 image。

`.github/workflow-templates/azure-deploy.yml` 則是課堂閱讀與跨 Repository 示範用的教學參考，不會在這個 Repository 執行，且只有手動觸發。兩份檔案的測試、OIDC 登入、image push 與更新步驟保持一致；帶到其他 Repository 前，需先調整觸發方式、branch、Environment Variables、OIDC Federated Credential subject 與 Azure 權限。

部署前確認 GitHub 的 CI 檢查已通過。此部署 workflow 會再次執行測試，避免部署未經測試的版本。成功後可從 Container App 的 ingress URL 檢查 `/health` 與首頁。

## 尚待確認的 Azure 識別資料

目前 Repository 沒有足夠資訊確認下列值，需由資源管理者查明後填到 `demo` Environment：

- Entra app registration / service principal 的 Application (client) ID。
- Microsoft Entra tenant ID。
- Azure subscription ID。
- Resource Group 名稱。
- ACR registry name 與其是否位於同一 Resource Group。
- Container App 名稱，以及其 ingress target port 是否為 `8080`。
- Container App 實際使用的 managed identity，及其 ACR `AcrPull` 權限是否已完成。
- OIDC Federated Credential 與上述 Azure role assignments 是否已設定。

這份 Repository 變更不會代為建立 Azure 資源、Federated Credential 或角色指派。

## 預演檢查

- 確認 CI 的測試與 Docker build 通過。
- 在 `demo` Environment 設定必要 Variables，確認無 client secret 或 ACR admin 密碼。
- 從 `main` 執行部署，確認 OIDC 登入、image push 與 Container App 更新成功。
- 開啟 Container App ingress URL，確認 `/health` 回傳 `{"status":"ok"}`。
- 確認 Container App revision 使用的 image tag 為部署 commit SHA。
- 發生登入或權限錯誤時，核對 Federated Credential 的 issuer、subject、audience，以及角色指派範圍；不要改用長期共用金鑰。

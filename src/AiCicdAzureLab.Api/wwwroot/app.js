const greetingForm = document.querySelector("#greeting-form");
const nameInput = document.querySelector("#name");
const captchaAnswerInput = document.querySelector("#captcha-answer");
const captchaImage = document.querySelector("#captcha-image");
const refreshCaptchaButton = document.querySelector("#refresh-captcha");
const submitButton = greetingForm.querySelector('button[type="submit"]');
const result = document.querySelector("#result");
const dailyQuote = document.querySelector("#daily-quote");
const healthStatus = document.querySelector("#health-status");
const statusBadge = document.querySelector("#status-badge");

let captchaId = null;
let captchaImageUrl = null;

async function loadCaptcha() {
  refreshCaptchaButton.disabled = true;

  try {
    const previousCaptchaId = captchaId ? "?previousCaptchaId=" + encodeURIComponent(captchaId) : "";
    const response = await fetch("/api/captcha" + previousCaptchaId);
    if (!response.ok) {
      throw new Error("驗證碼 API 回應 " + response.status);
    }

    const data = await response.json();
    const imageBlob = new Blob([data.imageSvg], { type: "image/svg+xml" });
    const nextImageUrl = URL.createObjectURL(imageBlob);

    if (captchaImageUrl) {
      URL.revokeObjectURL(captchaImageUrl);
    }

    captchaId = data.captchaId;
    captchaImageUrl = nextImageUrl;
    captchaImage.src = captchaImageUrl;
    captchaImage.alt = "請輸入圖片中的五碼驗證文字";
    captchaAnswerInput.value = "";
  } catch (error) {
    captchaId = null;
    captchaImage.removeAttribute("src");
    captchaImage.alt = "驗證碼載入失敗";
    result.textContent = "驗證碼載入失敗：" + error.message;
  } finally {
    refreshCaptchaButton.disabled = false;
  }
}

refreshCaptchaButton.addEventListener("click", loadCaptcha);

greetingForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const name = nameInput.value.trim();
  const captchaAnswer = captchaAnswerInput.value.trim();

  if (!captchaId) {
    result.textContent = "驗證碼尚未載入，請換一張後再試。";
    return;
  }

  result.textContent = "呼叫 API 中⋯";
  submitButton.disabled = true;

  try {
    const response = await fetch("/api/greeting", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name, captchaId, captchaAnswer })
    });

    if (!response.ok) {
      const error = await response.json().catch(() => null);
      throw new Error(error?.error || "API 回應 " + response.status);
    }

    const data = await response.json();
    result.textContent = data.message + "（服務：" + data.service + "）";
    await loadCaptcha();
  } catch (error) {
    result.textContent = "呼叫失敗：" + error.message;
  } finally {
    submitButton.disabled = false;
  }
});

async function checkHealth() {
  try {
    const response = await fetch("/health");
    if (!response.ok) {
      throw new Error("Health API 回應 " + response.status);
    }

    healthStatus.textContent = "C# API 運作正常";
    statusBadge.textContent = "ONLINE";
    statusBadge.classList.add("online");
  } catch (error) {
    healthStatus.textContent = "無法連線：" + error.message;
    statusBadge.textContent = "OFFLINE";
  }
}

checkHealth();
loadCaptcha();

async function loadDailyQuote() {
  try {
    const response = await fetch("/api/daily-quote");
    if (!response.ok) {
      throw new Error("Daily Quote API 回應 " + response.status);
    }

    const data = await response.json();
    dailyQuote.textContent = data.quote;
  } catch (error) {
    dailyQuote.textContent = "今日小語載入失敗：" + error.message;
  }
}

loadDailyQuote();

const challengeButton = document.querySelector("#challenge-button");
const challengeResult = document.querySelector("#challenge-result");

challengeButton.addEventListener("click", async () => {
  challengeResult.textContent = "抽取任務中⋯";
  challengeButton.disabled = true;

  try {
    const response = await fetch("/api/challenge");

    if (!response.ok) {
      throw new Error("API 回應 " + response.status);
    }

    const data = await response.json();

    challengeResult.textContent =
      data.title + "：" + data.description;
  } catch (error) {
    challengeResult.textContent = "抽取失敗：" + error.message;
  } finally {
    challengeButton.disabled = false;
  }
});

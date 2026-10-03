const greetingForm = document.querySelector("#greeting-form");
const nameInput = document.querySelector("#name");
const result = document.querySelector("#result");
const healthStatus = document.querySelector("#health-status");
const statusBadge = document.querySelector("#status-badge");
const captchaImage = document.querySelector("#captcha-image");
const captchaAnswer = document.querySelector("#captcha-answer");
const captchaRefresh = document.querySelector("#captcha-refresh");
const greetingSubmit = document.querySelector("#greeting-submit");
let captchaToken = "";

async function loadCaptcha() {
  captchaRefresh.disabled = true;

  try {
    const response = await fetch("/api/captcha");
    if (!response.ok) {
      throw new Error("無法取得驗證碼");
    }

    const data = await response.json();
    captchaToken = data.token;
    captchaImage.src = data.imageUrl;
    captchaAnswer.value = "";
  } finally {
    captchaRefresh.disabled = false;
  }
}

greetingForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const name = nameInput.value.trim();
  result.textContent = "呼叫 API 中⋯";
  greetingSubmit.disabled = true;
  captchaRefresh.disabled = true;

  try {
    const response = await fetch("/api/greeting", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        name,
        captchaToken,
        captchaAnswer: captchaAnswer.value.trim(),
      }),
    });
    const data = await response.json().catch(() => ({}));

    if (!response.ok) {
      throw new Error(data.error || "API 回應 " + response.status);
    }

    result.textContent = data.message + "（服務：" + data.service + "）";
  } catch (error) {
    result.textContent = "呼叫失敗：" + error.message;
  } finally {
    greetingSubmit.disabled = false;
    try {
      await loadCaptcha();
    } catch (error) {
      result.textContent = "驗證碼載入失敗：" + error.message;
    }
  }
});

captchaRefresh.addEventListener("click", async () => {
  try {
    await loadCaptcha();
    result.textContent = "已換發新的驗證碼";
  } catch (error) {
    result.textContent = "驗證碼載入失敗：" + error.message;
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
loadCaptcha().catch((error) => {
  result.textContent = "驗證碼載入失敗：" + error.message;
});

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

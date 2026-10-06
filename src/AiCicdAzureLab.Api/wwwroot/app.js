const greetingForm = document.querySelector("#greeting-form");
const configuredApiBaseUrl = window.APP_CONFIG?.apiBaseUrl?.trim().replace(/\/+$/, "");
const apiBaseUrl = configuredApiBaseUrl || window.location.origin;

function apiUrl(path) {
  return new URL(path, `${apiBaseUrl}/`).toString();
}

const nameInput = document.querySelector("#name");
const result = document.querySelector("#result");
const dailyQuote = document.querySelector("#daily-quote");
const healthStatus = document.querySelector("#health-status");
const statusBadge = document.querySelector("#status-badge");
const captchaImage = document.querySelector("#captcha-image");
const captchaAnswer = document.querySelector("#captcha-answer");
const captchaRefresh = document.querySelector("#refresh-captcha");
const greetingSubmit = greetingForm.querySelector('button[type="submit"]');
const loginButton = document.querySelector("#login-button");
const loginStatus = document.querySelector("#login-status");
let captchaToken = "";

loginButton.addEventListener("click", () => {
  const isLoggedIn = loginButton.getAttribute("aria-pressed") === "true";
  loginButton.setAttribute("aria-pressed", String(!isLoggedIn));
  loginButton.textContent = isLoggedIn ? "Login" : "Logout";
  loginStatus.textContent = isLoggedIn ? "尚未登入" : "已登入（示範模式）";
});

async function loadCaptcha() {
  captchaRefresh.disabled = true;

  try {
    const response = await fetch(apiUrl("/api/captcha"));
    if (!response.ok) {
      throw new Error("驗證碼 API 回應 " + response.status);
    }

    const data = await response.json();
    captchaToken = data.token;
    captchaImage.src = apiUrl(data.imageUrl);
    captchaAnswer.value = "";
  } catch (error) {
    captchaToken = "";
    captchaImage.removeAttribute("src");
    captchaImage.alt = "驗證碼載入失敗";
    throw error;
  } finally {
    captchaRefresh.disabled = false;
  }
}

captchaRefresh.addEventListener("click", async () => {
  try {
    await loadCaptcha();
    result.textContent = "已換發新的驗證碼";
  } catch (error) {
    result.textContent = "驗證碼載入失敗：" + error.message;
  }
});

greetingForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const name = nameInput.value.trim();
  const captchaAnswerValue = captchaAnswer.value.trim();

  if (!captchaToken) {
    result.textContent = "驗證碼尚未載入，請換一張後再試。";
    return;
  }

  result.textContent = "呼叫 API 中⋯";
  greetingSubmit.disabled = true;
  captchaRefresh.disabled = true;

  try {
    const response = await fetch(apiUrl("/api/greeting"), {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        name,
        captchaToken,
        captchaAnswer: captchaAnswerValue,
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

async function checkHealth() {
  try {
    const response = await fetch(apiUrl("/health"));
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

async function loadDailyQuote() {
  try {
    const response = await fetch(apiUrl("/api/daily-quote"));
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
let challengeCandidates = [];
const challengeAnimationDurationMs = 6000;
const challengeAnimationIntervalMs = 600;

async function loadChallengeCandidates() {
  const response = await fetch(apiUrl("/api/challenges"));

  if (!response.ok) {
    throw new Error("任務候選清單 API 回應 " + response.status);
  }

  const data = await response.json();
  challengeCandidates = data.map((candidate) => candidate.title);
}

function pickRandomChallengeMessage() {
  const randomIndex = Math.floor(
    Math.random() * challengeCandidates.length,
  );

  return challengeCandidates[randomIndex] + "⋯";
}

function animateChallengeResult(output) {
  output.textContent = pickRandomChallengeMessage();

  const animationInterval = window.setInterval(() => {
    output.textContent = pickRandomChallengeMessage();
  }, challengeAnimationIntervalMs);

  return new Promise((resolve) => {
    window.setTimeout(() => {
      window.clearInterval(animationInterval);
      resolve();
    }, challengeAnimationDurationMs);
  });
}

const sentimentText = document.querySelector("#sentiment-text");
const sentimentCount = document.querySelector("#sentiment-count");
const sentimentStatus = document.querySelector("#sentiment-status");
const sentimentNeedle = document.querySelector("#sentiment-needle");
const sentimentLabel = document.querySelector("#sentiment-label");
const sentimentScore = document.querySelector("#sentiment-score");
const sentimentConfidence = document.querySelector("#sentiment-confidence");
const sentimentSummary = document.querySelector("#sentiment-summary");
const sentimentError = document.querySelector("#sentiment-error");
let sentimentController;
let pendingSentimentText;
let lastAnalyzedSentimentText;

function resetSentiment() {
  sentimentStatus.textContent = "等待輸入";
  sentimentStatus.className = "sentiment-status";
  sentimentNeedle.style.transform = "rotate(0deg)";
  sentimentLabel.textContent = "等待輸入";
  sentimentScore.textContent = "分數：—";
  sentimentConfidence.textContent = "信心度：—";
  sentimentSummary.textContent = "輸入評論並離開輸入框後，這裡會顯示分析摘要。";
  sentimentError.textContent = "";
}

function applySentiment(data) {
  const score = Number(data.polarity);
  const labelMap = { negative: "憤怒", neutral: "中性", positive: "開心" };
  const className = data.label === "negative" ? "negative" : data.label === "positive" ? "positive" : "neutral";
  sentimentNeedle.style.transform = `rotate(${score * 90}deg)`;
  sentimentLabel.textContent = labelMap[data.label] || "中性";
  sentimentScore.textContent = `分數：${score.toFixed(2)}`;
  sentimentConfidence.textContent = `信心度：${(Number(data.confidence) * 100).toFixed(0)}%`;
  sentimentSummary.textContent = data.summary;
  sentimentStatus.textContent = labelMap[data.label] || "中性";
  sentimentStatus.className = `sentiment-status ${className}`;
  sentimentError.textContent = "";
}

async function analyzeSentiment() {
  const text = sentimentText.value.trim();
  sentimentCount.textContent = `${sentimentText.value.length} / 500`;
  if (text.length > 0 && (text === pendingSentimentText || text === lastAnalyzedSentimentText)) {
    return;
  }
  if (sentimentController) {
    sentimentController.abort();
  }
  if (text.length === 0) {
    resetSentiment();
    return;
  }
  if (text.length < 2) {
    sentimentStatus.textContent = "至少輸入 2 個字";
    return;
  }

  const controller = new AbortController();
  sentimentController = controller;
  pendingSentimentText = text;
  sentimentStatus.textContent = "分析中⋯";
  sentimentStatus.className = "sentiment-status loading";
  try {
    const response = await fetch(apiUrl("/api/sentiment/analyze"), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ text }),
      signal: controller.signal,
    });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) {
      if (response.status === 429) {
        throw new Error("分析請求太頻繁，請稍後再試。");
      }
      throw new Error(data.detail || data.error || "情感分析失敗");
    }
    applySentiment(data);
    lastAnalyzedSentimentText = text;
  } catch (error) {
    if (error.name === "AbortError") return;
    const isConfigurationError = error.message.includes("尚未完成設定");
    sentimentError.textContent = isConfigurationError
      ? "請先設定 Azure OpenAI Endpoint 與 Deployment，設定完成後重新啟動服務。"
      : `分析失敗，目前結果可能不是最新：${error.message}`;
    sentimentStatus.textContent = isConfigurationError ? "尚未設定" : "暫時無法分析";
    sentimentStatus.className = "sentiment-status error";
  } finally {
    if (sentimentController === controller) {
      sentimentController = null;
      pendingSentimentText = null;
    }
  }
}

sentimentText.addEventListener("input", () => {
  sentimentCount.textContent = `${sentimentText.value.length} / 500`;
  if (sentimentController) {
    sentimentController.abort();
    sentimentController = null;
  }
  pendingSentimentText = null;
  lastAnalyzedSentimentText = null;
  resetSentiment();
  if (sentimentText.value.trim().length > 0) {
    sentimentStatus.textContent = "離開輸入框後分析";
  }
});
sentimentText.addEventListener("keydown", (event) => {
  if (event.key !== "Enter" || !event.ctrlKey || event.isComposing || event.repeat) {
    return;
  }
  event.preventDefault();
  sentimentText.blur();
});
sentimentText.addEventListener("blur", analyzeSentiment);

challengeButton.addEventListener("click", async () => {
  challengeButton.disabled = true;
  let animationFinished;

  try {
    if (challengeCandidates.length === 0) {
      await loadChallengeCandidates();
    }

    animationFinished = animateChallengeResult(challengeResult);
    const response = await fetch(apiUrl("/api/challenge"));

    if (!response.ok) {
      throw new Error("API 回應 " + response.status);
    }

    const data = await response.json();

    await animationFinished;
    challengeResult.textContent =
      data.title + "：" + data.description;
  } catch (error) {
    if (animationFinished) {
      await animationFinished;
    }

    challengeResult.textContent = "抽取失敗：" + error.message;
  } finally {
    challengeButton.disabled = false;
  }
});

loadChallengeCandidates().catch(() => {
  challengeCandidates = [];
});

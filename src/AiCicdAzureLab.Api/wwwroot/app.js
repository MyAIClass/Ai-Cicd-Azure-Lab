const greetingForm = document.querySelector("#greeting-form");
const nameInput = document.querySelector("#name");
const result = document.querySelector("#result");
const dailyQuote = document.querySelector("#daily-quote");
const quoteCard = document.querySelector(".quote-card");

const quoteThemes = [
  { start: "#ffd166", end: "#fff1bf", text: "#713f12", border: "#e0a800", page: "#ffe8a3" },
  { start: "#90cdf4", end: "#d9f0ff", text: "#075985", border: "#3182ce", page: "#c7e7ff" },
  { start: "#9ae6b4", end: "#dcfce7", text: "#166534", border: "#38a169", page: "#c6f6d5" },
  { start: "#d6bcfa", end: "#f0e5ff", text: "#6b21a8", border: "#805ad5", page: "#e9d8fd" },
  { start: "#fbb6ce", end: "#ffe4ed", text: "#9f1239", border: "#d53f8c", page: "#fed7e2" }
];
let currentQuoteTheme = -1;
const healthStatus = document.querySelector("#health-status");
const statusBadge = document.querySelector("#status-badge");
const captchaImage = document.querySelector("#captcha-image");
const captchaAnswer = document.querySelector("#captcha-answer");
const captchaRefresh = document.querySelector("#refresh-captcha");
const greetingSubmit = greetingForm.querySelector('button[type="submit"]');
let captchaToken = "";

async function loadCaptcha() {
  captchaRefresh.disabled = true;

  try {
    const response = await fetch("/api/captcha");
    if (!response.ok) {
      throw new Error("驗證碼 API 回應 " + response.status);
    }

    const data = await response.json();
    captchaToken = data.token;
    captchaImage.src = data.imageUrl;
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
    const response = await fetch("/api/greeting", {
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

function applyQuoteTheme() {
  let nextTheme = Math.floor(Math.random() * quoteThemes.length);

  while (quoteThemes.length > 1 && nextTheme === currentQuoteTheme) {
    nextTheme = Math.floor(Math.random() * quoteThemes.length);
  }

  currentQuoteTheme = nextTheme;
  const theme = quoteThemes[nextTheme];
  quoteCard.style.setProperty("--quote-start", theme.start);
  quoteCard.style.setProperty("--quote-end", theme.end);
  quoteCard.style.setProperty("--quote-text", theme.text);
  quoteCard.style.setProperty("--quote-border", theme.border);
  quoteCard.style.background = `linear-gradient(135deg, ${theme.start}, ${theme.end})`;
  quoteCard.style.borderColor = theme.border;
}

async function loadDailyQuote() {
  try {
    const response = await fetch("/api/daily-quote", { cache: "no-store" });
    if (!response.ok) {
      throw new Error("Daily Quote API 回應 " + response.status);
    }

    const data = await response.json();
    dailyQuote.textContent = data.quote;
    applyQuoteTheme();
  } catch (error) {
    dailyQuote.textContent = "今日小語載入失敗：" + error.message;
  }
}
loadDailyQuote();
setInterval(loadDailyQuote, 10000);

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

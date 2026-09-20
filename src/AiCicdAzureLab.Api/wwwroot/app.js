const greetingForm = document.querySelector("#greeting-form");
const nameInput = document.querySelector("#name");
const result = document.querySelector("#result");
const healthStatus = document.querySelector("#health-status");
const statusBadge = document.querySelector("#status-badge");

greetingForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const name = nameInput.value.trim();
  result.textContent = "呼叫 API 中⋯";

  try {
    const response = await fetch("/api/greeting?name=" + encodeURIComponent(name));
    if (!response.ok) {
      throw new Error("API 回應 " + response.status);
    }

    const data = await response.json();
    result.textContent = data.message + "（服務：" + data.service + "）";
  } catch (error) {
    result.textContent = "呼叫失敗：" + error.message;
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
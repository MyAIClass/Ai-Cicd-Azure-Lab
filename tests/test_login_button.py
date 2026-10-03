from pathlib import Path
import unittest


ROOT = Path(__file__).parents[1]
INDEX = ROOT / "src" / "AiCicdAzureLab.Api" / "wwwroot" / "index.html"
APP = ROOT / "src" / "AiCicdAzureLab.Api" / "wwwroot" / "app.js"


class LoginButtonTests(unittest.TestCase):
    def test_login_button_and_status_are_present(self):
        html = INDEX.read_text(encoding="utf-8")
        self.assertIn('id="login-button"', html)
        self.assertIn('id="login-status"', html)

    def test_login_button_has_toggle_event(self):
        javascript = APP.read_text(encoding="utf-8")
        self.assertIn('loginButton.addEventListener("click"', javascript)
        self.assertIn("aria-pressed", javascript)
        self.assertIn("已登入（示範模式）", javascript)


if __name__ == "__main__":
    unittest.main()

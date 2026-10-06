from pathlib import Path
import tempfile
import unittest

from scripts.build_pages import build_site, normalize_api_base_url


ROOT = Path(__file__).parents[1]
WWWROOT = ROOT / "src" / "AiCicdAzureLab.Api" / "wwwroot"
INDEX = WWWROOT / "index.html"
APP = WWWROOT / "app.js"
WORKFLOW = ROOT / ".github" / "workflows" / "pages-deploy.yml"


class FrontendPagesTests(unittest.TestCase):
    def test_static_asset_paths_are_relative_and_config_loads_before_app(self):
        html = INDEX.read_text(encoding="utf-8")

        self.assertIn('href="styles.css"', html)
        self.assertIn('src="api-config.js"', html)
        self.assertIn('src="app.js"', html)
        self.assertLess(html.index('src="api-config.js"'), html.index('src="app.js"'))
        self.assertNotIn('href="/styles.css"', html)
        self.assertNotIn('src="/app.js"', html)

    def test_all_frontend_api_requests_and_captcha_image_use_api_url_helper(self):
        javascript = APP.read_text(encoding="utf-8")
        expected_requests = (
            'fetch(apiUrl("/api/captcha"))',
            'fetch(apiUrl("/api/greeting")',
            'fetch(apiUrl("/health"))',
            'fetch(apiUrl("/api/daily-quote"))',
            'fetch(apiUrl("/api/challenges"))',
            'fetch(apiUrl("/api/sentiment/analyze")',
            'fetch(apiUrl("/api/challenge"))',
        )

        for request in expected_requests:
            with self.subTest(request=request):
                self.assertIn(request, javascript)

        self.assertNotRegex(javascript, r'fetch\(\s*["\']')
        self.assertIn("captchaImage.src = apiUrl(data.imageUrl)", javascript)
        self.assertIn("new URL(path, `${apiBaseUrl}/`)", javascript)

    def test_pages_build_requires_https_api_origin_and_generates_public_config(self):
        self.assertEqual(
            "https://app.example.azurecontainerapps.io",
            normalize_api_base_url("https://app.example.azurecontainerapps.io/"),
        )
        for invalid_url in (None, "", "http://app.example.com", "https://app.example.com/api", "https://app.example.com?x=1"):
            with self.subTest(invalid_url=invalid_url):
                with self.assertRaises(ValueError):
                    normalize_api_base_url(invalid_url)

        with tempfile.TemporaryDirectory() as temporary_directory:
            destination = Path(temporary_directory) / "site"
            build_site(WWWROOT, destination, "https://app.example.azurecontainerapps.io/")

            self.assertTrue((destination / "index.html").is_file())
            self.assertTrue((destination / "styles.css").is_file())
            self.assertTrue((destination / "app.js").is_file())
            config = (destination / "api-config.js").read_text(encoding="utf-8")
            self.assertIn('apiBaseUrl: "https://app.example.azurecontainerapps.io"', config)

    def test_pages_build_fails_when_api_base_url_is_missing(self):
        with tempfile.TemporaryDirectory() as temporary_directory:
            destination = Path(temporary_directory) / "site"
            with self.assertRaisesRegex(ValueError, "API_BASE_URL is required"):
                build_site(WWWROOT, destination, None)
            self.assertFalse(destination.exists())

    def test_pages_publish_waits_for_successful_main_azure_deploy(self):
        workflow = WORKFLOW.read_text(encoding="utf-8")

        self.assertIn('workflows: ["Azure deploy"]', workflow)
        self.assertIn("branches:\n      - main", workflow)
        self.assertIn("github.event.workflow_run.conclusion == 'success'", workflow)
        self.assertIn("github.event.workflow_run.head_branch == 'main'", workflow)
        self.assertIn("API_BASE_URL: ${{ vars.API_BASE_URL }}", workflow)
        self.assertIn("pages: write", workflow)
        self.assertIn("id-token: write", workflow)


if __name__ == "__main__":
    unittest.main()

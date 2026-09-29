import json
import os
import subprocess
import tempfile
import unittest
from pathlib import Path


WORKFLOW = Path(__file__).resolve().parents[1] / ".github/workflows/ai-review.yml"


class AiReviewRequestTests(unittest.TestCase):
    def test_reasoning_model_request_uses_supported_parameters(self):
        workflow = WORKFLOW.read_text(encoding="utf-8")
        start = "          python3 - <<'PY'\n"
        end = "\n          PY"
        script = workflow.split(start, 1)[1].split(end, 1)[0]
        script = "\n".join(line.removeprefix("          ") for line in script.splitlines())

        with tempfile.TemporaryDirectory() as directory:
            folder = Path(directory)
            (folder / "diff_for_review.txt").write_text(
                "+新增測試\n", encoding="utf-8"
            )
            env = {**os.environ, "AZURE_OPENAI_DEPLOYMENT": "gpt-6-luna"}
            subprocess.run(
                ["python", "-c", script], cwd=folder, env=env, check=True
            )
            payload = json.loads((folder / "payload.json").read_text(encoding="utf-8"))

        self.assertEqual(payload["model"], "gpt-6-luna")
        self.assertEqual(payload["max_completion_tokens"], 4096)
        self.assertNotIn("max_tokens", payload)
        self.assertNotIn("temperature", payload)
        self.assertIn("+新增測試", payload["messages"][1]["content"])


if __name__ == "__main__":
    unittest.main()

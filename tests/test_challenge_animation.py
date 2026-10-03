import re
import unittest
from pathlib import Path


APP_JS = (
    Path(__file__).parents[1]
    / "src"
    / "AiCicdAzureLab.Api"
    / "wwwroot"
    / "app.js"
)


class ChallengeAnimationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = APP_JS.read_text(encoding="utf-8")

    def test_animation_cycles_and_stops_after_six_seconds(self):
        self.assertIn("window.setInterval", self.source)
        self.assertIn("window.clearInterval(animationInterval)", self.source)
        self.assertIn("Math.random()", self.source)
        self.assertIn("pickRandomChallengeMessage", self.source)
        self.assertIn('fetch("/api/challenges")', self.source)
        self.assertIn('candidate.title', self.source)
        self.assertIn("challengeAnimationDurationMs = 6000", self.source)


if __name__ == "__main__":
    unittest.main()

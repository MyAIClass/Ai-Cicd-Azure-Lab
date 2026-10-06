import json
import os
from pathlib import Path
import shutil
import sys
from urllib.parse import urlsplit, urlunsplit


ROOT = Path(__file__).resolve().parents[1]
WWWROOT = ROOT / "src" / "AiCicdAzureLab.Api" / "wwwroot"
OUTPUT = ROOT / "_site"


def normalize_api_base_url(value: str | None) -> str:
    if not value or not value.strip():
        raise ValueError("GitHub Actions variable API_BASE_URL is required.")

    parsed = urlsplit(value.strip())
    if (
        parsed.scheme.lower() != "https"
        or not parsed.hostname
        or parsed.username
        or parsed.password
        or parsed.path not in ("", "/")
        or parsed.query
        or parsed.fragment
    ):
        raise ValueError("API_BASE_URL must be an HTTPS origin without a path, query, or fragment.")

    try:
        parsed.port
    except ValueError as error:
        raise ValueError("API_BASE_URL contains an invalid port.") from error

    return urlunsplit(("https", parsed.netloc, "", "", ""))


def build_site(source: Path, destination: Path, api_base_url: str | None) -> None:
    normalized_api_base_url = normalize_api_base_url(api_base_url)
    if not source.is_dir():
        raise ValueError(f"Static site source directory does not exist: {source}")
    if destination.exists():
        raise ValueError(f"Output directory already exists; remove it before building: {destination}")

    shutil.copytree(source, destination)
    config = (
        "window.APP_CONFIG = Object.freeze({\n"
        f"  apiBaseUrl: {json.dumps(normalized_api_base_url)},\n"
        "});\n"
    )
    (destination / "api-config.js").write_text(config, encoding="utf-8")


def main() -> int:
    try:
        build_site(WWWROOT, OUTPUT, os.environ.get("API_BASE_URL"))
    except ValueError as error:
        print(error, file=sys.stderr)
        return 1

    print(f"GitHub Pages artifact created at {OUTPUT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

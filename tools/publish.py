"""Publish the Release build's zip to Thunderstore with tcli.

Usage:
  python -I tools/publish.py check
  python -I tools/publish.py publish

check verifies, without uploading, that artifacts/BetterCoop-<version>.zip
exists for BetterCoop.csproj's <Version>, that its manifest.json matches,
that thunderstore.toml names a team, that the version is newer than
Thunderstore's latest, and that a token is available. publish runs the same
checks, then uploads the zip with the repository's pinned tcli
(.config/dotnet-tools.json).
A version cannot be uploaded twice, so publish only on purpose.

The token is a Thunderstore service account token (tss_...), read from the
TCLI_AUTH_TOKEN environment variable or else from the git-ignored file
.thunderstore-token at the repository root. It reaches tcli through the
environment, never the command line, and is never printed.
"""

import json
import os
import re
import subprocess
import sys
import tomllib
import urllib.error
import urllib.request
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
PROJECT = ROOT / "src" / "BetterCoop" / "BetterCoop.csproj"
TOML = ROOT / "thunderstore.toml"
TOKEN_FILE = ROOT / ".thunderstore-token"
TOKEN_ENV = "TCLI_AUTH_TOKEN"
REQUIRED_FILES = {"manifest.json", "icon.png", "README.md"}
USER_AGENT = "peak-better-coop-publish"


class CheckFailed(Exception):
    pass


def project_version() -> str:
    match = re.search(r"<Version>([^<]+)</Version>", PROJECT.read_text(encoding="utf-8"))
    if not match:
        raise CheckFailed(f"no <Version> in {PROJECT}")
    return match.group(1).strip()


def check_zip(archive: Path, name: str, version: str) -> None:
    if not archive.is_file():
        raise CheckFailed(f"{archive} not found; run `dotnet build -c Release` first")
    with zipfile.ZipFile(archive) as package:
        missing = REQUIRED_FILES - set(package.namelist())
        if missing:
            raise CheckFailed(f"{archive.name} lacks {', '.join(sorted(missing))}")
        manifest = json.loads(package.read("manifest.json").decode("utf-8-sig"))
    if manifest.get("name") != name:
        raise CheckFailed(f"manifest name {manifest.get('name')!r} is not {name!r}")
    if manifest.get("version_number") != version:
        raise CheckFailed(
            f"manifest version {manifest.get('version_number')!r} is not {version!r}; "
            "rebuild with `dotnet build -c Release`"
        )


def latest_version(namespace: str, name: str) -> str | None:
    # The per-version endpoint answers 404 for a freshly published, unreviewed
    # version, so read the package's latest version instead.
    url = f"https://thunderstore.io/api/experimental/package/{namespace}/{name}/"
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    try:
        with urllib.request.urlopen(request) as response:
            return json.load(response)["latest"]["version_number"]
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return None
        raise


def semver(version: str) -> tuple[int, ...]:
    return tuple(int(part) for part in version.split("."))


def read_token() -> str:
    token = os.environ.get(TOKEN_ENV, "").strip()
    if not token and TOKEN_FILE.is_file():
        token = TOKEN_FILE.read_text(encoding="utf-8").strip()
    if not token:
        raise CheckFailed(f"no token: set {TOKEN_ENV} or write it to {TOKEN_FILE.name}")
    if not token.startswith("tss_"):
        raise CheckFailed("the token is not a service account token (tss_...)")
    return token


def check() -> tuple[Path, str]:
    config = tomllib.loads(TOML.read_text(encoding="utf-8"))
    namespace = config["package"]["namespace"].strip()
    name = config["package"]["name"]
    if not namespace:
        raise CheckFailed(f"set [package] namespace in {TOML.name} to the Thunderstore team")
    version = project_version()
    archive = ROOT / "artifacts" / f"{name}-{version}.zip"
    check_zip(archive, name, version)
    full_name = f"{namespace}-{name}-{version}"
    latest = latest_version(namespace, name)
    if latest is not None and semver(version) <= semver(latest):
        raise CheckFailed(f"Thunderstore already has {latest}; bump <Version> past it")
    token = read_token()
    communities = ", ".join(config["publish"]["communities"])
    print(f"ready: {full_name} from {archive.relative_to(ROOT)} to {communities}", flush=True)
    return archive, token


def publish() -> None:
    archive, token = check()
    subprocess.run(["dotnet", "tool", "restore"], cwd=ROOT, check=True)
    subprocess.run(
        ["dotnet", "tool", "run", "tcli", "publish",
         "--config-path", str(TOML), "--file", str(archive)],
        cwd=ROOT,
        env={**os.environ, TOKEN_ENV: token},
        check=True,
    )


def main(argv: list[str]) -> int:
    commands = {"check": check, "publish": publish}
    if len(argv) != 1 or argv[0] not in commands:
        print(__doc__.strip(), file=sys.stderr)
        return 2
    try:
        commands[argv[0]]()
    except CheckFailed as error:
        print(f"error: {error}", file=sys.stderr)
        return 1
    except subprocess.CalledProcessError as error:
        print(f"error: {error.cmd[0:4]} exited with {error.returncode}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

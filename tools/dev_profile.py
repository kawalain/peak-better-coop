"""Build and launch a BepInEx development profile for PEAK in a plain folder.

The profile lives outside %APPDATA% on purpose: tools started from a packaged
(MSIX) host such as the Claude desktop app see a virtualised %APPDATA%, while
the game started by Steam sees the real one.

Usage:
  python -I tools/dev_profile.py install <profile_dir> <cache_dir> <Owner-Package>...
  python -I tools/dev_profile.py launch <profile_dir> <game_dir>

install resolves dependencies to their latest versions, downloads each
package from Thunderstore into <cache_dir>, and lays it out like Gale does:
BepInExPack at the profile root, other packages under BepInEx/<subdir>/<name>/.
launch copies the doorstop proxy into the game folder and starts the game
through Steam with the profile's preloader.
"""

import json
import shutil
import subprocess
import sys
import urllib.request
import winreg
import zipfile
from pathlib import Path, PurePosixPath

COMMUNITY_INDEX = "https://thunderstore.io/c/peak/api/v1/package/"
LOADER = "BepInEx-BepInExPack_PEAK"
PEAK_APP_ID = "3527290"
SUBDIRS = {"plugins", "patchers", "core", "monomod", "config"}
USER_AGENT = "peak-better-coop-dev-profile"


def fetch(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request) as response:
        return response.read()


def resolve(index: dict, wanted: list[str]) -> list[dict]:
    resolved: dict[str, dict] = {}
    queue = [LOADER, *wanted]
    while queue:
        name = queue.pop(0)
        if name in resolved:
            continue
        latest = index[name]["versions"][0]
        resolved[name] = latest
        queue.extend(dep.rsplit("-", 1)[0] for dep in latest["dependencies"])
    return [dict(version, full_name=name) for name, version in resolved.items()]


def safe_parts(member: str) -> tuple[str, ...]:
    parts = PurePosixPath(member.replace("\\", "/")).parts
    if any(part in ("..", "") for part in parts) or (parts and ":" in parts[0]):
        raise ValueError(f"unsafe path in archive: {member}")
    return parts


def install_package(archive: Path, name: str, profile: Path) -> None:
    with zipfile.ZipFile(archive) as zf:
        for member in zf.infolist():
            if member.is_dir():
                continue
            parts = safe_parts(member.filename)
            if name.startswith("BepInEx-BepInExPack"):
                if len(parts) < 2:
                    continue  # top-level icon, manifest, README
                target = profile.joinpath(*parts[1:])
            elif parts[0] in SUBDIRS and len(parts) > 1:
                target = profile / "BepInEx" / parts[0] / name / Path(*parts[1:])
            else:
                target = profile / "BepInEx" / "plugins" / name / Path(*parts)
            target.parent.mkdir(parents=True, exist_ok=True)
            with zf.open(member) as source, open(target, "wb") as destination:
                shutil.copyfileobj(source, destination)


def install(profile: Path, cache: Path, wanted: list[str]) -> None:
    index = {p["full_name"]: p for p in json.loads(fetch(COMMUNITY_INDEX))}
    for package in resolve(index, wanted):
        name, version = package["full_name"], package["version_number"]
        archive = cache / name / f"{name}-{version}.zip"
        if not archive.exists():
            archive.parent.mkdir(parents=True, exist_ok=True)
            archive.write_bytes(fetch(package["download_url"]))
        install_package(archive, name, profile)
        print(f"installed {name} {version}")


def steam_exe() -> Path:
    with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, r"SOFTWARE\WOW6432Node\Valve\Steam") as key:
        return Path(winreg.QueryValueEx(key, "InstallPath")[0]) / "steam.exe"


def launch(profile: Path, game: Path) -> None:
    for name in ("winhttp.dll", "doorstop_config.ini", ".doorstop_version"):
        shutil.copy2(profile / name, game / name)
    preloader = profile / "BepInEx" / "core" / "BepInEx.Preloader.dll"
    major = (profile / ".doorstop_version").read_text().strip().split(".")[0]
    enable, target = (
        ("--doorstop-enabled", "--doorstop-target-assembly")
        if major == "4"
        else ("--doorstop-enable", "--doorstop-target")
    )
    command = [str(steam_exe()), "-applaunch", PEAK_APP_ID, enable, "true", target, str(preloader)]
    print(" ".join(command))
    subprocess.Popen(command)


def main(argv: list[str]) -> None:
    if len(argv) >= 4 and argv[1] == "install":
        install(Path(argv[2]), Path(argv[3]), argv[4:])
    elif len(argv) == 4 and argv[1] == "launch":
        launch(Path(argv[2]), Path(argv[3]))
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main(sys.argv)

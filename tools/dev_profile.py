"""Build and launch a BepInEx development profile for PEAK in a plain folder.

The profile lives outside %APPDATA% on purpose: tools started from a packaged
(MSIX) host such as the Claude desktop app see a virtualised %APPDATA%, while
the game started by Steam sees the real one.

Usage:
  python -I tools/dev_profile.py install <profile_dir> <cache_dir> <Owner-Package>...
  python -I tools/dev_profile.py launch <profile_dir> <game_dir> [--control]
  python -I tools/dev_profile.py twins <profile_dir> <game_dir> [--plugin <BetterCoop.dll>] [--only host|guest] [--control]

install resolves dependencies to their latest versions, downloads each
package from Thunderstore into <cache_dir>, and lays it out like Gale does:
BepInExPack at the profile root, other packages under BepInEx/<subdir>/<name>/.
launch copies the doorstop proxy into the game folder and starts the game
through Steam with the profile's preloader.

twins starts two Twins, copies of the game for testing apart from the player's
own (development builds only; see GLOSSARY.md): the host Twin opens a room and
the guest Twin joins it, by the room card ~/BetterCoopTwins/room.txt. Each runs
from its own copy of <profile_dir> under ~/BetterCoopTwins (host, guest), with
its own config (Verbose Log on) and logs, started from PEAK.exe directly in a
small window.
Steam must be running. --plugin puts that BetterCoop.dll, such as one just
built, in both copies; --only starts one of them again, as after a test closed it.

--control turns on the Control Line in the copies it starts (development builds
only), for tools/peak_mcp.py to watch and steer them.
"""

import json
import os
import shutil
import subprocess
import sys
import urllib.request
import winreg
import zipfile
from pathlib import Path, PurePosixPath

TWINS = Path.home() / "BetterCoopTwins"
COMMUNITY_INDEX = "https://thunderstore.io/c/peak/api/v1/package/"
LOADER = "BepInEx-BepInExPack_PEAK"
PEAK_APP_ID = "3527290"
CONTROL = "--bettercoop-control"
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


def launch(profile: Path, game: Path, control: bool) -> None:
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
    if control:
        command.append(CONTROL)
    print(" ".join(command))
    subprocess.Popen(command)


def twin(profile: Path, game: Path, role: str, plugin: Path | None, control: bool) -> None:
    twin_dir = TWINS / role
    card = TWINS / "room.txt"

    def leave_out(folder: str, names: list[str]) -> set[str]:
        here = Path(folder)
        skipped = {n for n in names if n.startswith("LogOutput") or n == "in.kawala.bettercoop.twin-id.txt"}
        if here == profile / "BepInEx":
            skipped.add("cache")
        return skipped

    shutil.copytree(profile, twin_dir, ignore=leave_out, dirs_exist_ok=True)
    # A Twin is for testing: its log says everything the mod does.
    config = twin_dir / "BepInEx" / "config" / "in.kawala.bettercoop.cfg"
    if config.exists():
        text = config.read_text(encoding="utf-8")
        config.write_text(text.replace("Verbose Log = false", "Verbose Log = true"), encoding="utf-8")
    if plugin is not None:
        for installed in (twin_dir / "BepInEx" / "plugins").rglob("BetterCoop.dll"):
            shutil.copy2(plugin, installed)
    preloader = twin_dir / "BepInEx" / "core" / "BepInEx.Preloader.dll"
    major = (twin_dir / ".doorstop_version").read_text().strip().split(".")[0]
    enable, target = (
        ("--doorstop-enabled", "--doorstop-target-assembly")
        if major == "4"
        else ("--doorstop-enable", "--doorstop-target")
    )
    command = [
        str(game / "PEAK.exe"),
        enable, "true", target, str(preloader),
        "-screen-fullscreen", "0", "-screen-width", "960", "-screen-height", "540",
        "-logFile", str(twin_dir / "Player.log"),
        f"--bettercoop-twin={card}",
        f"--bettercoop-twin-name=Twin{'A' if role == 'host' else 'B'}",
    ]
    if role == "host":
        card.unlink(missing_ok=True)
        command.append("--bettercoop-twin-host")
    if control:
        command.append(CONTROL)
    # Started directly rather than through Steam, which would only bring the
    # player's own copy forward; the app id lets Steamworks start without relaunching.
    env = dict(os.environ, SteamAppId=PEAK_APP_ID, SteamGameId=PEAK_APP_ID)
    print(f"{role}: " + " ".join(command))
    subprocess.Popen(command, cwd=game, env=env)


def main(argv: list[str]) -> None:
    control = "--control" in argv
    argv = [a for a in argv if a != "--control"]
    if len(argv) >= 4 and argv[1] == "install":
        install(Path(argv[2]), Path(argv[3]), argv[4:])
    elif len(argv) == 4 and argv[1] == "launch":
        launch(Path(argv[2]), Path(argv[3]), control)
    elif len(argv) >= 4 and argv[1] == "twins":
        rest = argv[4:]
        plugin = Path(rest[rest.index("--plugin") + 1]) if "--plugin" in rest else None
        roles = [rest[rest.index("--only") + 1]] if "--only" in rest else ["host", "guest"]
        if not set(roles) <= {"host", "guest"}:
            sys.exit(__doc__)
        TWINS.mkdir(parents=True, exist_ok=True)
        for role in roles:
            twin(Path(argv[2]), Path(argv[3]), role, plugin, control)
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main(sys.argv)

"""Build and launch a BepInEx development profile for PEAK in a plain folder.

The profile lives outside %APPDATA% on purpose: tools started from a packaged
(MSIX) host such as the Claude desktop app see a virtualised %APPDATA%, while
the game started by Steam sees the real one.

Usage:
  python -I tools/dev_profile.py install <profile_dir> <cache_dir> <Owner-Package>...
  python -I tools/dev_profile.py launch <profile_dir> <game_dir> [--control]
  python -I tools/dev_profile.py twins <profile_dir> <game_dir> [--plugin <BetterCoop.dll>] [--guests 1-3] [--unmodded <role>] [--only <role>] [--control] [--visible]

install resolves dependencies to their latest versions, downloads each
package from Thunderstore into <cache_dir>, and lays it out like Gale does:
BepInExPack at the profile root, other packages under BepInEx/<subdir>/<name>/.
launch copies the doorstop proxy into the game folder and starts the game
through Steam with the profile's preloader.

twins starts Twins, copies of the game for testing apart from the player's own
(development builds only; see GLOSSARY.md): the host Twin opens a room and the
guest Twins join it, by the room card ~/BetterCoopTwins/room.txt. Each runs from
its own copy of <profile_dir> under ~/BetterCoopTwins (host, guest, guest2,
guest3), with its own config (Verbose Log on) and logs, started from PEAK.exe
directly, hidden and silent (--visible shows a small window and plays sound;
either way the game renders, so screenshots work). Roles are named the same on
the Control Line.

Twins start from the Twin game folder, <drive>/BetterCoopTwinGame on the game's
own drive: hard links to the game's files, kept in step with them, except that
its globalgamemanagers names the product TWIN instead of PEAK. Unity keeps the
window size, audio, and every other setting under the product name, so Twins
never write the player's settings or saves, and the game's own folder is left
as it is.
Steam must be running. --plugin puts that BetterCoop.dll, such as one just
built, in every copy; --guests starts that many guests (1 by default, up to 3);
--unmodded makes that guest stand in for an Unmodded Player, with the mod
loaded but not patched and publishing nothing; --only starts one role again, as
after a test closed it (give --unmodded again for an Unmodded one).

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
HEADLESS = "--bettercoop-twin-headless"
TWIN_GAME = "BetterCoopTwinGame"
# Unity keeps settings under the product name; the Twin game folder's is this,
# as long as PEAK so the file it is stored in keeps its layout.
PRODUCT, TWIN_PRODUCT = b"PEAK", b"TWIN"
# Laid in the Twin game folder from the profile, never linked from the game's.
DOORSTOP = ("winhttp.dll", "doorstop_config.ini", ".doorstop_version")
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


GUESTS = ["guest", "guest2", "guest3"]
LABELS = {"host": "TwinA", "guest": "TwinB", "guest2": "TwinC", "guest3": "TwinD"}


def twin_game(game: Path, profile: Path) -> Path:
    """Brings the Twin game folder in step with the game's and returns it."""
    root = Path(game.anchor) / TWIN_GAME
    managers = Path("PEAK_Data") / "globalgamemanagers"
    wanted: set[Path] = set()
    for folder, _, names in os.walk(game):
        for name in names:
            source = Path(folder) / name
            relative = source.relative_to(game)
            if relative.parts[0] in DOORSTOP or relative == managers:
                continue
            wanted.add(relative)
            target = root / relative
            if target.exists() and os.path.samefile(source, target):
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            target.unlink(missing_ok=True)
            try:
                os.link(source, target)
            except OSError as error:
                sys.exit(f"cannot hard-link {source} into {root}: {error}")
    for folder, _, names in os.walk(root):
        for name in names:
            relative = (Path(folder) / name).relative_to(root)
            if relative.parts[0] not in DOORSTOP and relative != managers and relative not in wanted:
                (root / relative).unlink()
    data = (game / managers).read_bytes()
    marked = len(PRODUCT).to_bytes(4, "little") + PRODUCT
    if data.count(marked) != 1:
        sys.exit(f"cannot find the product name {PRODUCT.decode()} once in {game / managers}")
    patched = data.replace(marked, len(TWIN_PRODUCT).to_bytes(4, "little") + TWIN_PRODUCT)
    if not (root / managers).exists() or (root / managers).read_bytes() != patched:
        (root / managers).write_bytes(patched)
    for name in DOORSTOP:
        # A running Twin holds the proxy open, so it is replaced only when it changed.
        if not (root / name).exists() or (root / name).read_bytes() != (profile / name).read_bytes():
            (root / name).unlink(missing_ok=True)
            shutil.copy2(profile / name, root / name)
    return root


def twin(profile: Path, game: Path, role: str, plugin: Path | None, control: bool, unmodded: bool, visible: bool = False) -> None:
    twin_dir = TWINS / role
    card = TWINS / "room.txt"

    def leave_out(folder: str, names: list[str]) -> set[str]:
        here = Path(folder)
        skipped = {n for n in names if n.startswith("LogOutput") or n == "in.kawala.bettercoop.twin-id.txt"}
        if here == profile / "BepInEx":
            skipped.add("cache")
        return skipped

    # The mods are laid out afresh, so one taken out of the profile is not left in the Twin.
    for mods in ("plugins", "patchers"):
        shutil.rmtree(twin_dir / "BepInEx" / mods, ignore_errors=True)
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
        f"--bettercoop-twin-name={LABELS[role]}",
        f"--bettercoop-twin-role={role}",
    ]
    if unmodded:
        command.append("--bettercoop-twin-unmodded")
    if role == "host":
        card.unlink(missing_ok=True)
        command.append("--bettercoop-twin-host")
    if control:
        command.append(CONTROL)
    if not visible:
        command.append(HEADLESS)
    # Started directly rather than through Steam, which would only bring the
    # player's own copy forward; the app id lets Steamworks start without relaunching.
    env = dict(os.environ, SteamAppId=PEAK_APP_ID, SteamGameId=PEAK_APP_ID)
    print(f"{role}: " + " ".join(command))
    startup = None
    if not visible:
        # The game's window would show on screen until the mod moves it off;
        # it starts hidden, and the mod shows it again off every screen.
        startup = subprocess.STARTUPINFO()
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startup.wShowWindow = 0  # SW_HIDE
    subprocess.Popen(command, cwd=game, env=env, startupinfo=startup)


def main(argv: list[str]) -> None:
    control = "--control" in argv
    visible = "--visible" in argv
    argv = [a for a in argv if a not in ("--control", "--visible")]
    if len(argv) >= 4 and argv[1] == "install":
        install(Path(argv[2]), Path(argv[3]), argv[4:])
    elif len(argv) == 4 and argv[1] == "launch":
        launch(Path(argv[2]), Path(argv[3]), control)
    elif len(argv) >= 4 and argv[1] == "twins":
        rest = argv[4:]
        plugin = Path(rest[rest.index("--plugin") + 1]) if "--plugin" in rest else None
        guests = int(rest[rest.index("--guests") + 1]) if "--guests" in rest else 1
        unmodded = rest[rest.index("--unmodded") + 1] if "--unmodded" in rest else None
        roles = [rest[rest.index("--only") + 1]] if "--only" in rest else ["host", *GUESTS[:guests]]
        if not 1 <= guests <= len(GUESTS) or not set(roles) <= set(LABELS) or unmodded not in (None, *GUESTS):
            sys.exit(__doc__)
        TWINS.mkdir(parents=True, exist_ok=True)
        game = twin_game(Path(argv[3]), Path(argv[2]))
        for role in roles:
            twin(Path(argv[2]), game, role, plugin, control, role == unmodded, visible)
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main(sys.argv)

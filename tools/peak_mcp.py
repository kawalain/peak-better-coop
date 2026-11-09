# /// script
# requires-python = ">=3.11"
# dependencies = ["mcp>=2.3,<3"]
# ///
"""MCP server for the Control Line: watch and steer running copies of PEAK.

Development builds of BetterCoop started with the Control Line on (see
CLAUDE.md) each listen on 127.0.0.1 and announce themselves in
~/.bettercoop/control/<pid>.json. This server reads those files, so it finds
the first copy (the host) and the Twin without any configuration, and passes
each tool call to the copy it names as one line of JSON.

Run it with `uv run tools/peak_mcp.py`; `.mcp.json` registers it for Claude Code.
"""

import json
import os
import socket
import sys
import time
from pathlib import Path
from typing import Any

from mcp.server.mcpserver import Image, MCPServer

REGISTRY = Path.home() / ".bettercoop" / "control"
TIMEOUT = 30.0

mcp = MCPServer("peak")


def alive(pid: int) -> bool:
    """Whether a process with this id is running, without touching it."""
    if sys.platform == "win32":
        import ctypes

        kernel32 = ctypes.windll.kernel32
        handle = kernel32.OpenProcess(0x1000, False, pid)  # PROCESS_QUERY_LIMITED_INFORMATION
        if not handle:
            return False
        code = ctypes.c_ulong()
        ok = kernel32.GetExitCodeProcess(handle, ctypes.byref(code))
        kernel32.CloseHandle(handle)
        return bool(ok) and code.value == 259  # STILL_ACTIVE
    try:
        os.kill(pid, 0)
    except ProcessLookupError:
        return False
    except PermissionError:
        return True
    return True


def instances() -> list[dict[str, Any]]:
    found = []
    for file in sorted(REGISTRY.glob("*.json")):
        try:
            card = json.loads(file.read_text(encoding="utf-8-sig"))
        except (OSError, ValueError):
            continue
        if not alive(int(card["pid"])):
            # A copy that was closed by force leaves its file behind; trying its
            # port would cost a second each on Windows.
            file.unlink(missing_ok=True)
            continue
        try:
            with socket.create_connection(("127.0.0.1", int(card["port"])), timeout=1.0):
                pass
        except OSError:
            continue  # a copy that quit or crashed without removing its file
        found.append(card)
    return found


def pick(target: str) -> dict[str, Any]:
    running = instances()
    if not running:
        raise RuntimeError(
            "No copy of the game has the Control Line on. Start a development build with "
            "--bettercoop-control, BETTERCOOP_CONTROL=1, or [Debug] Control Line."
        )
    chosen = [c for c in running if target in (c["role"], str(c["pid"]))]
    if not chosen:
        roles = ", ".join(f"{c['role']} (pid {c['pid']})" for c in running)
        raise RuntimeError(f"No copy matches {target!r}; running: {roles}.")
    return chosen[0]


def call(target: str, command: str, **args: Any) -> Any:
    card = pick(target)
    request = {"token": card["token"], "command": command, "args": args}
    with socket.create_connection(("127.0.0.1", int(card["port"])), timeout=TIMEOUT) as connection:
        connection.sendall((json.dumps(request) + "\n").encode("utf-8"))
        data = b""
        while not data.endswith(b"\n"):
            chunk = connection.recv(65536)
            if not chunk:
                break
            data += chunk
    answer = json.loads(data.decode("utf-8"))
    if not answer.get("ok"):
        raise RuntimeError(answer.get("error", "the game refused"))
    return answer.get("result")


@mcp.tool()
def peak_instances() -> list[dict[str, Any]]:
    """List the running copies of the game with the Control Line on: role (host, guest, or player for a copy that is not a Twin), pid, profile."""
    return [{k: c[k] for k in ("role", "pid", "profile")} for c in instances()]


@mcp.tool()
def peak_commands(target: str = "host") -> Any:
    """List every command the copy understands, with what each takes."""
    return call(target, "commands")


@mcp.tool()
def peak_state(target: str = "host") -> Any:
    """The copy's scene, room and players, its own scout, and the mod's Pairs, Run Rules and Run Report."""
    return call(target, "state")


@mcp.tool()
def peak_log(target: str = "host", lines: int = 80, contains: str = "") -> str:
    """The last lines of the copy's BepInEx log, optionally only those containing some text."""
    return call(target, "log", lines=lines, contains=contains)


@mcp.tool()
def peak_screenshot(target: str = "host", scale: int = 1) -> Image:
    """A picture of the copy's screen as it is now, rendered at 1 to 4 times the window's size."""
    path = Path(call(target, "screenshot", scale=scale))
    for _ in range(50):
        if path.exists() and path.stat().st_size > 0:
            break
        time.sleep(0.1)
    return Image(data=path.read_bytes(), format="png")


@mcp.tool()
def peak_controls(target: str = "host", contains: str = "") -> Any:
    """The buttons, stamps and other controls that can be pressed on the copy's screen now, with ids for peak_press."""
    return call(target, "controls", contains=contains)


@mcp.tool()
def peak_press(id: int, target: str = "host", right: bool = False) -> Any:
    """Press a control listed by peak_controls (right=True for a right click, which goes back on paper)."""
    return call(target, "press", id=id, right=right)


@mcp.tool()
def peak_run(command: str, target: str = "host", args: dict[str, Any] | None = None) -> Any:
    """Run any Control Line command by name with its arguments (see peak_commands): orders, warps, drills, game console commands."""
    return call(target, command, **(args or {}))


if __name__ == "__main__":
    mcp.run()

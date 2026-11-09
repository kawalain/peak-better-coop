"""Run the in-game checks of docs/testing.md in four Twins, as fast as they go.

Usage:
  python -I tools/twin_checks.py <profile_dir> <game_dir> [--plugin <BetterCoop.dll>]
      [--only <name,...>] [--keep] [--visible]

It starts four Twins (host, guest, guest2, guest3) with the Control Line,
headless unless --visible, and runs every check against the same four games:
players leave and join the room by the Control Line instead of closing the game,
and the one check that needs an Unmodded Player starts a guest again as one,
keeping its log so far as ~/BetterCoopTwins/<role>-closed.log. Each check prints
a line as it starts and as it ends, with the time taken; the lines also go to
~/BetterCoopTwins/checks.log, and pictures worth a look are listed at the end.
After a failed check the room is mended so the rest still run. --only runs the
named checks (with the setup they need); --keep leaves the Twins running
afterwards. Exits 1 when a check failed.

The Twins show the game in Korean, so the notices the checks read are the
Korean ones in src/BetterCoop/Ui/Strings.cs.
"""

import json
import re
import shutil
import socket
import subprocess
import sys
import threading
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import dev_profile  # noqa: E402

TWINS = dev_profile.TWINS
REGISTRY = Path.home() / ".bettercoop" / "control"
ROLES = ["host", "guest", "guest2", "guest3"]
PACE = 1.05  # the Roster takes one order a second from each player
START = time.monotonic()
PROGRESS = TWINS / "checks.log"


class Failed(Exception):
    pass


def say(text: str) -> None:
    line = f"[{time.monotonic() - START:7.1f}s] {text}"
    print(line, flush=True)
    with PROGRESS.open("a", encoding="utf-8") as file:
        file.write(line + "\n")


# --- The Control Line -------------------------------------------------------

def alive(pid: int) -> bool:
    import ctypes

    handle = ctypes.windll.kernel32.OpenProcess(0x1000, False, pid)
    if not handle:
        return False
    code = ctypes.c_ulong()
    ctypes.windll.kernel32.GetExitCodeProcess(handle, ctypes.byref(code))
    ctypes.windll.kernel32.CloseHandle(handle)
    return code.value == 259  # STILL_ACTIVE


def cards() -> dict[str, dict]:
    found = {}
    for file in REGISTRY.glob("*.json"):
        try:
            card = json.loads(file.read_text(encoding="utf-8-sig"))
        except (OSError, ValueError):
            continue
        if card.get("role") in ROLES and alive(int(card["pid"])):
            found[card["role"]] = card
    return found


def call(role: str, command: str, timeout: float = 30, **args):
    card = cards().get(role)
    if card is None:
        raise Failed(f"{role} is not running")
    with socket.create_connection(("127.0.0.1", int(card["port"])), timeout=timeout) as connection:
        connection.sendall((json.dumps({"token": card["token"], "command": command, "args": args}) + "\n").encode("utf-8"))
        data = b""
        while not data.endswith(b"\n"):
            chunk = connection.recv(1 << 20)
            if not chunk:
                break
            data += chunk
    answer = json.loads(data.decode("utf-8"))
    if not answer.get("ok"):
        raise Failed(f"{role} {command}: {answer.get('error')}")
    return answer.get("result")


def state(role: str) -> dict:
    return call(role, "state")


def quietly(probe):
    try:
        return probe()
    except (Failed, OSError, ValueError, KeyError, TypeError, IndexError):
        return None


def wait(probe, what: str, timeout: float = 60, every: float = 0.25):
    end = time.monotonic() + timeout
    while time.monotonic() < end:
        got = quietly(probe)
        if got:
            return got
        time.sleep(every)
    raise Failed(f"timed out after {timeout:.0f} s waiting for {what}")


def check(condition: bool, why: str) -> None:
    if not condition:
        raise Failed(why)


# --- Logs ---------------------------------------------------------------------

def log_file(role: str) -> Path:
    return TWINS / role / "BepInEx" / "LogOutput.log"


def marks() -> dict[str, int]:
    return {role: (log_file(role).stat().st_size if log_file(role).exists() else 0) for role in ROLES}


def since(role: str, mark: dict[str, int]) -> str:
    file = log_file(role)
    if not file.exists():
        return ""
    with file.open("rb") as handle:
        start = mark.get(role, 0)
        if handle.seek(0, 2) < start:
            start = 0  # the Twin started again with a new log
        handle.seek(start)
        return handle.read().decode("utf-8", errors="replace")


def notices(role: str, mark: dict[str, int], containing: str = "") -> list[str]:
    return [line for line in since(role, mark).splitlines() if "[Notice]" in line and containing in line]


# --- The Twins ----------------------------------------------------------------

class Twins:
    def __init__(self, profile: Path, game: Path, plugin: Path | None, visible: bool):
        self.profile, self.plugin, self.visible = profile, plugin, visible
        self.game = dev_profile.twin_game(game, profile)
        self.unmodded: set[str] = set()

    def launch(self, role: str, unmodded: bool = False) -> None:
        if unmodded:
            self.unmodded.add(role)
        else:
            self.unmodded.discard(role)
        # A game just closed lets go of its files a moment later.
        for attempt in range(20):
            try:
                dev_profile.twin(self.profile, self.game, role, self.plugin, True, unmodded, self.visible)
                return
            except OSError:
                if attempt == 19:
                    raise
                time.sleep(0.5)

    def close(self, role: str) -> None:
        card = cards().get(role)
        if card is None:
            return
        pid = int(card["pid"])
        subprocess.run(["taskkill", "/PID", str(pid), "/F"], capture_output=True)
        wait(lambda: not alive(pid), f"{role} to close", 30)
        (REGISTRY / f"{pid}.json").unlink(missing_ok=True)
        # Starting the Twin again copies its profile afresh; keep the log of the checks so far.
        log = TWINS / role / "BepInEx" / "LogOutput.log"
        if log.exists():
            shutil.copyfile(log, TWINS / f"{role}-closed.log")

    def close_all(self) -> None:
        for role in list(cards()):
            self.close(role)


def present() -> list[str]:
    return [role for role in ROLES if role in cards()]


def in_room(role: str) -> bool:
    s = state(role)
    return s["room"] is not None and s["me"] is not None


def actor_of(role: str) -> int:
    return state(role)["me"]["actor"]


def leader() -> str:
    roles = [r for r in present() if quietly(lambda r=r: in_room(r))]
    for role in roles:
        s = state(role)
        if s["room"]["masterActor"] == s["me"]["actor"]:
            return role
    raise Failed("no Twin leads the room")


def actors() -> dict[str, int]:
    return {role: actor_of(role) for role in present() if quietly(lambda r=role: in_room(r))}


def role_of(actor: int) -> str:
    for role, number in actors().items():
        if number == actor:
            return role
    raise Failed(f"no Twin is actor {actor}")


def pairs(role: str) -> list[tuple[int, int]]:
    return sorted(tuple(p) for p in state(role)["mod"]["pairs"])


def scout(role: str, actor: int) -> dict:
    return next(s for s in state(role)["scouts"] if s["actor"] == actor)


def same_pairs_everywhere() -> list[tuple[int, int]]:
    seen = {role: pairs(role) for role in actors()}
    values = list(seen.values())
    return values[0] if all(v == values[0] for v in values) else None


def operation(role: str) -> dict | None:
    return state(role)["mod"]["pairChange"]


def wait_operation(role: str, kind: str, after: float, timeout: float = 30) -> dict:
    return wait(lambda: (op := operation(role)) and op["kind"] == kind and op["at"] > after and op["done"] and op,
                f"{kind} to run", timeout)


last_order: dict[str, float] = {}


def order(role: str, what: str, **args):
    gap = PACE - (time.monotonic() - last_order.get(role, -10))
    if gap > 0:
        time.sleep(gap)
    last_order[role] = time.monotonic()
    return call(role, "order", what=what, **args)


def together(*calls):
    """Runs the calls at once, each (role, command, args), and answers in order."""
    results: list = [None] * len(calls)

    def run(i, role, command, args):
        try:
            results[i] = call(role, command, **args)
        except Failed as error:
            results[i] = error

    threads = [threading.Thread(target=run, args=(i, *c)) for i, c in enumerate(calls)]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join()
    return results


def last_op_at(role: str) -> float:
    op = operation(role)
    return op["at"] if op else 0.0


def end_screen_next(role: str) -> bool:
    """Presses Next on the end screen; a profile with ResultsSkip goes to the Airport without one."""
    if state(role)["scene"] == "Airport":
        return True
    for control in call(role, "controls"):
        if "Canvas_End" in control["path"]:
            call(role, "press", id=control["id"])
            return True
    return False


def roster(role: str, scroll_to: str) -> str:
    call(role, "menu", action="pause")
    button = wait(lambda: [c for c in call(role, "controls") if "BetterCoop.MenuButton" in c["path"]], "the Roster button", 10)
    call(role, "press", id=button[0]["id"])
    time.sleep(1.5)
    scrolled = call(role, "scroll", to=scroll_to)
    time.sleep(0.5)
    picture = call(role, "screenshot", scale=1)
    call(role, "menu", action="back")
    call(role, "menu", action="back")
    return f"{scrolled}; {picture}"


def name_of(actor: int) -> str:
    for role in actors():
        for player in state(role)["room"]["players"]:
            if player["actor"] == actor:
                return player["name"]
    raise Failed(f"no player {actor}")


# --- The checks ---------------------------------------------------------------

class Run:
    def __init__(self, twins: Twins):
        self.twins = twins
        self.pictures: list[str] = []
        self.expiry: tuple[str, float, dict] | None = None

    def mend(self) -> None:
        """After a failed check: every Twin back in the room, so the next checks start whole."""
        for role in present():
            if not quietly(lambda r=role: in_room(r)):
                quietly(lambda r=role: call(r, "room", action="join"))
        wait(lambda: all(in_room(r) for r in present()), "every Twin to be back in the room", 180, 1)
        time.sleep(2)

    def setup(self) -> str:
        TWINS.mkdir(parents=True, exist_ok=True)
        self.twins.close_all()
        for role in ROLES:
            self.twins.launch(role)
        wait(lambda: len(present()) == 4, "four Twins to start", 120)
        wait(lambda: all(state(r)["scene"] == "Airport" for r in ROLES) and len(state("host")["scouts"]) == 4,
             "four scouts in the Airport", 300, 1)
        for role in ROLES:
            call(role, "language", name="Korean")
        return ", ".join(f"{r} #{a}" for r, a in actors().items())

    def roster_airport(self) -> str:
        found = roster("guest", "1.")
        check(re.search(r"scrolled to \d+\.\s", found) is not None, f"no numbered heading: {found}")
        self.pictures.append("Roster at the Airport: " + found.split("; ")[-1])
        return "headings are numbered"

    def plan_and_formation(self) -> str:
        a = actors()
        plan = [[a["host"], a["guest"]], [a["guest2"], a["guest3"]]]
        mashed = together(("host", "order", {"what": "plan", "pairs": plan}), ("host", "order", {"what": "plan", "pairs": plan}))
        last_order["host"] = time.monotonic()
        check(sorted(map(str, mashed)) == ["Done", "TooSoon"], f"mashed plan answered {mashed}")
        call("host", "depart")
        wait(lambda: all(state(r)["mod"]["formed"] for r in ROLES), "the Pairs to form", 240, 1)
        got = wait(same_pairs_everywhere, "every copy to show the same Pairs", 20)
        check(got == sorted(tuple(p) for p in plan), f"formed {got}, planned {plan}")
        for carrier, rider in got:
            wait(lambda c=carrier, r=rider: scout("host", c)["carrying"] == r and scout("host", r)["carried"],
                 f"{carrier} to carry {rider}", 20)
        return f"mashed plan answered TooSoon; formed {got} as planned"

    def four_scouts_shuffle(self) -> str:
        before = pairs("host")
        here = scout("host", actors()["host"])["position"]
        call("host", "warp", x=here[0] + 60, y=here[1] + 5, z=here[2])
        time.sleep(2)
        bodies = [scout("host", carrier)["position"] for carrier, _ in before]
        after_at = last_op_at("host")
        mashed = together(("host", "order", {"what": "shuffle"}), ("host", "order", {"what": "shuffle"}))
        last_order["host"] = time.monotonic()
        check(sorted(map(str, mashed)) == ["Done", "TooSoon"], f"mashed shuffle answered {mashed}")
        wait_operation("host", "OrderedShuffle", after_at)
        got = wait(same_pairs_everywhere, "every copy to show the same Pairs", 20)
        partner = {**{c: r for c, r in before}, **{r: c for c, r in before}}
        for carrier, rider in got:
            check(partner[carrier] != rider, f"{carrier} and {rider} were partners already")
        time.sleep(2)
        for carrier, _ in got:
            spot = scout("host", carrier)["position"]
            check(min(sum((a - b) ** 2 for a, b in zip(spot, body)) ** 0.5 for body in bodies) < 4,
                  f"new Carrier {carrier} stands at {spot}, not where a former Shared Body stood ({bodies})")
        return f"{before} became {got}; each new Carrier stands where a former Shared Body stood"

    def call_off(self) -> str:
        before = pairs("host")
        mark = marks()
        after_at = last_op_at("host")
        check(order("host", "shuffle") == "Done", "shuffle refused")
        outcomes = [call("host", "order", what="callOff"), call("host", "order", what="callOff")]
        last_order["host"] = time.monotonic()
        check(outcomes == ["Done", "TooSoon"], f"call off answered {outcomes}")
        wait(lambda: (op := operation("host")) and op["at"] > after_at and op["calledOff"] and op["done"], "the call-off", 20)
        for role in ROLES:
            wait(lambda r=role: notices(r, mark, "짝 섞기를 취소"), f"{role}'s call-off notice", 15)
        time.sleep(1)
        check(same_pairs_everywhere() == before, "the Pairs changed")
        for role in ROLES:
            count = len(notices(role, mark, "짝 섞기를 취소"))
            check(count == 1, f"{role} showed {count} call-off notices")
        return "every copy told once; the Pairs stayed"

    def recovery(self) -> str:
        mark = marks()
        outcomes = [order("host", "recover"), call("host", "order", what="recover")]
        last_order["host"] = time.monotonic()
        check(outcomes == ["Done", "TooSoon"], f"recover answered {outcomes}")
        for role in ROLES:
            wait(lambda r=role: notices(r, mark, "다시 업히"), f"{role}'s recovery notice", 15)
        time.sleep(1)
        for role in ROLES:
            count = len(notices(role, mark, "다시 업히"))
            check(count == 1, f"{role} showed {count} recovery notices")
        return "every copy told once"

    def assignment(self) -> str:
        (c1, r1), (c2, r2) = pairs("host")
        wanted = sorted([(r1, c2), (r2, c1)])
        after_at = last_op_at("host")
        check(order("host", "assign", pairs=[list(p) for p in wanted]) == "Done", "assignment refused")
        wait_operation("host", "PairAssignment", after_at)
        got = wait(lambda: (p := same_pairs_everywhere()) == wanted and p, f"the Pairs {wanted}", 20)
        return f"assigned {got}"

    def guest_pair(self) -> tuple[int, int, str, str]:
        roles = actors()
        for carrier, rider in pairs("host"):
            if role_of(rider) != "host":
                return carrier, rider, role_of(carrier), role_of(rider)
        raise Failed("no Pair with a guest Rider")

    def role_swap(self) -> str:
        carrier, rider, carrier_role, rider_role = self.guest_pair()
        mark = marks()
        after_at = last_op_at("host")
        order(rider_role, "swap")
        wait(lambda: notices(carrier_role, mark, "자리를 바꾸고 싶어"), "the Carrier to hear of the ask", 15)
        order(carrier_role, "swap")
        wait_operation("host", "RoleSwap", after_at)
        wait(lambda: (rider, carrier) in (same_pairs_everywhere() or []), "the roles to swap everywhere", 20)
        return f"{carrier}>{rider} became {rider}>{carrier}"

    def swap_withdrawn_and_cancelled(self) -> str:
        _, _, _, rider_role = self.guest_pair()
        mark = marks()
        order(rider_role, "swap")
        order(rider_role, "swap")
        wait(lambda: not state(rider_role)["mod"]["swapPending"], "the ask to be withdrawn", 10)
        wait(lambda: notices(rider_role, mark, "요청을 취소했습니다"), "the withdrawal notice", 15)
        order(rider_role, "swap")
        wait(lambda: state(rider_role)["mod"]["swapPending"], "the ask", 10)
        after_at = last_op_at("host")
        order("host", "shuffle")
        wait_operation("host", "OrderedShuffle", after_at)
        wait(lambda: not state(rider_role)["mod"]["swapPending"], "the shuffle to cancel the ask", 10)
        wait(lambda: notices(rider_role, mark, "짝 섞기가 시작되어"), "the notice that the shuffle cancelled the ask", 15)
        return "withdrawn and cancelled by a shuffle, each told"

    def mashed_swap(self) -> str:
        carrier, rider, carrier_role, rider_role = self.guest_pair()
        time.sleep(3.2)  # past the partner's notice gate for this player's earlier asks
        mark = marks()
        gap = PACE - (time.monotonic() - last_order.get(rider_role, -10))
        if gap > 0:
            time.sleep(gap)
        together((rider_role, "order", {"what": "swap"}), (rider_role, "order", {"what": "swap"}))
        last_order[rider_role] = time.monotonic()
        wait(lambda: notices(rider_role, mark, "방금 한 일"), "the second ask to be told to wait", 10)
        call(rider_role, "drill", what="flood", kind="swap", count=200)
        wait(lambda: notices(carrier_role, mark, "자리를 바꾸고 싶어"), "the partner's notice", 15)
        time.sleep(4)
        shown = len(notices(carrier_role, mark, "자리를 바꾸고 싶어"))
        check(shown == 1, f"the partner was shown {shown} Role Swap notices")
        check(not any("RoleSwap #" in line for line in since("host", mark).splitlines()), "a Role Swap ran")
        wait(lambda: not state(rider_role)["mod"]["swapPending"], "the flood's last withdrawal", 10)
        return "the second ask waited; the partner saw one notice of 201; no Role Swap ran"

    def start_expiry(self) -> str:
        _, _, _, rider_role = self.guest_pair()
        mark = marks()
        order(rider_role, "swap")
        wait(lambda: state(rider_role)["mod"]["swapPending"], "the ask", 10)
        self.expiry = (rider_role, time.monotonic(), mark)
        return f"{rider_role} asked; checked after the next checks"

    def forged_values(self) -> str:
        before = pairs("host")
        forger = next(r for r in ("guest", "guest2", "guest3") if r != leader())
        mark = marks()
        call(forger, "drill", what="forge", key="SwapRequest", actor=actors()["host"])
        wait(lambda: "SwapRequest, which only that player" in since("host", mark), "the forged Role Swap to be dropped", 10)
        check(not state("host")["mod"]["swapPending"], "the forged Role Swap took")
        call(forger, "drill", what="forge", key="Pairs")
        wait(lambda: "the Troop Leader put it back" in since("host", mark), "the Troop Leader to put the Pairs back", 10)
        time.sleep(1)
        check(same_pairs_everywhere() == before, "the forged Pairs took")
        return "both dropped; the Troop Leader put the Pairs back"

    def warp_rider(self) -> str:
        carrier, rider, _, rider_role = self.guest_pair()
        before = scout("host", carrier)["position"]
        here = scout(rider_role, rider)["position"]
        call(rider_role, "warp", x=here[0] + 40, y=here[1] + 10, z=here[2] + 40)
        time.sleep(2.5)
        after = scout("host", carrier)["position"]
        moved = sum((a - b) ** 2 for a, b in zip(before, after)) ** 0.5
        check(moved < 1, f"the Carrier moved {moved:.1f} m")
        return f"the Carrier moved {moved:.2f} m"

    def floods(self) -> str:
        before = pairs("host")
        carrier, rider, carrier_role, rider_role = self.guest_pair()
        other = next(r for r in ("guest", "guest2", "guest3") if r not in (carrier_role, rider_role))
        mark = marks()
        timing: list = []
        timer = threading.Thread(target=lambda: timing.append(call("host", "perf", timeout=60, seconds=6)))
        timer.start()
        time.sleep(0.5)
        for role, kind in ((rider_role, "rider"), (rider_role, "revive"), (carrier_role, "wheel"), (carrier_role, "sound"),
                           (other, "recovery"), (other, "pairs")):
            if role == "host":
                continue
            call(role, "drill", what="flood", kind=kind, count=2000)
        timer.join()
        time.sleep(3)
        check(same_pairs_everywhere() == before, "the Pairs changed")
        for role in ROLES:
            text = since(role, mark)
            check("failed; it keeps running" not in text, f"{role} logged a failure")
            check(not notices(role, mark, "다시 업히"), f"{role} showed a forged recovery")
        cost = timing[0]["modMsPerFrame"] if timing else None
        check(cost is not None and cost < 1, f"the mod took {cost} ms a frame on the Troop Leader")
        return f"no failures, no forged recovery, Pairs kept; the mod took {cost:.2f} ms a frame"

    def expiry_ends(self) -> str:
        check(self.expiry is not None, "no ask was started")
        role, asked, mark = self.expiry
        time.sleep(max(0.0, asked + 31 - time.monotonic()))
        wait(lambda: not state(role)["mod"]["swapPending"], "the ask to lapse", 10)
        wait(lambda: notices(role, mark, "30초 동안 답이 없어"), "the notice that the ask lapsed", 15)
        return "the ask lapsed after 30 s and said so"

    def rules_and_timed(self) -> str:
        try:
            return self._rules_and_timed()
        finally:
            call("host", "order", what="rule", rule="TimedShuffle", on=False)

    def _rules_and_timed(self) -> str:
        mark = marks()
        order("host", "rule", rule="TimedShuffle", on=True)
        call("host", "order", what="shuffleMinutes", minutes=1)
        for role in ("guest", "guest2", "guest3"):
            wait(lambda r=role: notices(r, mark, "원정 규칙"), f"{role}'s rule notice", 15)
        time.sleep(2)
        seconds = state("host")["mod"]["secondsToTimedShuffle"]
        due = time.monotonic() + (seconds or 0)
        check(seconds is not None and seconds <= 60, f"the next Timed Shuffle is {seconds} s away")
        for role in ("guest", "guest2", "guest3"):
            told = notices(role, mark, "원정 규칙")
            check(len(told) == 1 and "시간마다 짝 섞기 켬" in told[0] and "1분" in told[0], f"{role} was told {told}")
        after_at = last_op_at("host")
        wait_operation("host", "TimedShuffle", after_at, seconds + 10)
        late = time.monotonic() - due
        check(abs(late) < 6, f"the Timed Shuffle ran {late:+.1f} s from when it was due")
        return f"one notice naming both changes; the Timed Shuffle ran when due ({seconds:.0f} s)"

    def leave_and_rejoin(self) -> str:
        carrier, rider, carrier_role, rider_role = self.guest_pair()
        name = name_of(rider)
        mark = marks()
        call(rider_role, "drill", what="leave", atOperation=True, delay=0.03, hold=True)
        time.sleep(1)
        after_at = last_op_at("host")
        order("host", "shuffle")
        wait_operation("host", "OrderedShuffle", after_at)
        line = wait(lambda: re.search(r"OrderedShuffle #\d+: ([^;]+);", since("host", mark)), "the shuffle's Pairs", 10)
        shuffled = [tuple(int(x) for x in p.split(">")) for p in line.group(1).split(", ")]
        wait(lambda: not quietly(lambda: in_room(rider_role)), "the Rider to leave", 15)
        kept = sorted(p for p in shuffled if rider not in p)
        partner = next(c if r == rider else r for c, r in shuffled if rider in (c, r))
        got = wait(lambda: (p := same_pairs_everywhere()) == kept and p, f"the Pairs {kept}", 20)
        time.sleep(2)
        alone = scout("host", partner)
        check(not alone["carried"] and alone["carrying"] is None, f"{partner} is still carried or carrying: {alone}")
        wait(lambda: any(name in line and "짝이 풀렸" in line for line in notices("host", mark)), f"a notice naming {name}", 15)
        was_carrier = any(c == rider for c, _ in shuffled)
        joined = marks()
        call(rider_role, "room", action="join")
        back = wait(lambda: in_room(rider_role) and actor_of(rider_role), "the one who left to come back", 180, 1)
        former = (back, partner) if was_carrier else (partner, back)
        wait(lambda: (p := same_pairs_everywhere()) and former in p and p, f"{former} to pair again", 120, 1)
        announced = since("host", joined).count("Announced LatePairing")
        check(announced == 1, f"{announced} Late Pairings")
        check("is an Unmodded Player" not in since("host", joined), "the one who came back was taken for an Unmodded Player")
        return f"the shuffle's Pairs {shuffled} kept without the one who left; {partner} left alone, then {former} again by one Late Pairing"

    def leader_leaves(self) -> str:
        a = actors()
        rest = [a["guest"], a["guest2"], a["guest3"]]
        wanted = sorted([(a["host"], rest[0]), (rest[1], rest[2])])
        after_at = last_op_at("host")
        if pairs("host") != wanted:
            order("host", "assign", pairs=[list(p) for p in wanted])
            wait_operation("host", "PairAssignment", after_at)
            wait(lambda: same_pairs_everywhere() == wanted, "the assignment", 20)
        partner_role = role_of(rest[0])
        mark = marks()
        call("host", "room", action="leave")
        wait(lambda: leader() != "host", "a new Troop Leader", 30)
        new = leader()
        time.sleep(4)
        check(pairs(new) == [(rest[1], rest[2])], f"the other Pair did not stay: {pairs(new)}")
        check(scout(new, rest[2])["carried"], "the other Pair's Rider is no longer carried")
        partner = scout(new, rest[0])
        check(not partner["carried"] and partner["carrying"] is None, f"the Troop Leader's partner is left {partner}")
        check("no longer carried" in since(partner_role, mark) or not partner["carried"], "the partner was left carried")
        call("host", "room", action="join")
        wait(lambda: len(pairs(new)) == 2, "the partnerless to pair by Late Pairing", 180, 1)
        return f"{new} leads; the other Pair stayed carried; the host came back and Late Pairing paired it"

    def party_joins(self) -> str:
        boss = leader()
        others = [r for r in ROLES if r != boss]
        for role in others:
            call(role, "room", action="leave")
        wait(lambda: len(state(boss)["scouts"]) == 1, "everyone else to leave", 30)
        mark = marks()
        together(*[(role, "room", {"action": "join"}) for role in others])
        wait(lambda: len(pairs(boss)) == 2 and len(state(boss)["scouts"]) == 4, "everyone to be paired", 240, 1)
        count = since(boss, mark).count("Announced LatePairing")
        return f"all four paired again by {count} Late Pairing(s), as the three arrived"

    def rider_dies(self) -> str:
        carrier, rider, _, rider_role = self.guest_pair()
        mark = marks()
        call(rider_role, "console", name="Character.Die")
        wait(lambda: "Cleared the remains" in since(rider_role, mark), "the Rider to come back", 20)
        wait(lambda: (s := scout(rider_role, rider)) and not s["dead"] and s["carried"], "the Rider to be carried again", 20)
        return "the Rider came back on the Carrier, remains cleared"

    def forged_deaths(self) -> str:
        boss = leader()
        a = actors()
        others = [a[r] for r in ROLES if r != boss]
        wanted = sorted([(a[boss], others[0]), (others[1], others[2])])
        after_at = last_op_at(boss)
        order(boss, "assign", pairs=[list(p) for p in wanted])
        wait_operation(boss, "PairAssignment", after_at)
        wait(lambda: same_pairs_everywhere() == wanted, "the assignment", 20)
        c1, r1 = others[1], others[2]
        c1_role, r1_role, r2_role = role_of(c1), role_of(r1), role_of(others[0])
        call(c1_role, "drill", what="kill", actor=c1, how="consent", allow=False)
        time.sleep(1)
        mark = marks()
        for how in ("consent", "die", "sync"):
            call(r2_role, "drill", what="kill", actor=c1, how=how, allow=True, to=r1)
        time.sleep(3)
        refused = [line for line in since(r1_role, mark).splitlines() if "Refused a" in line]
        check(len(refused) == 3, f"the Rider refused {len(refused)} of 3: {refused}")
        check(not scout(r1_role, r1)["dead"] and not scout(r1_role, c1)["dead"], "a member died")
        call(r1_role, "drill", what="kill", actor=a[boss], how="die", to=a[r2_role])
        wait(lambda: scout(r2_role, a[r2_role])["dead"], "the allowed cannibal's kill to take", 10)
        mark = marks()
        call(c1_role, "console", name="Character.Die")
        wait(lambda: all(scout(r, c1)["dead"] and scout(r, r1)["dead"] for r in ROLES), "both members to die everywhere", 15)
        return "three forgeries refused; an allowed cannibal and a real death took both"

    def everyone_dies(self) -> str:
        together(*[(role, "console", {"name": "Character.Die"}) for role in ROLES])
        wait(lambda: all(state(r)["mod"]["report"] for r in ROLES), "every Run Report", 60, 1)
        reports = [state(r)["mod"]["report"] for r in ROLES]
        first = reports[0]
        check(all(r == first for r in reports), f"the reports differ: {reports}")
        check(first["ending"] == "Lost", f"the Run ended {first['ending']}")
        kinds = [b["kind"] for b in first["badges"]]
        check("RescueRanger" not in kinds, f"a Rescue Ranger without any rescue: {[b for b in first['badges'] if b['kind'] == 'RescueRanger']}")
        buddies = [b["amount"] for b in first["badges"] if b["kind"] == "BestBuddies"]
        check(all(x <= first["seconds"] + 0.5 for x in buddies), "a Pair together longer than the Run")
        return f"every copy: Lost after {first['seconds']:.0f} s, badges {kinds}"

    def back_at_airport(self) -> str:
        mark = marks()
        done: set[str] = set()

        def next_pressed() -> bool:
            done.update(r for r in ROLES if r not in done and end_screen_next(r))
            return len(done) == len(ROLES)

        try:
            wait(next_pressed, "every end screen", 20, 1)
        except Failed:
            pass
        stalled = [r for r in ROLES if r not in done]
        if stalled:
            # The game's end screen stops before its buttons for a scout who rejoined
            # mid-Run (their timeline is empty), so the room goes back as after it.
            call(leader(), "drill", what="end", ending="airport")
        wait(lambda: all(state(r)["scene"] == "Airport" for r in ROLES), "everyone at the Airport", 180, 1)
        for role in ROLES:
            wait(lambda r=role: notices(r, mark, "원정 보고서가 도착"), f"{role}'s report notice", 30)
        self.pictures.append("Roster after a Run: " + roster("guest", "원정 보고서").split("; ")[-1])
        return "the report notice reached every copy" + (f"; the end screen stalled on {', '.join(stalled)}" if stalled else "")

    def summit(self) -> str:
        boss = leader()
        call(boss, "depart")
        wait(lambda: all(state(r)["mod"]["formed"] and state(r)["scene"] != "Airport" for r in ROLES), "the next Run", 240, 1)
        time.sleep(3)
        self.pictures.append("Passport photo: " + call(boss, "photo", actor=actors()["guest"]))
        try:
            call(boss, "drill", what="end", ending="summit")
        except Failed:
            pass  # the game's own win path trips on the Twin's blocked stats; the report is what counts
        wait(lambda: all((rep := state(r)["mod"]["report"]) and rep["ending"] == "Summit" for r in ROLES), "the Summit report", 30)
        reports = [state(r)["mod"]["report"] for r in ROLES]
        check(all(r == reports[0] for r in reports), "the reports differ")
        buddies = [b["amount"] for b in reports[0]["badges"] if b["kind"] == "BestBuddies"]
        check(all(x <= reports[0]["seconds"] + 0.5 for x in buddies), "a Pair together longer than the Run")
        # The game's closing cutscene stalls in a Twin, so the room goes back as after the end screen.
        call(boss, "drill", what="end", ending="airport")
        wait(lambda: all(state(r)["scene"] == "Airport" for r in ROLES), "everyone at the Airport", 180, 1)
        return f"every copy: Summit after {reports[0]['seconds']:.0f} s"

    def unmodded_player(self) -> str:
        boss = leader()
        unmodded = "guest3" if boss != "guest3" else "guest2"
        others = [r for r in ROLES if r not in (boss, unmodded)]
        for role in others:
            call(role, "room", action="leave")
        self.twins.close(unmodded)
        self.twins.launch(unmodded, unmodded=True)
        wait(lambda: in_room(unmodded) and state(unmodded)["scene"] == "Airport", "the Unmodded Player to join", 240, 1)
        call(unmodded, "language", name="Korean")
        mark = marks()
        together(*[(role, "room", {"action": "join"}) for role in others])
        wait(lambda: len(state(boss)["scouts"]) == 4 and all(in_room(r) for r in others), "everyone back", 240, 1)
        name = name_of(actors()[unmodded])
        for role in [boss, *others]:
            wait(lambda r=role: notices(r, mark, name), f"{role} to tell of the Unmodded Player", 20)
        time.sleep(3)
        for role in [boss, *others]:
            count = len([n for n in notices(role, mark, name) if "모드가 없어" in n])
            check(count == 1, f"{role} told {count} times of the Unmodded Player")
        listed = roster(boss, "모드 없는 대원")
        check("모드 없는 대원" in listed, f"the Roster has no Unmodded row: {listed}")
        self.pictures.append("Roster with an Unmodded Player: " + listed.split("; ")[-1])
        order(boss, "rule", rule="AllowOddPlayerCount", on=False)
        # The kiosk reads the rule from the room, which shows it a round trip later, as a player sees it.
        wait(lambda: state(others[0])["mod"]["rules"]["AllowOddPlayerCount"] is False, "the rule to reach the room", 10)
        departing = marks()
        call(boss, "depart")
        wait(lambda: notices(boss, departing, "짝수여야"), "the kiosk to refuse an odd party", 15)
        check(state(boss)["scene"] == "Airport", "an odd party departed")
        order(boss, "rule", rule="AllowOddPlayerCount", on=True)
        wait(lambda: state(others[0])["mod"]["rules"]["AllowOddPlayerCount"] is True, "the rule to reach the room", 10)
        mark = marks()
        call(boss, "depart")
        wait(lambda: all(state(r)["mod"]["formed"] for r in [boss, *others]), "the Pairs to form", 240, 1)
        time.sleep(3)
        number = actors()[unmodded]
        check(all(number not in p for p in pairs(boss)), "the Unmodded Player is in a Pair")
        for role in [boss, *others]:
            check(not any(name in n and "짝 없이 올라갑" in n for n in notices(role, mark)), f"{role} told the Unmodded Player waits for a Pair")
        self.unmodded_role = unmodded
        return f"told once everywhere, listed on the Roster, an odd party refused then let go, never paired"

    def unmodded_leader(self) -> str:
        boss = leader()
        unmodded = getattr(self, "unmodded_role", None)
        if unmodded is None:
            raise Failed("needs the Unmodded Player check to pass first")
        others = [r for r in ROLES if r not in (boss, unmodded)]
        mark = marks()
        call(boss, "room", action="leave")
        wait(lambda: leader() == unmodded, "the Unmodded Player to lead", 30)
        for role in others:
            wait(lambda r=role: "Resting in the room" in since(r, mark), f"{role} to rest", 20)
            wait(lambda r=role: notices(r, mark, "모드를 쉽니다"), f"{role}'s resting notice", 15)
        time.sleep(2)
        for role in others:
            check(pairs(role) == [], f"{role} still has Pairs")
            count = len(notices(role, mark, "모드를 쉽니다"))
            check(count == 1, f"{role} was told {count} times that the mod rests")
        return "every copy with the mod rests, told once"


CHECKS = [
    ("setup", "Four Twins in the Airport"),
    ("roster_airport", "12 Roster at the Airport"),
    ("plan_and_formation", "E1/2/1 Mashed plan, Pair Plan, Formation"),
    ("four_scouts_shuffle", "E1/4/15 Mashed shuffle, four scouts, Handover places"),
    ("call_off", "E1/5 Call off"),
    ("recovery", "E1/8 Recovery"),
    ("assignment", "6 Pair Assignment"),
    ("role_swap", "7 Role Swap agreed"),
    ("swap_withdrawn_and_cancelled", "7 Role Swap withdrawn, cancelled by a shuffle"),
    ("mashed_swap", "E2 Mashed Role Swap"),
    ("start_expiry", "7 Role Swap left to lapse (checked later)"),
    ("forged_values", "13 Forged values"),
    ("warp_rider", "14 Warp with a Rider"),
    ("floods", "E3 Floods"),
    ("expiry_ends", "7 Role Swap lapses"),
    ("rules_and_timed", "9 Run Rules and Timed Shuffle"),
    ("leave_and_rejoin", "E4/17/E5/3 Leave inside the round trip, rejoin"),
    ("leader_leaves", "16 Troop Leader leaves"),
    ("party_joins", "E6 A party joining together"),
    ("rider_dies", "10 Shared Life: the Rider dies"),
    ("forged_deaths", "E8/10 Forged deaths, an allowed cannibal, the Carrier dies"),
    ("everyone_dies", "E7/11 Everyone dies: Lost"),
    ("back_at_airport", "11/12 Back at the Airport: report notice, Roster"),
    ("summit", "11/12 Summit, passport photo"),
    ("unmodded_player", "18 Unmodded Player"),
    ("unmodded_leader", "19 Unmodded Troop Leader"),
]


def main(argv: list[str]) -> int:
    keep, visible = "--keep" in argv, "--visible" in argv
    argv = [a for a in argv if a not in ("--keep", "--visible")]
    if len(argv) < 3:
        sys.exit(__doc__)
    rest = argv[3:]
    plugin = Path(rest[rest.index("--plugin") + 1]) if "--plugin" in rest else None
    only = set(rest[rest.index("--only") + 1].split(",")) if "--only" in rest else None
    PROGRESS.parent.mkdir(parents=True, exist_ok=True)
    PROGRESS.write_text("", encoding="utf-8")
    run = Run(Twins(Path(argv[1]), Path(argv[2]), plugin, visible))
    results = []
    for key, title in CHECKS:
        if only is not None and key != "setup" and key not in only:
            continue
        say(f">> {title}")
        began = time.monotonic()
        try:
            detail = getattr(run, key)()
            results.append((title, True, detail))
            say(f"   ok {time.monotonic() - began:5.1f}s  {detail}")
        except Exception as error:  # a failed check is reported, and the others still run
            why = str(error) if isinstance(error, Failed) else f"{type(error).__name__}: {error}"
            results.append((title, False, why))
            say(f"   FAIL {time.monotonic() - began:5.1f}s  {why}")
            if key == "setup":
                break
            try:
                run.mend()
            except Exception as mending:
                say(f"   could not mend the room: {mending}")
    failed = [r for r in results if not r[1]]
    say(f"Done in {time.monotonic() - START:.0f} s: {len(results) - len(failed)} passed, {len(failed)} failed.")
    for title, _, why in failed:
        say(f"  FAILED {title}: {why}")
    for picture in run.pictures:
        say(f"  Look at {picture}")
    if not keep:
        run.twins.close_all()
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))

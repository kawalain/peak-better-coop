# Testing in the game

Unit tests (`dotnet test`) cover the game-independent rules. Everything else
is tried in the game, in two Twins driven over the Control Line (see
`CLAUDE.md`), never in the player's own game. Items marked **two people**
need real hands on both copies (mouse, keys, voice) and are tried with
another player before a test build goes out.

## Setting up

```bash
dotnet build -c Debug -p:GaleModDir=
python -I tools/dev_profile.py twins <profile_dir> <PeakGameDir> --plugin src/BetterCoop/bin/Debug/netstandard2.1/BetterCoop.dll --control
```

The host Twin opens a room and the guest Twin joins it; both log verbosely.
`tools/peak_mcp.py` reaches them as `host` and `guest`. The commands used
below:

| Command | Use |
| --- | --- |
| `state` | Pairs, roles, Run Rules, Carry Record, Run Report, scouts and positions |
| `log {contains}` | the copy's log; every notice shown is logged as `[Notice]` |
| `screenshot {scale}` | the screen, for checking what a player sees |
| `menu {action}`, `controls`, `press {id}`, `scroll {to}` | the pause menu, the Roster, and any button on screen |
| `order {what, ...}` | a Roster order, under the Roster's rules |
| `depart` | boards for the Run from the Airport |
| `warp {to}` | moves this copy's scout, as the game's warp |
| `console {name}` | the game's console, e.g. `Character.Die`, `Character.TestWin` |
| `photo {actor}` | takes a passport photo again and saves it as a PNG |
| `drill {what: forge, key, actor?}` | writes a made-up mod value as a player who may not |

A Twin closed by force is started again with `--only host` or `--only guest`.

## Required before every test build

Run these in order in one pair of Twins. Each line is setup, action, and
what must happen.

1. **Formation.** At the Airport, `depart`. On landing both copies show the
   same Pairs; the Carrier carries the Rider; a Pair letter shows on both.
2. **Pair Plan.** At the Airport, `order {what: plan, pairs: [[2,1]]}`, then
   `depart`. The Pairs formed are the plan's.
3. **Leave and Late Pairing.** In a Run, close the guest and start it again.
   The host's notice names the guest by name, not `#2`; on rejoin one Late
   Pairing (one `Announced LatePairing` in the host log) reunites the Pair.
4. **Pair Shuffle.** `order {what: shuffle}`. Both copies count down 3 s with
   a Pair letter, then the Pairs change; no one is pushed.
5. **Call off.** `order {what: shuffle}` then at once `order {what: callOff}`.
   Both copies say the shuffle was called off; the Pairs stay.
6. **Pair Assignment.** `order {what: assign, pairs: [[1,2]]}`. The letter
   says what changes for its reader; the Pairs become the assignment's.
7. **Role Swap.** The guest `order {what: swap}`; the host's Roster shows the
   agree stamp first in My Pair; the host `order {what: swap}`. The roles swap.
   Asking twice withdraws; a request left 30 s expires; a shuffle cancels it.
   Each case shows its notice.
8. **Recovery.** `order {what: recover}`. Every client shows the recovery tag;
   the Pair is attached again.
9. **Run Rules.** Turn a rule on and change the shuffle interval. Every
   client gets one notice naming both changes; with Timed Shuffle at 1 min a
   shuffle runs about a minute after the last.
10. **Shared Life.** Kill the Rider (`console Character.Die` on its copy). The
    Rider comes back on the Carrier, with no remains left in front of the
    view. Kill the Carrier: both die.
11. **Run endings.** `console Character.TestWin`: the Run Report says Summit,
    and the time together of a Pair is no longer than the Run. Kill both: the
    report says Lost. Back at the Airport a notice says the report arrived; the
    Roster opens it.
12. **Photos.** Open the Roster on both copies at the Airport and after a Run:
    no photo is black or shows the default look. `photo {actor}` on the
    island gives a clear photo.
13. **Forged values.** `drill {what: forge, key: Pairs}` and
    `drill {what: forge, key: SwapRequest, actor: 1}` from the guest. Neither
    copy's Pairs change; the logs say the write was dropped and the Troop
    Leader put the room's value back.
14. **Warp with a Rider.** `warp {to: <Carrier>}` on the Rider's copy. The
    Carrier does not move.

## Features

### Roster

- Opens from the pause menu on every copy; the leader sees orders, others see
  My Pair, the plan, rules and the report.
- Role Swap is the first row of My Pair, hidden when there is no Pair or the
  rules forbid it; asking and agreeing are stamps of their own.
- Next shuffle is hidden when Timed Shuffle is off and nothing counts down.
- Closing it plainly sinks it; the hand takes it only when announcing (통보).
- The Zoo (pause menu, development builds) shows every paper component in
  its states; check a changed component there first.

### Notices

- Pair letters say what the change does to their reader: gains a Pair,
  changes partner, swaps places, loses the Pair, or a mix.
- A player who left is named by the last name seen.
- Rule changes the Troop Leader publishes together give one notice.

### Control Split and Shared View (two people)

- The Rider's look and use buttons reach the Carrier's body with the set
  latency and no jitter; scrolling picks items.
- The Rider sees the Carrier's crosshair, throw charge, rope length, item
  sounds, and the backpack wheel, and picks from it.
- The Rider's binoculars and previews follow the Carrier.

### Carrying (two people)

- Climbing, jumping and falling with a Rider never pushes either body apart.
- A Body Handover moves the Carrier to the place and items as the rules say.
- Carry weight, when on, slows the Carrier as the rules say.

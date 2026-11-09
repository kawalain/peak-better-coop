# Testing in the game

Unit tests (`dotnet test`) cover the game-independent rules. Everything else
is tried in the game, in Twins driven over the Control Line (see
`CLAUDE.md`), never in the player's own game. Items marked **two people**
need real hands on both copies (mouse, keys, voice), a real network, or a
real party, and are tried with other players before a test build goes out.

## Setting up

```bash
dotnet build -c Debug -p:GaleModDir=
python -I tools/dev_profile.py twins <profile_dir> <PeakGameDir> --plugin src/BetterCoop/bin/Debug/netstandard2.1/BetterCoop.dll --control
```

The host Twin opens a room and the guest Twin joins it; both log verbosely.
`tools/peak_mcp.py` reaches them as `host` and `guest`. For a party of four,
add `--guests 3` (`guest2`, `guest3`); `--unmodded guest3` makes that guest an
Unmodded Player. Photon makes the player with the lowest actor number Troop
Leader when one leaves, so to choose who takes over, start the host first and
then each guest with `--only` in the order wanted. The commands used below:

| Command | Use |
| --- | --- |
| `state` | Pairs, roles, Run Rules, Carry Record, Run Report, scouts and positions |
| `log {contains}` | the copy's log; every notice shown is logged as `[Notice]` |
| `screenshot {scale}` | the screen, for checking what a player sees |
| `menu {action}`, `controls`, `press {id}`, `scroll {to}` | the pause menu, the Roster, and any button on screen |
| `order {what, ...}` | a Roster order, under the Roster's rules |
| `depart` | boards for the Run from the Airport |
| `warp {to}` | moves this copy's scout, as the game's warp |
| `interact {actor}` | acts on that scout as the interact key does when aimed at them, e.g. a Rescue Carry |
| `console {name}` | the game's console, e.g. `Character.Die`; none that change what the account keeps |
| `photo {actor}` | takes a passport photo again and saves it as a PNG |
| `drill {what: forge, key, actor?}` | writes a made-up mod value as a player who may not |
| `drill {what: letter, case}` | plays one of the Zoo's Pair letter cases (0 to 5) |
| `drill {what: end, ending}` | in a Twin, ends the real Run at the summit or with the party lost |
| `sounds {contains, play?}` | the game's loaded audio clips and the effects that use them |

A Twin closed by force is started again with `--only <role>`. Close a Twin by
its process id (`hello` gives it), never by the game's name: the player's own
game may be running.

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
11. **Run endings.** `drill {what: end, ending: summit}`: the Run Report says
    Summit, and the time together of a Pair is no longer than the Run.
    `drill {what: end, ending: lost}` on the host: the report says Lost. Back at the Airport a notice says the report arrived; the
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

Then, in four Twins (`--guests 3`):

15. **Four scouts.** Plan two Pairs, `depart`, then `order {what: shuffle}`.
    Every copy shows the same Pairs, and every scout has a new partner. With
    the Pairs far apart and the Body Handover moving places, each new Carrier,
    a guest's too, stands where its former Shared Body stood (the game takes
    warps only from the Troop Leader).
16. **Troop Leader leaves with Pairs under way.** With the host in one Pair,
    close the host. The other Pair stays carried; the host's partner is not
    left marked carried (`carried: false`, log `no longer carried`); the next
    Troop Leader pairs the partnerless by Late Pairing.
17. **A Rider leaves while carried.** Close a carried Rider's copy. Their
    Carrier carries no one and plays on alone until a Late Pairing.

And in four Twins with an Unmodded Player (`--guests 3 --unmodded guest3`,
started so the Unmodded guest joins second):

18. **Unmodded Player.** Every copy with the mod tells once that the Unmodded
    guest climbs without a Pair; the Roster lists them under Unmodded with
    "No mod". With Allow Odd Player Count off and three who can pair, the
    kiosk blocks and says the Unmodded Player is not counted; with it on, the
    party departs and the Unmodded guest is never in a Pair, nor told of at
    Pair Formation as waiting for one.
19. **Unmodded Troop Leader.** Close the host while the Unmodded guest is
    next by actor number. The others lose their Pairs, each is told once that
    the mod rests in this room, and their Roster says why instead of showing
    a board.

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
  changes partner, swaps places, loses the Pair, or a mix. The Zoo, and
  `drill letter`, play each case.
- A player who left is named by the last name seen.
- Rule changes the Troop Leader publishes together give one notice.

### Control Split and Shared View (two people)

- The Rider's look and use buttons reach the Carrier's body with the set
  latency and no jitter; scrolling picks items.
- The Rider sees the Carrier's crosshair, throw charge, rope length, item
  sounds, and the backpack wheel, and picks from it.
- The Rider's binoculars and previews follow the Carrier.
- Grabbing and letting go of a wall follows the Rider's own Climb Input
  (Hold or Toggle), whatever the Carrier's is.

### Carrying (two people)

- Climbing, jumping and falling with a Rider never pushes either body apart.
- A Body Handover moves the Carrier to the place and items as the rules say.
- Carry weight, when on, slows the Carrier as the rules say.

### Rescue Carry

In four Twins with two Pairs. `console {name: CharacterAfflictions.Starve}` on
a Carrier's copy passes that Shared Body out (a Body Handover passes on the
hunger; `CharacterAfflictions.ClearAll` wakes it).

- The other Carrier `interact {actor}` with it: the Rider's log says `Picking
  up`, and `state` shows Carrier, Rider, Rescued, and the Rescued's Rider each
  carrying the next. The Carrier's back slot shows the Rescued, and the
  Rescued is still alive a minute and more later.
- With Late Pairing off, `order {what: assign}` with one Pair leaves two
  Unpaired Players: a Pair rescues a passed-out one, and an Unpaired Player
  rescues a passed-out Shared Body with its Rider still on it.
- The rescuer's log says `Putting down` when Rescue Carry is turned off, just
  before a Pair Shuffle, and when the rescuing Shared Body passes out; a
  Rescued who wakes is put down as in unmodded PEAK. Every Pair's own carry
  holds throughout.
- (two people) The Carrier's back item drops where the Rescued lay; tapping
  the back-slot key, or picking up a backpack, puts the Rescued down. The
  stack stays together while climbing and falling.

### Paper sounds (two people)

- A page rising, a stamp coming down, a draft crushed and tossed, and the
  Scoutmaster's hand taking a page each sound once, quieter than the game's
  own menu, at the Airport and on the island. `sounds {contains: Passport}`
  lists them, with `BetterCoop` copies once played.

### Real play (two people)

- A Campfire Shuffle when a campfire is lit: the game's console jump to a
  segment does not take the campfire's path, so it is tried in real play.
- The party with real Steam accounts, through a Steam lobby invite: the
  Twins share one account and skip the lobby.
- A real network, with players far apart: the Twins share one computer.
- An Unmodded Player with the plain game, not a Twin: their carry of a
  passed-out Pair member is set down at once by the Troop Leader.
- A gamepad, the English game, and the other mods the players use.

## Known limits

- When the Troop Leader changes, a Pair Shuffle that was waiting behind a
  Role Swap or Late Pairing is lost (a Timed Shuffle comes again), up to 15
  seconds of carry time can be missing from the Carry Record, and a Troop
  Leader who leaves just as the Run ends leaves no Run Report. None of these
  leaves a Pair or operation stuck.
- When Photon makes an Unmodded Player the Troop Leader, everyone else rests
  until a player with the mod leads again.

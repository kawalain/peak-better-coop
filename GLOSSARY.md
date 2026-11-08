# BetterCoop

A PEAK mod in which players climb in two-person teams: one player carries the
other for the whole climb, the two control the carrying body together, and they
share stamina, death, and revival.

Each term gives its Korean word in parentheses; Korean player text uses it.

## Language

### Runs

**Run** (원정):
One expedition, from departing at the Airport check-in kiosk until the party
is back at the Airport.
_Avoid_: game, match, session, level

**Troop Leader** (분대장):
The player who hosts the room; the game's own name for the host.
_Avoid_: host, 방장 (in player text)

### Teams

**Pair** (짝):
Two players bound together, one as Carrier and one as Rider, from a Pair
Formation, a Late Pairing, or a Pair Assignment until a Pair Shuffle, a Role
Swap, a Pair Assignment, a member leaving, or the end of the Run.
_Avoid_: partner, couple, team

**Carrier** (업는 사람):
The member of a Pair who carries the other; their keyboard drives the shared
body.
_Avoid_: host, driver, keyboard side, 운반자

**Rider** (업힌 사람):
The member of a Pair who is carried; their mouse drives the shared body.
_Avoid_: climber, passenger, carried player, mouse side, 탑승자

**Shared Body** (공유 몸):
The Carrier's character, which both members of a Pair control at once.

**Control Split** (조작 분담):
The rule that divides the Shared Body's game actions between the Pair: look,
use, secondary use, scroll, and ping belong to the Rider, and every other
action belongs to the Carrier. With default bindings this is mouse to the Rider
and keyboard to the Carrier. In the backpack wheel, the Carrier opens it and
holds it open, and the Rider picks from it.
_Avoid_: input relay, keyboard/mouse split

**Shared View** (시점 공유):
The rule that both members of a Pair see and hear the Shared Body as its
Carrier does: the same first-person view, crosshair, previews, progress,
screen effects, and item sounds, and the same HUD (slots, remaining uses,
stamina, statuses), on the Rider's screen as on the Carrier's.
_Avoid_: spectating, mirroring

**Pair Formation** (짝 편성):
Forming the Pairs for the first time in a Run, when the party arrives on the
island.
_Avoid_: 짝짓기, 짝 짓기, matchmaking

**Role Preference** (원하는 역할):
A player's stated wish to be Carrier, Rider, or either (상관없음; stored as Random) in a Pair
Formation, a Pair Shuffle, or a Late Pairing.

**Pair Shuffle** (짝 섞기):
Forming the Pairs anew in the middle of a Run so that every Pair changes:
new partners where there are enough players, otherwise the same two with
their roles traded. Role Preferences hold where they allow it.
_Avoid_: re-pairing, reshuffle

**Late Pairing** (짝 이어주기):
Forming a Pair in the middle of a Run from two Unpaired Players, leaving every
other Pair as it is. Two whose Pair ended when one of them left pair again
first, in their former roles. A member who is dead or passed out becomes the
Rider and comes back on the Carrier's back; otherwise Role Preferences decide.
The Shared Body starts where the higher of the two is.
_Avoid_: re-pairing, late join pairing

**Pair Assignment** (짝 지정):
The Troop Leader setting the Pairs as they choose, one Pair or all of them at
once, naming who carries in each. Players they leave out become Unpaired
Players and pair up by Late Pairing; the next Pair Shuffle mixes the assigned
Pairs like any other.
_Avoid_: manual pairing, forced pair

**Campfire Shuffle** (모닥불 짝 섞기):
A Pair Shuffle when a campfire is lit and the Run moves on to the next segment.

**Timed Shuffle** (시간마다 짝 섞기):
A Pair Shuffle at a fixed interval set by the Troop Leader.

**Body Handover** (몸 인계):
When Pairs change, each new Shared Body takes over one former Shared Body:
its statuses always, and its place and items as the Troop Leader sets. Nothing
is lost or copied: the items swap between the two bodies.
_Avoid_: body swap, inheritance

**Role Swap** (자리 바꾸기):
The Carrier and Rider of a Pair trading roles when both ask for it or the
Troop Leader orders it, with a Body Handover to the new Carrier.
_Avoid_: role switch, flip

**Shared Life** (생사 공유):
The rule that the Rider lives and dies with the Shared Body: the Rider dies
when the Carrier dies, comes back when the Carrier comes back, and never passes
out or dies alone. Reviving either member revives both.
_Avoid_: shared death, shared revival

**Unpaired Player** (짝 없는 사람):
A player in the room who is not in any Pair and plays as in unmodded PEAK,
until a Late Pairing or a Pair Shuffle gives them a partner. A player joining
during a Run starts as one.
_Avoid_: solo player, odd player

### Controls

**Roster** (편성표):
The page of the pause menu for the mod, drawn as a paper form: every player sees the Pairs and the
Unpaired Players, sets their Role Preference, and asks for a Role Swap; the
Troop Leader also changes the Pairs and the Run Rules there.
_Avoid_: Coop Menu, 코옵 메뉴, coop panel, admin menu

**Run Rules** (원정 규칙):
The Troop Leader's settings that hold for everyone in the room: shuffles,
Body Handover, carry weight, odd player counts, and who picks from the
backpack wheel.
_Avoid_: host settings, room settings

**Solo Pair** (1인 짝 테스트):
A debug mode in which a player alone in the room plays both Carrier and Rider
of one Shared Body, with the Rider's actions held back as if sent over the
network.
_Avoid_: solo mode, loopback

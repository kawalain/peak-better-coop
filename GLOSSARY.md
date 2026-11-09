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

**Carry Record** (업기 기록):
What each player has done in Pairs and Rescue Carries over a Run: the time
spent as Carrier, the time spent as Rider, the height climbed while carrying,
counting only climbs, not falls, and the time spent carrying a Rescued. Time
with no partner or with the Shared Body dead counts only as rescue time, and
height does not count while the Shared Body is passed out. It follows the
player through leaving and rejoining, and starts afresh when the next Run
departs.
_Avoid_: stats, score, leaderboard

**Run Report** (원정 보고서):
The paper the party gets back at the Airport after a Run: how it ended, how
long it took, who carried whom in each segment, everyone's Carry Record, and
the Merit Badges. Players who left during the Run are on it too. It lasts
until the next Run departs.
_Avoid_: end screen, summary, results

**Merit Badge** (공로 배지):
A playful title on the Run Report for the best Carry Record in one thing: the
most time carrying, the most time carried, the most height climbed carrying,
the most time rescuing, and the two who stayed a Pair the longest.
_Avoid_: achievement, award, trophy

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
island. Pairs in the Pair Plan whose members are both there are kept; everyone
else is paired at random.
_Avoid_: 짝짓기, 짝 짓기, matchmaking

**Pair Plan** (짝 계획):
The Pairs the Troop Leader sets at the Airport before a Run, naming who carries
in each. Nobody is carried at the Airport; the Pair Formation on arrival uses
the plan, over Role Preferences, and the plan ends there or when the party is
back at the Airport. Every player sees it on the Roster. When the same scouts
gather again, the Troop Leader's plan starts from the Pairs the last Run ended
with.
_Avoid_: pre-pairing, lobby pairs, 예약 짝

**Role Preference** (원하는 역할):
A player's stated wish to be Carrier, Rider, or either (상관없음; stored as Random) in a Pair
Formation, a Pair Shuffle, or a Late Pairing, followed unless the Run Rules
say otherwise. A Pair Plan and a Pair Assignment always come before it.
_Avoid_: role wish, 희망 역할

**Pair Shuffle** (짝 섞기):
Forming the Pairs anew in the middle of a Run so that every Pair changes:
new partners where there are enough players, otherwise the same two with
their roles traded. Role Preferences hold where they allow it. The Troop
Leader can call off a Pair Shuffle while it counts down; the Timed Shuffle
then counts again from the start.
_Avoid_: re-pairing, reshuffle

**Late Pairing** (짝 이어주기):
Forming a Pair in the middle of a Run from two Unpaired Players, leaving every
other Pair as it is, unless the Run Rules turn it off. Two whose Pair ended when one of them left pair again
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
The Carrier and Rider of a Pair trading roles when both ask for it, if the
Run Rules allow asking, or when the Troop Leader orders it, with a Body
Handover to the new Carrier.
_Avoid_: role switch, flip

**Shared Life** (생사 공유):
The rule that the Rider lives and dies with the Shared Body: the Rider dies
when the Carrier dies, comes back when the Carrier comes back, and never passes
out or dies alone. Reviving either member revives both.
_Avoid_: shared death, shared revival

**Rescue Carry** (구조 업기):
Carrying a passed-out scout so that the time they have left stops running out,
as in unmodded PEAK, by someone in a Pair or not, unless the Run Rules turn it
off: a Shared Body carries the Rescued on its Rider's back, and an Unpaired
Player carries a passed-out Shared Body with its Rider still on it. A Shared
Body carries one Rescued at a time, picks them up and puts them down as in
unmodded PEAK, and drops its back item to do so. The Rescued is put down when
they wake or die, when the Shared Body carrying them passes out or dies, and
before its Pair changes. Nobody is told about it, as in unmodded PEAK.
_Avoid_: nested carry, double carry, 중첩 업기

**Rescued** (구조된 사람):
The passed-out scout in a Rescue Carry: an Unpaired Player, or a Shared Body
with its Rider.
_Avoid_: passenger, carried player

**Unpaired Player** (짝 없는 사람):
A player in the room who is not in any Pair and plays as in unmodded PEAK,
until a Late Pairing or a Pair Shuffle gives them a partner. A player joining
during a Run starts as one.
_Avoid_: solo player, odd player

**Unmodded Player** (모드 없는 대원):
A player in the room whose game does not run the mod, or runs a version that
cannot work with the Troop Leader's. They climb as Unpaired Players, are told
which version the Troop Leader has, and do not hold the party at the Airport.
_Avoid_: vanilla player, outdated player, 바닐라

### Controls

**Roster** (편성표):
The page of the pause menu for the mod, drawn as a paper form: every player sees the Pairs and the
Unpaired Players, reads the Run Rules, sets their Role Preference, and asks for
a Role Swap; the Troop Leader also changes the Pairs and the Run Rules there,
and at the Airport sets the Pair Plan.
_Avoid_: Coop Menu, 코옵 메뉴, coop panel, admin menu

**Run Rules** (원정 규칙):
The Troop Leader's settings that hold for everyone in the room: whether Role
Preferences are followed, whether Role Swaps may be asked for, whether Late
Pairing happens, odd player counts, shuffles, Body Handover, carry weight,
Rescue Carry, and who picks from the backpack wheel. Every rule that changes
play is one; each
starts out leaving players free. A new Troop Leader keeps the rules the room
has. Every player can read them on the Roster and is told when they change.
_Avoid_: host settings, room settings

**Twin** (분신):
A copy of the game started for testing on the same computer, in a development
build, apart from the player's own game: its own profile, its own scout, and
nothing kept on the player's account. Twins test together as real players: one
Twin opens a room and the others join it. A Twin never joins the player's own
game.
_Avoid_: second client, clone, bot

**Control Line** (조종선):
A development build's way for test tools on the same computer to look at the
game and give it orders, such as reading the Pairs or ordering a Pair Shuffle.
It is off unless asked for at launch.
_Avoid_: debug server, remote, backdoor

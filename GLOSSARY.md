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
Two players bound together, one as Carrier and one as Rider, from when Pairs
are formed until a Pair Shuffle, a Role Swap, a member leaving, or the end of
the Run.
_Avoid_: partner, couple, team

**Carrier** (업는 사람):
The member of a Pair who carries the other; their keyboard drives the shared
body.
_Avoid_: host, driver, keyboard side, 운반자

**Rider** (업힌 사람):
The member of a Pair who is carried; their mouse drives the shared body.
_Avoid_: climber, passenger, carried player, mouse side, 탑승자

**Shared Body**:
The Carrier's character, which both members of a Pair control at once.

**Control Split**:
The rule that divides the Shared Body's game actions between the Pair: look,
use, secondary use, scroll, and ping belong to the Rider, and every other
action belongs to the Carrier. With default bindings this is mouse to the Rider
and keyboard to the Carrier. In the backpack wheel, the Carrier opens it and
holds it open, and the Rider picks from it.
_Avoid_: input relay, keyboard/mouse split

**Shared View** (시점 공유):
The rule that both members of a Pair see and hear the Shared Body as its
Carrier does: the same first-person view, crosshair, previews, progress, and
item sounds, on the Rider's screen as on the Carrier's.
_Avoid_: spectating, mirroring

**Role Preference**:
A player's stated wish to be Carrier, Rider, or either (Random) when Pairs are
formed.

**Pair Shuffle** (짝 섞기):
Forming the Pairs anew in the middle of a Run so that every Pair changes:
new partners where there are enough players, otherwise the same two with
their roles traded. Role Preferences hold where they allow it.
_Avoid_: re-pairing, reshuffle

**Campfire Shuffle**:
A Pair Shuffle when a campfire is lit and the Run moves on to the next segment.

**Timed Shuffle**:
A Pair Shuffle at a fixed interval set by the Troop Leader.

**Body Handover** (몸 인계):
When Pairs change, each new Shared Body takes over one former Shared Body:
its statuses always, and its place and items as the Troop Leader sets. Nothing
is lost or copied: the items swap between the two bodies.
_Avoid_: body swap, inheritance

**Role Swap** (자리 바꾸기):
The Carrier and Rider of a Pair trading roles when both ask for it, with a
Body Handover to the new Carrier.
_Avoid_: role switch, flip

**Shared Life**:
The rule that the Rider lives and dies with the Shared Body: the Rider dies
when the Carrier dies, comes back when the Carrier comes back, and never passes
out or dies alone. Reviving either member revives both.
_Avoid_: shared death, shared revival

**Unpaired Player** (짝 없는 사람):
A player in the room who is not in any Pair and plays as in unmodded PEAK.
_Avoid_: solo player, odd player

**Solo Pair** (1인 짝 테스트):
A debug mode in which a player alone in the room plays both Carrier and Rider
of one Shared Body, with the Rider's actions held back as if sent over the
network.
_Avoid_: solo mode, loopback

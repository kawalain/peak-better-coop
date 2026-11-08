# Revives are sent by the Troop Leader

Shared Life brings the Rider back with the Carrier, and pairing wakes a Rider
who is passed out. The Rider's client used to send those revive calls for its
own character. The game itself sends revives from the Troop Leader, and other
mods treat a revive from anyone else as a cheat and warn every player each
time it happens. Now the Rider's client asks the Troop Leader, and the Troop
Leader sends the revive; a Troop Leader who is the Rider sends it directly.

## Consequences

- A Rider comes back one network round trip later than before.
- The Rider's client keeps following the Carrier's state (ADR 0002), so a
  request lost to a Troop Leader leaving is asked again of the next one.
- Deaths and places still come from the body's owner, as the game sends them.

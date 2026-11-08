# Pairs are authoritative room state, not inferred from the carry

The master client forms Pairs once per Run and publishes them as Photon room
custom properties; every client reads Pairs from there, and the game's carry
state (`carrier` / `carriedPlayer`) is only a consequence of a Pair, never the
source of it. The previous version inferred partners from the live carry in
four separate places, which drifted apart and kept stale partners after a drop
(for example, shared death outliving the carry).

## Consequences

- A Pair survives drops, pass-outs, deaths, and segment changes for the whole
  Run; it ends only when the Run ends or a member leaves the room.
- Features that need a partner (shared stamina, death, revival, input relay,
  HUD) look up the Pair, so they agree even while the carry is momentarily
  broken.

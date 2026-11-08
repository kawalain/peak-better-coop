# A small, read-only public API for other mods

Other mods meet BetterCoop's Pairs without knowing them: a status-bar mod
shows a Rider's empty bar, a spectating mod follows a Rider's idle body.
BetterCoop holds its outcomes against such mods (ADR 0006), but a mod that
wants to cooperate has had nothing to read except the internal room
property format. BetterCoop now publishes one static class,
`BetterCoop.Api`, that other mods reference as a soft dependency: whether
the mod is enabled, a character's role, partner, and Shared Body, and events
when the Pairs change and when a Pair change starts and finishes.

The API only reads. Letting other mods ask for Pair Shuffles or form Pairs
would make them part of the Troop Leader's one-operation-at-a-time rule
(ADR 0004), a larger contract than any mod has asked for yet.

## Consequences

- The API is a contract. Its own integer version rises only on a change that
  breaks callers; adding members does not raise it.
- When the mod is disabled (ADR 0003) the API reports itself disabled and
  every character as unpaired, rather than throwing.
- Calls and event handlers never let an exception cross between BetterCoop
  and the calling mod.
- The room property formats stay internal and may change in any version.

# Disable the whole mod when any patch fails

If any Harmony patch target is missing or changed after a game update, the mod
disables itself entirely and shows a notice, instead of running the features
that still patch. The features depend on each other (the Control Split without
the carry lock, or a Pair without shared death), and a half-working mod produced
exactly the hard-to-diagnose bugs of the previous version after the last update.

## Consequences

- A disabled mod publishes no protocol, so that player is an Unmodded Player
  (ADR 0009): they climb along without a Pair, and no Pair runs half-modded.
  Before 0.6.0 such a player kept the whole party at the Airport.
- The same holds when Co-op Mod, the mod BetterCoop grew from, is loaded too:
  both patch the carry and shared stamina, so the mod turns itself off and asks
  the player to remove Co-op Mod.
- The rule covers the mod's own load only. Harmony reruns every rewrite of a
  game method whenever any mod patches that method again; a rewrite that no
  longer fits then leaves the method as it is and turns off just its own
  feature, rather than failing the other mod's patching. The backpack wheel's
  key check is the one rewrite that can stop fitting; without it the wheel
  works as in the unmodded game, for the Carrier only.
- At run time the mod fails open instead: every patch is wrapped so that one
  that throws is logged and skipped, and the game method, with other mods'
  patches, runs on as unmodded for that call. An exception in a patch would
  otherwise reach the game's caller and stop code that is not the mod's.

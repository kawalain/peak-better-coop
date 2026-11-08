# Disable the whole mod when any patch fails

If any Harmony patch target is missing or changed after a game update, the mod
disables itself entirely and shows a notice, instead of running the features
that still patch. The features depend on each other (the Control Split without
the carry lock, or a Pair without shared death), and a half-working mod produced
exactly the hard-to-diagnose bugs of the previous version after the last update.

## Consequences

- A disabled mod publishes no version, so the host's party gate reports that
  player as missing the mod and the Run cannot depart half-modded.

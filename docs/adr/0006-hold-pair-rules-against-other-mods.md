# Hold the Pair's rules against other mods by outcome, not by name

Players run BetterCoop beside many other mods, and some change the same
things for every carried player: a carried player who is awake is moved to a
spectating camera, loses their input, or is handed the carrier's backpack.
A Rider is exactly such a player. Instead of detecting particular mods and
turning off or patching around each, BetterCoop holds the outcomes a Pair
depends on, whatever produced the change:

- The Rider's actions are read before any other patch on input sampling, so
  a patch that clears a carried player's input afterwards changes nothing.
- After every other patch on the camera, a camera that was turned to
  spectating while the Rider is conscious on their Carrier's back goes back to
  the first-person view the game had placed (Shared View).
- A back item that lands on the Rider moves onto the Carrier's back when it is
  empty (ADR 0002), so a backpack another mod hands to the carried player
  stays with the Shared Body instead of falling to the ground.
- Drops of a live Pair and carries of a Pair member by anyone else stay
  refused, as before.
- Status bars that another mod draws for other players are hidden for every
  Rider, whose own statuses are kept clear and say nothing, and on a Rider's
  screen also for their Carrier, whose bar is already the Rider's own HUD
  (Shared View). BetterCoop finds the HUD components that point at such a
  character and makes them invisible, leaving the other mod's lists and logic
  alone.
- Revives are sent by the Troop Leader, as the game's own revives are (ADR
  0007), so a mod that flags revives from anyone else sees nothing amiss.

Naming mods would break on their next update and would miss every mod not yet
seen; disabling BetterCoop beside them would stop a whole party for a camera
setting. Co-op Mod remains the one exception (ADR 0003): it pairs players by
its own rules, which cannot be held alongside.

## Consequences

- The other mod's behaviour is untouched for players outside a Pair.
- Two mods that both insist on a later patch order can still meet; the
  Rider's camera log records when another patch turns the Rider to
  spectating.
- A mod that shows only the nearest few players still counts a hidden bar, so
  it shows one fewer. Taking Riders out of the character list other mods read
  would avoid that, but would also hide them from those mods' other decisions,
  and on a Rider's client would hide the Carrier too.

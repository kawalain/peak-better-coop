# The Pair controls one body, split by action

The Pair plays through one Shared Body, the Carrier's character. The Rider owns
look, use, secondary use, scroll, and ping; the Carrier owns every other action
(movement, jump, sprint, crouch, interact, hotbar, drop, emotes). The previous
version gave the Rider the hands (interact, hotbar, drop) and the Carrier the
look, which left interaction unreachable and made aiming lag; players expected a
keyboard/mouse split instead.

The split is defined per game action, not per physical device, so rebinding a
key never moves an action to the other player.

## Consequences

- The game simulates the Shared Body only on the Carrier's client, so the
  Rider's actions are sent there and the Carrier's own copies of those actions
  are ignored. Interaction and item aim follow the Rider's look as it arrives on
  the Carrier's client.
- The Rider's screen renders from the Carrier's head with the Rider's own,
  locally applied look, so the person aiming sees no network delay; the
  Carrier's view trails by one network trip.
- The Rider stays conscious while carried, so the Rider's client shows the same
  interaction prompts without delay. The game's carry otherwise requires a
  passed-out passenger, so the carry is held by the mod for the whole Run.
- The Rider's keyboard plays no part beyond menus and push-to-talk, and the
  Rider's own inventory is idle while paired. The Shared Body has only the
  Carrier's three hand slots: carrying leaves no room for a backpack.

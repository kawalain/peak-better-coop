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
- The Rider stays conscious while carried, so the Rider's client names what
  the Rider looks at without delay. The game's carry otherwise requires a
  passed-out passenger, so the carry is held by the mod for the whole Run.
- Each screen prompts only its owner's actions: the Rider's shows the held
  item's use, secondary-use, and scroll prompts and the use progress (sent
  back from the Carrier's client, where the use runs); the Carrier's shows the
  interaction, drop, and throw prompts. Names show on both.
- PEAK moves a body relative to where it looks, so the Rider steers and the
  Carrier only throttles; the Carrier cannot turn on their own.
- PEAK grips a wall with an empty-handed primary use and lets go on release,
  so the Rider holds the grip and the Carrier climbs. The Rider's held actions
  are sent reliably and stay held through packet gaps, so lag never drops the
  climb.
- Carries never nest: while paired, a Carrier starts no other carry and a
  passed-out Carrier cannot be carried; others help them where they lie.
  Superseded by ADR 0010: a Rescue Carry stacks the Rescued on the Rider.
- The Shared Body has one set of statuses, the Carrier's. The Rider's own
  statuses are cleared at pairing and kept clear, and the Rider lives or dies
  only with the Shared Body (Shared Life): the Rider's client follows the
  Carrier's state rather than death and revival events, which arrive from
  different senders in no fixed order.
- The Rider's keyboard plays no part beyond menus and push-to-talk, and the
  Rider's own inventory is idle while paired: the Rider's hands and back stay
  empty. An item that lands in the Rider's hand (one spawned there) is
  dropped at the Shared Body for the Carrier to pick up; a back item (one worn
  before pairing, or handed over by another mod's carry) moves onto the
  Carrier's back when that is empty, and is dropped there otherwise.
- The Shared Body has the Carrier's three hand slots and back slot. The back
  slot holds a backpack, fanny pack, jetpack, or rocket pack as usual; a tap of
  the back-slot key brings that item to hand and never puts the Rider down,
  and holding it opens the Carrier's own backpack wheel, which the game
  otherwise offers only for a backpack on the ground or on someone else.

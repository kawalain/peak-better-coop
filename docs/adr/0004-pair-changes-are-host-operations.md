# Pair changes are host operations published in one write

Pair Shuffles and Role Swaps change who carries whom in the middle of a Run,
together with a Body Handover of places, items, and statuses. The Troop Leader
runs them one at a time as operations: it announces each in a room property
with a server time three seconds ahead, refuses pickups by the players involved
from half a second before, moves the inventories itself (the master owns them),
and then writes the new Pairs, the handover plan, and the finished operation in
one `SetCustomProperties` call. Every client counts down from the server time
and applies its own part of the plan (statuses and places, which only a body's
owner can set) once per operation id.

Sending each part as its own event looked simpler, but a client could then see
new Pairs without their handover, or a handover against the old Pairs, and
Photon gives no order across senders. One property write arrives whole, a late
joiner reads the same state, and a new Troop Leader finds an operation that is
not done and runs it.

## Consequences

- One operation at a time. A Pair Shuffle that comes during another merges into
  it; one that comes during a Role Swap or Late Pairing waits for it; a Role
  Swap request during a Pair Shuffle is cancelled.
- Held items are separate network objects, so hands are emptied and equipping is
  blocked from just before the run until the handover arrives (or five seconds
  pass), and a forced inventory sync puts away anything still held.
- Operation ids only grow within a room, so a client never applies a handover
  twice or an old one again; going back to the Airport finishes any operation
  instead of clearing the id.
- If the Troop Leader leaves after moving inventories but before the write, the
  new one runs the operation again; the Shared Bodies stay intact, but items may
  move twice.
- A Late Pairing is an operation too, run whenever two Unpaired Players with
  the same mod version and a spawned character are present, at least one of
  them awake, and no other operation is running. A character counts only five
  seconds after it spawns: mods that let players join during a Run move and
  revive the newcomer in those first seconds, and would pull a Rider off the
  Carrier's back. A Pair that ends because a member left is kept in the room
  by the players' user ids, since a reconnecting player gets a new actor
  number; when both are Unpaired again they pair first, in their former roles,
  and the left-behind member does not wait for them otherwise.
- A Pair Assignment, which the Troop Leader orders from the Roster, is an
  operation too. The Pairs it sets are written with its announcement, under
  their operation's id, so a new Troop Leader runs it with the same Pairs; the
  Troop Leader checks them again when it runs, as a member may have left. It
  concerns every player, as a Pair Shuffle does. It moves no items or places:
  each new Carrier keeps their body and their Rider comes to it, while a Pair
  only turned round hands its body over as a Role Swap does. Players it leaves
  out are Unpaired for a Late Pairing. A Pair Shuffle the Troop Leader orders
  there runs as the usual operation. It moves no items and leaves
  the new Rider's statuses to be cleared as at any pairing; its plan only takes
  the Shared Body to the higher member's place. A lone Unpaired Player waits
  rather than breaking up another Pair.

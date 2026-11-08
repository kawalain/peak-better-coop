# Shared View: aim is computed by the Rider, state is sent by the Carrier

The game draws an item's feedback (placement previews, crosshair, throw bar,
rope length, use progress, owner-only sounds) only on the client that owns the
character, which for a Shared Body is the Carrier's. The Rider's screen gets
the same feedback by one of two routes, picked per piece:

- What depends on aim is computed on the Rider's client from the Rider's own
  look: placement previews, whether a hook or shooter can hit, and the
  highlight on an aimed-at player. The Rider's camera is already at the
  Carrier's head, and the use lands where the Rider aims once the look reaches
  the Carrier's client, so this shows the Rider's aim with no network delay.
- What is the Carrier's game state is sent from the Carrier's client: use
  progress, throw charge, rope length, the crosshair chosen from movement, and
  item sounds the game plays only for the owner.

Sending everything from the Carrier would be simpler, but every preview would
then trail the Rider's mouse by a round trip, the delay the Control Split
moved look to the Rider to avoid (ADR 0002).

## Consequences

- A preview on the Rider's screen can disagree with the final placement for a
  moment when the Rider moves the mouse just as the use finishes; the
  placement follows the look that reached the Carrier's client.
- Values sent at a fixed rate are smoothed on the Rider's screen: the Rider's
  progress bar keeps moving at the last observed rate between updates and
  clears at once when the use stops.
- Sounds the game already plays for everyone are never relayed, so nothing
  plays twice.
- The backpack wheel is an input menu, not feedback, so it follows the Control
  Split instead: the Carrier opens it (interact, or holding the back-slot key
  for the Carrier's own backpack) and holds it open, the Rider's screen shows
  it, and the Rider's click picks a slice, which the Carrier's client carries
  out. Releasing the key first closes it with no pick. A Run rule hands the
  pick back to the Carrier, with the wheel on the Carrier's screen only.

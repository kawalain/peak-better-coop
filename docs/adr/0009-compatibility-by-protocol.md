# Compatibility by protocol, and Unmodded Players climb along

Until 0.6.0 two clients worked together only when their mod version strings
matched exactly, and the party could not depart while anyone lacked the mod
or ran another version. That made every patch a forced update for the whole
party and kept players without the mod out of the Run entirely. The mod now
publishes a protocol number, raised only when the shared values or events
change shape; clients with the same protocol work together whatever their
version. A player without the mod, or with another protocol, is an Unmodded
Player: they depart with the party and climb as an Unpaired Player, and are
told which version the Troop Leader has. A client whose Troop Leader speaks
another protocol, or none, as when Photon makes an Unmodded Player the Troop
Leader after the last one left, sits the mod out until that changes.

## Consequences

- A change to a room or player property, an event code, or a wire format
  raises the protocol in the same commit. A version bump alone never does.
- Only players with the Troop Leader's protocol are counted for the odd
  player count, Pair Formation, Pair Shuffles, and Late Pairing.
- Letting Unmodded Players take part in Pairs is left for later: it depends
  on which game RPCs the Troop Leader may send on their behalf.

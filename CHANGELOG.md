# Changelog

## 0.6.0

Players need 0.6.0 to be paired with a 0.6.0 Troop Leader. From 0.6.0 on,
patch versions work together, and players without the mod no longer keep the
party at the Airport.

### New

- **Roster** (ESC → Roster): the Pairs as passport photos, your partner and
  role, Role Preference, and Swap Places. The Troop Leader rearranges,
  shuffles, and plans the Pairs there, and sets the Run Rules.
- **Run Report**: how long each scout carried and rode, who carried whom in
  each segment, and Merit Badges. It updates during the Run and stays until
  the next one departs.
- **Late Pairing**: players who join mid-Run, or lose their partner, are
  paired without waiting for a shuffle; a reconnecting player gets their
  partner back.
- New Run Rules: Honour Role Preferences, Allow Role Swaps, Late Pairing.
- Notices now arrive on paper.
- `BetterCoop.Api` for other mods: read a character's role, partner, and
  Shared Body, and hear when the Pairs change (see the README).

### Changed

- Players without the mod, or with a version that does not work with the
  Troop Leader's, depart with the party and climb without a Pair; everyone is
  told who they are. A player whose Troop Leader has another version, or no
  mod, sits the mod out in that room and is told why.
- Only players who can pair count towards an even party.
- The F8 recovery key is gone; the Troop Leader uses the Roster instead.
- Revives are sent by the Troop Leader, so other mods no longer flag them.

### Fixed

- Hardened against modified clients: only the Troop Leader can change the
  Pairs, plans, rules, and reports, only you can change your own Role Preference
  and swap request, and forged or oversized data is dropped.
- A Rider warped onto their Carrier no longer pushes the two apart.
- Passport photos no longer turn black, and no longer show a default look
  before a player's own arrives.
- A player who left is named, not shown as a number.
- A Rider brought back on their Carrier leaves no remains in front of the view.
- A Pair's time together in the Run Report is no longer longer than the Run.
- A Rider whose Carrier leaves the room is no longer left marked as carried,
  which kept statuses off them until they were paired again.
- Paper pages, stamps, and the Scoutmaster's hand now make the game's own
  paper sounds.

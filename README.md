# BetterCoop

Climb PEAK in Pairs. One player carries the other for the whole Run, and the
two steer that one body together: the **Carrier** on the keyboard, the
**Rider** on the mouse. They share stamina and statuses, and they live, die,
and come back together.

[한국어](https://github.com/kawalain/peak-better-coop/blob/main/README.ko.md)

## How it plays

| Carrier (keyboard) | Rider (mouse) |
| --- | --- |
| Move, jump, sprint, crouch | Look and steer |
| Interact, pick up, drop, throw | Use the held item |
| Hotbar, emotes, open the backpack wheel | Scroll, ping, pick from the backpack wheel |

- The body walks where the Rider looks. To climb, the Rider holds the grip and
  the Carrier climbs.
- Both players see through the Carrier's eyes, with the same HUD.
- Tap the back-slot key to bring the back item to hand; hold it to open your
  own backpack, then the Rider clicks the item.
- Pairs change when a campfire is lit, and optionally on a timer. A Pair can
  also trade roles with **Swap Places** in the ESC menu. Items and statuses
  carry over; nothing is lost.

## Installing

Install with a mod manager (Gale, r2modman, or Thunderstore Mod Manager).
Everyone in the room needs the same version; the Airport kiosk names anyone
who does not have it. The party also needs an even number of players unless
the Troop Leader (host) allows an odd one.

## Settings

In your mod manager's config editor under `in.kawala.bettercoop`. The Troop
Leader's values apply to the room, except your own Role Preference and the
Debug settings.

| Setting | Default | |
| --- | --- | --- |
| Role Preference | Random | Carrier, Rider, or Random. |
| Rider Picks From Backpack | On | Off: the Carrier picks from the backpack wheel. |
| Allow Odd Player Count | Off | The extra player plays without a Pair. |
| Campfire Shuffle | On | New Pairs at each campfire. |
| Timed Shuffle, Interval | Off, 10 | New Pairs every few minutes (1–60). |
| Hand Over Place, Items | On, On | A new Carrier takes the old body's place and items. |
| Carry Weight | Off | Carriers feel the weight of a person. |
| Verbose Log | Off | Turn on when reporting a bug, and send `BepInEx/LogOutput.log`. |
| Recovery Key | On | The Troop Leader presses F8 to re-attach every Pair. |

## Compatibility

BetterCoop works next to other mods, including ones that change carrying;
while you are in a Pair, the Pair's rules win. It turns itself off beside
Co-op Mod, and after a game update it does not fit yet.

## Antivirus warnings

Some antivirus programs flag `BetterCoop.dll` with a name ending in `!ml`: a
machine-learning guess, common for mods because they change a game's code
while it runs. BetterCoop runs only inside PEAK, downloads and starts
nothing, and talks only to players in your room. Its source is at
<https://github.com/kawalain/peak-better-coop>. Report the file as a false
positive and restore it, or reinstall.

## Credits

- [Sappheiros009](https://github.com/Sappheiros009/CoopMod): the original
  idea, from Co-op Mod. BetterCoop began as a fork of Co-op Mod and has since
  been rewritten; none of Co-op Mod's code remains.
- [Puro](https://steamcommunity.com/profiles/76561198105109754): ideas and
  testing.
- [kawalain](https://steamcommunity.com/profiles/76561198436496102):
  maintainer.

## License

[MIT](https://github.com/kawalain/peak-better-coop/blob/main/LICENSE)

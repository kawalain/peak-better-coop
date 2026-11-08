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
  also trade roles with **Swap Places**. Items and statuses carry over;
  nothing is lost.
- **Roster** in the ESC menu lists the Pairs and the time to the next shuffle,
  and is where you swap places and set the role you would like.

## Installing

Install with a mod manager (Gale, r2modman, or Thunderstore Mod Manager).
Everyone in the room needs the same version; the Airport kiosk names anyone
who does not have it. The party also needs an even number of players unless
the Troop Leader (host) allows an odd one.

## Settings

In the **Roster** of the ESC menu, or in your mod manager's config editor
under `in.kawala.bettercoop`. The Troop Leader's values apply to the room,
except your own Role Preference and the Debug settings. The Troop Leader's
Roster can also shuffle the Pairs at once, swap any Pair's roles, pair two
chosen players, and re-carry every Pair.

| Setting | Default | |
| --- | --- | --- |
| Role Preference | Random | Carrier, Rider, or Random for either. |
| Rider Picks From Backpack | On | Off: the Carrier picks from the backpack wheel. |
| Allow Odd Player Count | Off | The extra player plays without a Pair. |
| Campfire Shuffle | On | New Pairs at each campfire. |
| Timed Shuffle, Interval | Off, 10 | New Pairs every few minutes (1–60). |
| Hand Over Place, Items | On, On | A new Carrier takes the old body's place and items. |
| Carry Weight | Off | Carriers feel the weight of a person. |
| Verbose Log | Off | Turn on when reporting a bug, and send `BepInEx/LogOutput.log`. |

## Compatibility

BetterCoop works next to other mods, including ones that change carrying;
while you are in a Pair, the Pair's rules win. It turns itself off beside
Co-op Mod, and after a game update it does not fit yet.

## For mod developers

`BetterCoop.Api` lets another mod read the Pairs without depending on
BetterCoop's internals: `Enabled`, `RoleOf(character)`,
`TryGetPartner(character, out partner)`, `TryGetSharedBody(character, out body)`
(a Rider plays through their Carrier's body), and the events `PairsChanged`,
`PairChangeStarting`, and `PairChangeFinished`. `ApiVersion` rises only when a
change breaks callers. A Rider's own character is idle while carried and its
statuses are kept clear, so a mod that shows players' status should show the
Shared Body instead.

Reference `BetterCoop.dll` as a soft dependency and keep the calls in a
method of their own, so your mod still loads without BetterCoop:

```csharp
[BepInDependency("in.kawala.bettercoop", BepInDependency.DependencyFlags.SoftDependency)]
public class MyPlugin : BaseUnityPlugin
{
    static bool HasBetterCoop => Chainloader.PluginInfos.ContainsKey("in.kawala.bettercoop");

    static bool IsRider(Character character) => HasBetterCoop && IsRiderInBetterCoop(character);

    [MethodImpl(MethodImplOptions.NoInlining)]
    static bool IsRiderInBetterCoop(Character character) =>
        BetterCoop.Api.RoleOf(character) == BetterCoop.Api.Role.Rider;
}
```

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

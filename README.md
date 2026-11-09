# BetterCoop

Two scouts, one body: the **Carrier** has the keyboard, the **Rider** has the
mouse, and they live and die together. Pray for a partner who knows the way,
and not one who eats every mushroom.

[한국어](https://github.com/kawalain/peak-better-coop/blob/main/README.ko.md)

![A Pair Shuffle letter counting down to a new partner](https://raw.githubusercontent.com/kawalain/peak-better-coop/main/docs/images/en-shuffle-letter.webp)

## How it plays

| Carrier (keyboard) | Rider (mouse) |
| --- | --- |
| Move, jump, sprint, crouch | Look and steer |
| Interact, pick up, drop, throw | Use the held item |
| Hotbar, emotes, open the backpack wheel | Scroll, ping, pick from the backpack wheel |

- The **Roster** in the ESC menu shows who carries whom, and the Run Report
  hands out Merit Badges to the steadiest back and the comfiest passenger.
- Only the Troop Leader (host) needs the mod. Anyone without it climbs on
  their own two feet.
- BetterCoop gets along with other mods, though its rules win while you are
  in a Pair. It steps aside for Co-op Mod, and sits out game updates it does
  not fit yet.

| Roster at the Airport | Roster during a Run | Run Report |
| --- | --- | --- |
| ![The Pair Plan on the Roster, with each scout's photo in album corners](https://raw.githubusercontent.com/kawalain/peak-better-coop/main/docs/images/en-airport-plan.webp) | ![Your Pair on the Roster: partner, role, Swap Places, and Role Preference](https://raw.githubusercontent.com/kawalain/peak-better-coop/main/docs/images/en-roster-run.webp) | ![The Run Report's Merit Badges and the Pairs by segment](https://raw.githubusercontent.com/kawalain/peak-better-coop/main/docs/images/en-report.webp) |

## Settings

The Troop Leader sets the Run Rules on the Roster, where each one is
explained: Honour Role Preferences, Allow Role Swaps, Late Pairing, Allow Odd
Player Count, Campfire Shuffle, Timed Shuffle, Hand Over Place and Items,
Rescue Carry, Carry Weight, and Rider Picks From Backpack. Each player sets
their own Role Preference there. If your Pair falls apart for reasons other
than your partner, turn on **Verbose Log** in your mod manager's config and
send `BepInEx/LogOutput.log` with your bug report.

![The Run Rules on the Roster, each with what it does](https://raw.githubusercontent.com/kawalain/peak-better-coop/main/docs/images/en-rules.webp)

## For mod developers

`BetterCoop.Api` lets another mod read the Pairs without depending on
BetterCoop's internals: `Enabled` (false while the mod is off, or sits out a
room whose Troop Leader's version does not work with this one), `RoleOf(character)`,
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

Some antivirus programs flag `BetterCoop.dll` by a machine-learning guess, a
name ending in `!ml`, as they often do with mods. Allow the file and install
the mod again. The source is open at
<https://github.com/kawalain/peak-better-coop>.

## Credits

- [Sappheiros009](https://github.com/Sappheiros009/CoopMod): Co-op Mod, the
  original idea that BetterCoop grew from.
- [Puro](https://steamcommunity.com/profiles/76561198105109754): ideas and
  testing.
- [kawalain](https://steamcommunity.com/profiles/76561198436496102):
  maintainer.

## License

[MIT](https://github.com/kawalain/peak-better-coop/blob/main/LICENSE)

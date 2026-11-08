# BetterCoop

BepInEx 5 plugin for PEAK (Unity 6, Photon PUN). Domain terms are in
`GLOSSARY.md`; use them in code, logs, and UI text. Decisions are in `docs/adr/`.

## Game code is the source of truth

Look up every game type, member, RPC name, and behaviour in `decompiled/`
before writing a patch. When it is missing or the game has updated
(`version.txt` in the game folder changed), regenerate it, with `M` set to
`<PeakGameDir>/PEAK_Data/Managed`:

```bash
ilspycmd -p -o decompiled/Assembly-CSharp -r "$M" "$M/Assembly-CSharp.dll"
```

`ilspycmd` is pinned to 9.1.x (`dotnet tool install -g ilspycmd --version 9.1.0.7988`)
because newer releases need a newer .NET SDK than the one this repo builds with.
`decompiled/` is git-ignored; it stays local for copyright reasons.

Prefer direct member access (Assembly-CSharp is publicized) over reflection
strings, so a game update surfaces as a compile error.

## Build and test

```bash
dotnet build -c Release
dotnet test
```

The game path defaults in `Directory.Build.props`; override `PeakGameDir` in the
git-ignored `Directory.Build.user.props`. Tests cover game-independent logic
only and must not touch Unity, Photon, or game types.

## Deploy and feedback

The development profile is a Gale profile whose data folder (Gale settings,
Locations) lies outside `%APPDATA%`: a packaged host (such as the Claude desktop
app) virtualises `%APPDATA%` for the tools it starts, so a build cannot reach a
profile there, nor read one reliably. Set `BetterCoopDeployDir` in
`Directory.Build.user.props` to
`<gale_data>/peak/profiles/<profile>/BepInEx/plugins/BetterCoop`, the mod's
folder laid out from the test build zip, and every build deploys there. When the
package changes, replace that folder directly rather than through Gale's GUI.
Start the game from Gale, or from here through Steam with that profile's
preloader:

```bash
python -I tools/dev_profile.py launch <gale_data>/peak/profiles/<profile> <PeakGameDir>
```

`launch` copies the doorstop proxy into the game folder, the only change made
there. Without Gale, `dev_profile.py install` lays out an equivalent folder
profile from Thunderstore. To test the Control Split alone, turn on `[Debug]
Solo Pair`: alone in a room, your mouse takes the Rider's path (encoded,
delayed by the set latency, replayed) before it reaches your body, and a solo
Run may depart. Diagnostics land in the profile's `BepInEx/LogOutput.log`;
players send theirs from their own Gale profile.

## Test builds and versions

Players must run the exact same version to depart, so bump `<Version>` in
`BetterCoop.csproj` only when a test build goes to players: minor for a build with
new features, patch for a build of fixes. Commits in between keep the version.
A Release build writes the test build to `artifacts/BetterCoop-<version>.zip`.

## Code layout

`src/BetterCoop/Core/` holds game-independent rules (pairing, the Control Split's
button replay and look smoothing, the Rider snapshot format) and is the only
code the tests reach. `Features/` holds one Harmony feature per file, named
after its glossary term; `Net/` holds Photon keys and presence; `Ui/` holds
notices and their Korean/English text. Every feature looks up Pairs through
`PairRoster` (ADR 0001), never through the game's carry state.

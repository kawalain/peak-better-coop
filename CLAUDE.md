# PEAK Co-op

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

Setting `PeakCoopDeployDir` in `Directory.Build.user.props` makes every build
copy the DLL into that folder. Point it only at the Gale development profile's
`BepInEx/plugins/PeakCoop` folder; the game folder itself stays untouched.
Play-test feedback comes back as the profile's `BepInEx/LogOutput.log`.

To set the profile up without the GUI, `gale --game peak --no-gui` creates the
`Default` profile, and `gale --game peak --profile Default --install <zip> --no-gui`
installs a local Thunderstore zip. Gale installs a zip as the mod loader only
when the manifest `name` is the full name `BepInEx-BepInExPack_PEAK`, so rewrite
that field in the downloaded BepInExPack_PEAK zip first; otherwise it lands in
`BepInEx/plugins` and the loader never starts.

## Test builds and versions

Players must run the exact same version to depart, so bump `<Version>` in
`PeakCoop.csproj` only when a test build goes to players: minor for a build with
new features, patch for a build of fixes. Commits in between keep the version.
A Release build writes the test build to `artifacts/PeakCoop-<version>.zip`.

## Code layout

`src/PeakCoop/Core/` holds game-independent rules (pairing, the Control Split's
button replay and look smoothing, the Rider snapshot format) and is the only
code the tests reach. `Features/` holds one Harmony feature per file, named
after its glossary term; `Net/` holds Photon keys and presence; `Ui/` holds
notices and their Korean/English text. Every feature looks up Pairs through
`PairRoster` (ADR 0001), never through the game's carry state.

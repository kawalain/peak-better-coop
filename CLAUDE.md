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

The development profile is a plain folder outside `%APPDATA%`: a packaged host
(such as the Claude desktop app) virtualises `%APPDATA%` for the tools it
starts, so a Gale profile created from here is invisible to the game that Steam
starts. `tools/dev_profile.py` builds and launches that folder profile:

```bash
python -I tools/dev_profile.py install <profile_dir> <cache_dir> PEAKModding-ModConfig
python -I tools/dev_profile.py launch <profile_dir> <PeakGameDir>
```

`install` adds BepInExPack_PEAK and ModConfig (in-game config editing) with
their dependencies. Setting `PeakCoopDeployDir` in `Directory.Build.user.props`
to `<profile_dir>/BepInEx/plugins/PeakCoop` makes every build deploy there.
`launch` copies the doorstop proxy into the game folder, the only change made
there. Diagnostics land in `<profile_dir>/BepInEx/LogOutput.log`; players use
Gale and send theirs from their own profile.

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

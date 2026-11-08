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

## Rewrite in progress

Files under `src/PeakCoop/Features/` that are listed as `<Compile Remove>` in
`PeakCoop.csproj` are the previous version, kept for history. Rewrite one
feature at a time against the current game, in place, in this order: party gate,
pairing, carry lock and camera, input relay, shared stamina/death/revival, Rider
HUD. Remove its `<Compile Remove>` line in the same commit.

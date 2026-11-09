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
profile from Thunderstore. With `[Debug] Verbose Log` on (off by default),
diagnostics land in the profile's `BepInEx/LogOutput.log`; players turn it on
and send theirs from their own Gale profile. Verbose Log is the only debug
setting a test build or a release has.

## Development tools

Development (Debug) builds add tools for trying the mod alone; a test build or
a release has none of them. All of them live in `src/BetterCoop/DevTools/`,
which only a Debug build compiles, and the plugin reaches them only through
the partial-method hooks in `DevHooks.cs`, whose calls the compiler drops
without `DevTools/`. Keep it that way: no `#if DEBUG` elsewhere, and a new tool
gets a hook rather than a reference from the plugin. Any other build fails if
the DLL still names something from `DevTools/` (`CheckNoDevTools` in the
project file).

- `[Debug] Solo Pair`: alone in a room, your mouse takes the Rider's path
  (encoded, delayed by the set latency, replayed) before it reaches your body,
  and a solo Run may depart. The loopback itself stays in the Control Split,
  run only while `DevHooks.SoloPair` is true.
- The Zoo, a pause menu button, shows every paper component in `Ui/Paper/` in
  its states. It also opens the Roster on a `FakeRoom` of any size, led by you
  or not, which changes its Pairs by the real rules without touching the
  config, the room, or the network. Add a new paper component to the Zoo along
  with it.
- Twins: `python -I tools/dev_profile.py twins <profile_dir> <PeakGameDir>
  [--plugin <BetterCoop.dll>] [--only host|guest] [--control]` copies the
  profile to `~/BetterCoopTwins/host` and `guest` and starts two copies of the
  game in small windows, apart from the player's own game: the host Twin opens
  a room without a Steam lobby and the guest Twin joins it by the room card
  `~/BetterCoopTwins/room.txt`. Test in Twins, never in the player's own game.
  Photon takes only Steam sign-in, so every copy shares one Steam account: the
  host Twin's room lets one account join more than once, and each Twin shows a
  scout id and name of its own. The mod knows players by `KnownPlayers.IdOf`,
  never by `Player.UserId`, so they stay apart. Twins leave the account and the
  player's files alone (no achievements, stats, rich presence, Steam lobby,
  quicksave, or saved settings), though Unity may still save a window size.
- The Control Line lets a tool watch and steer a running copy: its state (room,
  scouts, Pairs, Run Rules, Carry Record, Run Report), log, and screen; Roster
  orders through the Roster's own `LiveRoom`; warps; pressing what is on
  screen; and the game's console commands, except those that change what the
  account keeps. It is off unless asked for: `--control` on `dev_profile.py
  launch` or `twins`, `BETTERCOOP_CONTROL=1`, or `[Debug] Control Line`. A copy
  with it on listens on 127.0.0.1 with a token made at every start, announced
  in `~/.bettercoop/control/<pid>.json`. `tools/peak_mcp.py`, registered in
  `.mcp.json`, reads those files and exposes each copy (`host` or `guest` for
  a Twin, `player` otherwise) as MCP tools. A new command goes in
  `DevTools/ControlCommands.cs` (or `ControlRoom.cs` for the mod's own state),
  never anything that runs code or feeds raw input.

## Test builds and versions

Players must run the exact same version to depart, so bump `<Version>` in
`BetterCoop.csproj` only when a test build goes to players: minor for a build with
new features, patch for a build of fixes. Commits in between keep the version.
A Release build writes the test build to `artifacts/BetterCoop-<version>.zip`.

## Publish to Thunderstore

Publish only when the user asks for this version to go out; a build, commit,
or push does not. Thunderstore never accepts the same version twice, so a
published version cannot be fixed, only superseded by a bump. Build Release,
then check and publish that zip:

```bash
python -I tools/publish.py check
python -I tools/publish.py publish
```

`check` uploads nothing: it confirms the zip matches `<Version>`, the version is
newer than Thunderstore's latest, and a token is present. `publish` uploads with the tcli
pinned in `.config/dotnet-tools.json`; Thunderstore takes the team from
`thunderstore.toml` and everything else from the zip's `manifest.json`. The
token is the team's service account token, in `TCLI_AUTH_TOKEN` or the
git-ignored `.thunderstore-token`; the user provides it, and it never appears
in commands, logs, or replies.

## Code layout

`src/BetterCoop/Core/` holds game-independent rules (pairing, the Control Split's
button replay and look smoothing, the Rider snapshot format) and is, with
`DevTools/FakeRoom.cs`, the only code the tests reach. `Features/` holds one Harmony feature per file, named
after its glossary term; `Net/` holds Photon keys and presence; `Ui/` holds
notices and their Korean/English text. Every feature looks up Pairs through
`PairRoster` (ADR 0001), never through the game's carry state.

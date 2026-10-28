# Tug of War

A small Dalamud API 15 plugin for running the Tug of War dice game in FFXIV. It reads `/random 100` results from game chat, maps the roller to one of two independently sized team rosters, and tracks the score.

## Features

- Separate character lists for each team; team sizes do not need to match.
- Import the current party roster, assign members to either team, and apply the assignments while preserving names outside that party.
- One accepted roll per team per turn, from any listed member.
- The higher roll gains one point. The other team loses one point if its score is above zero.
- Ties clear both rolls and let the teams reroll without changing the score.
- The first team to 3 points wins.
- Optional Party, Say or Shout announcements for match start, resolved turns, ties, and the winner.
- Manual roll entry for a result the chat parser does not recognize.

## Use in game

1. Open the plugin with `/tugofwar`.
2. Set the team names and add one character name per roster line, or expand **Import current party**, refresh the party roster, assign each member to Team A, Team B, or Skip, and apply. Applying replaces team assignments for members in that party snapshot while preserving other roster names. Commas and semicolons also separate manually entered names. `Forename Surname@World` is accepted; world names are ignored when matching the roll sender.
3. Enable **Announce match and resolved scores** only if you want the plugin to post automatically, then select Party (default), Say, or Shout.
4. Choose **Start / reset match**. Players use `/random 100` in game. The first listed player from each team to roll is recorded for that turn.

The plugin ignores players who are not listed. If a team has already rolled that turn, another roll from that team is ignored. A score cannot go below zero.

## Build

Requires the local Dalamud API 15 SDK and .NET SDK used by that install.

```powershell
dotnet build .\TugOfWar.csproj
dotnet run --project .\tests\TugOfWarChecks.csproj
```

The plugin build writes to `dist/dev`. Add `dist/dev/TugOfWar.dll` in Dalamud Settings → Experimental → Dev Plugin Locations and scan, or use an existing registered location with automatic reload enabled.

## Install with Dalamud

In the Dalamud Plugin Installer (`/xlplugins`), open **Settings → Experimental → Custom Plugin Repositories** and add:

```text
https://raw.githubusercontent.com/Jacob5800/DalamudPlugins/main/repo.json
```

The repository feed and installable package are published automatically when a version tag is pushed. Use the four-part assembly version from `TugOfWar.json` for the tag (for example, `TugOfWar-v0.2.0.1`).

## License

The original project content is © 2026 Jacob5800 and is provided for viewing on GitHub only. See [LICENSE](LICENSE) for reuse restrictions. Third-party dependencies remain subject to their own licenses.

## Chat parsing

Rolls are matched by sender name and the `/random 100` result text. The expected maximum must be present and equal to 100, which prevents a smaller `/random` roll from scoring accidentally. If the game language or roll text differs, enter the result through the manual entry fields.

Party announcements are opt-in and sent using the game's party-chat command. Without that setting, scores are shown in the plugin window and tie/win results are also printed locally.

## Troubleshooting rolls

Expand **Diagnostics** and leave **Log roll diagnostics** enabled, start a match, then have both teams use `/random 100`. Choose **Copy diagnostics** after the failure. The latest 200 lines include roll chat text/player names, payload types, parser results and match decisions; unrelated chat is excluded. The same diagnostics appear in `/xllog`. Disable logging afterward if preferred. System rolls are identified using their linked player payload, since their chat sender can be empty.

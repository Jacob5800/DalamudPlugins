# Keybound Automation

Custom Tataru Brawler distribution, displayed as **Keybound Automation** in the installer. Open `/tatarubrawler` or click **KB** in the server info bar. Settings includes **Show button in the server info bar**, enabled by default.

The custom interface, English/Japanese typing, repeat queue controls and startup defaults are retained. Diagnostic recording, automatic typing and MapID 1359 play are enabled at startup; their settings checkboxes are hidden. Stop controls remain available.

Disable any other plugin controlling Keybound Brawler before enabling this one.

## Provenance

- The user supplied the custom build with the original owner's permission.
- Original source: https://github.com/Roxyz0501/KeyboundPilot (Roxyz0501).
- Custom modifications credited to Tataru-business; see LICENSE and NOTICE.
- Version 1.0.8.0 redistributed the supplied DLL byte-for-byte.
- Version 1.0.8.1 builds `src/`, reconstructed from that custom DLL using ILSpy 11.2.0.10050, with server-bar integration added. This is not a build of the newer original repository, which differs from the supplied custom build.
- Original archive is retained in `vendor/` for reference and rollback. SHA-256: `20A6FA784FF086DF349DE3C70AF0409A9D3855408D93D05EC84BA7E724E3A47C`.

## Build and package

Run `./plugins/TataruBrawler/Package.ps1` from the repository root with .NET 10 and Dalamud API 15 references available. `DalamudLibPath` may point to the SDK directory; it defaults to the local XIVLauncher dev folder. Output: `plugins/TataruBrawler/dist/package/latest.zip`.

The original custom source was not supplied. Reconstructed source compiled before and after the server-bar addition. Gameplay and native interaction still require in-game verification; compilation does not establish behavioral equivalence. InternalName remains `TataruBrawler` to preserve settings identity.

## Activity handling

While automated play is active in Keybound (territory 1359), the plugin resets the guarded Keybound input-agent inactivity counter and the existing general activity timers. It stops refreshing on pause/stop or outside that duty. No keys or movement are sent by this refresh. The input-agent layout is checked before resetting its counter. See `LICENSE-Saucy.md` for the adapted reset attribution.

## Reward tracking and input-blocking attacks

The Main tab shows completed games and observed MGP/MGC gains for the current plugin session and saved totals across characters on this installation. Saved totals live in `rewards.json` beside the plugin settings, with a previous-save backup. Each result is counted once; aborted visits do not count. A 15-second settlement window includes delayed currency updates on returning to the lobby. Currency caps and missing inventory baselines cannot be converted into theoretical rewards.

Cursed Gaze (49529) and Sensory Jammer (49532/49702) are detected through native battle-NPC cast IDs in territory 1359, independent of localized names. Typing pauses for the last half-second of the cast and the existing conservative four seconds after resolution. This does not change movement or registrar navigation.

## Background typing

English background input uses window-directed key messages to the current FFXIV process, after checking its dedicated input field, competing text focus and blocking casts. It does not activate the game window or send global key-down input into another application. Pending key releases are retained on stop/unload. New installs enable this option; existing checkbox choices are preserved. Japanese/IME prompts still wait for game focus. The game must continue updating in the background.

New duty entry explicitly starts play once. Stop remains effective for the current visit. Reward detection samples visible KTGResult before the repeat loop dismisses it, without relying on the extra agent-ID/container-loaded gates removed in 1.0.8.4. Loading screens do not establish currency baselines.

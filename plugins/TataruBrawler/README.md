# Keybound Automation

Custom Tataru Brawler distribution, displayed as **Keybound Automation** in the installer. Open `/tatarubrawler` or click **KB** in the server info bar. Settings includes **Show button in the server info bar**, enabled by default.

The custom interface, English/Japanese typing, repeat queue controls and startup defaults are retained. Diagnostic recording, automatic typing and MapID 1359 play are enabled at startup; their settings checkboxes are hidden. Stop controls remain available.

Disable any other plugin controlling Keybound Brawler before enabling this one.

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

## MGC cap warnings and punctuation

Settings provides **Warn when MGC approaches its cap** (on by default) and **MGC warning threshold (%)** (90 by default). The cap and balance are read from the game's CurrencyManager, without a hard-coded cap. A persistent Main-tab warning and a chat notification appear at the threshold, with an additional notification at the cap. Repeated samples do not repeat the chat alert. Spending below the threshold rearms it. These warnings do not stop play or make purchases.

**Skip optional English punctuation** is off by default. Enabling it skips question marks, exclamation marks, hyphens and straight/curly apostrophes in new English answers. Spaces, periods and commas remain. Japanese answers and native prompt identity are preserved. Turn it off if game acceptance differs; live shortened-answer acceptance still needs verification.

## Automatic recovery

Temporary input unavailability already pauses typing until ready. A SendFailed stop now retries after two seconds of continuously ready duty input, no more often than every five seconds. It retains the normal focus, cast-block and loading gates, including partial-answer cleanup during word preparation. Manual Pause/Stop, results, unsupported text and exhausted NoProgress cleanup never trigger a restart.

## Illegal mode

Settings → **Illegal mode (insert whole word)** is experimental and off by default. It inserts the prepared answer into the owned Keybound field and invokes its Enter callback. It checks prompt visibility, field ownership/readiness, existing text and competing focus. Existing manual pause/stop and cast blocks remain active. Submission is throttled to at least 1.5 seconds (or the longer configured between-word delay). It does not use the system clipboard or OS typing. Clear partial text before switching modes. Actual whole-answer acceptance has not been verified in game.

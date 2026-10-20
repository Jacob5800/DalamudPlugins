# Housing Timeout Reminder

Maintained fork of [thakyZ/HousingTimeoutReminder](https://github.com/thakyZ/HousingTimeoutReminder), updated to Dalamud API 15 and .NET 10.

The plugin reminds you to visit configured housing plots before the automatic demolition timer expires. The original AGPL-1.0 license and upstream attribution are retained.

## Use

1. Install Housing Timeout Reminder from [the custom Dalamud feed](https://raw.githubusercontent.com/Jacob5800/DalamudPlugins/main/repo.json).
2. Log into a character. Its owned personal house and FC estate are filled in automatically when the game provides their ownership data.
3. Open `/htimeout` and select **Current character** to see the district, ward, plot, and recorded visit. If an address is missing, open the game's Teleport list on your home world and click **Detect my housing**. Manual editing is available as a fallback.
4. Enter the estate to record an inside visit. The ward, yard, apartments, and FC private chambers do not count.
5. Log into each alt once with the plugin enabled. Their houses are stored separately under **Saved characters**, and reminders can appear while playing another character. The plugin cannot discover characters you have never logged into locally.

**Settings** controls how many days to wait before reminding you, and whether to show all saved characters. Automatic detection does not create a visit timestamp. A newly detected house says **Not recorded** until an inside visit is observed.

Use `/house` or `/htimeout` to open the plugin. In **Settings**, **Enable FC estate reminders** is on by default. Turn it off to hide FC reminders and FC settings across all saved characters; personal-house reminders and saved addresses remain available.

Apartments and FC private chambers are not tracked. Reminders use locally recorded visits rather than the server's demolition timer; previous visits made without this plugin, visits by other FC members, and server demolition suspensions are not automatically known. The in-game estate timer remains the authority.

An observed visit to your FC estate refreshes saved alts that share that exact FC estate on the same world. Personal-house visits remain character-specific.

FC reminders use your rank's **Estate Hall Access** permission, irrespective of custom rank names. Open the game's **FC Members** tab once on each character so the plugin can read that character's row and the loaded rank definitions. A known denied permission suppresses FC reminders; unknown or ambiguous data does not silently hide a house. The cached decision applies to saved alts too. Reopen the Members tab after changing ranks or rank permissions to refresh it.

The reminder window has an opaque background. Drag its title bar to move it; the position is saved when you release the mouse.

## Verification

Release and Debug builds are checked with the API 15 SDK. Run `dotnet run --project tests/HousingChecks/HousingChecks.csproj --configuration Release` after a Release build to validate housing address decoding, character identity, and inside-only visit rules.

Live ownership-data availability and the cleaned settings UI still require an in-game observation on a character with housing.

## Build

- Install the .NET 10 SDK.
- Install Dalamud API 15 through XIVLauncher, or provide its development files under `%APPDATA%\XIVLauncher\addon\Hooks\dev`.
- Build with `dotnet build HousingTimeoutReminder.sln --configuration Release`.

ECommons is restored from its published NuGet package. Its MIT license is included in [THIRD-PARTY-NOTICES-ECommons.txt](THIRD-PARTY-NOTICES-ECommons.txt).

## Source and releases

Canonical source now lives in [Jacob5800/DalamudPlugins](https://github.com/Jacob5800/DalamudPlugins/tree/main/plugins/HousingTimeoutReminder). Future release tags use `HousingTimeoutReminder-v<version>`. Existing standalone releases remain available for installed versions. The internal plugin name and saved configuration format are unchanged.

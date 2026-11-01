# Workshoppa API 15

Workshoppa version 9.3.13.5, built for Dalamud API 15 and FFXIV patch 7.57. This continuation includes a retainer depositor with item exclusions and automatically skips the short cutscenes between workshop production phases when the game exposes its skip prompt. The plugin listing is credited to Jacob5800, and the main window includes a direct Workshoppa Discord link.

## Credit and permission

Original plugin by Liza Carvelli.

## Install

1. Open Dalamud Settings and go to Experimental.
2. Add this custom repository URL: https://raw.githubusercontent.com/Jacob5800/DalamudPlugins/main/repo.json
3. Open the Plugin Installer, find Workshoppa, and install it.

## Commands

- /ws opens the plugin UI.
- /buy-tanks buys a requested number of ceruleum tank stacks.
- /fill-tanks fills inventory with a requested number of ceruleum tank stacks.

## Build from source

Open `Workshoppa.sln` with the Dalamud API 15 development SDK installed, or build `Workshoppa/Workshoppa.csproj` in Release configuration. Releases are published from the shared collection with Workshoppa-v<version> tags.

The workshop transition cutscene skip is armed only after choosing an advance or completion option. It opens and confirms the game's own skip dialog while that transition is pending.

## Airship and submarine voyages

Open /ws, then Voyages, and open the Voyage Control Panel main menu. Run voyages now makes one pass through enabled vessels. Collection and redeployment are enabled by default; automatic panel-start and repairs are off. Automatic panel-start performs one pass per visit. Stop prevents further actions; completed collections, repairs and departures remain applied. Travelling vessels are never recalled.

Use separate airship/submarine, collection and redeployment toggles. Under each vessel, disable inclusion or replace the previous voyage with a custom ordered route of one to five different destinations. Submarine points must share a map. The chart must acknowledge selected points and satisfy rank, availability, fuel and range checks. Unknown or invalid data stops the run without substituting a route. Vessel settings are scoped by workshop house ID, vessel type, slot and name and included in settings backups. Renaming or replacing a vessel creates a new identity.

Repair broken parts before redeploying uses carried Magitek repair materials for zero-condition components. With repairs disabled, a vessel needing repairs stops and notifies you. This does not buy supplies, change builds, register vessels, navigate to the panel, log into other characters or compute optimized routes. Missing resources, permissions or routes stop the run and notify through chat and status. Unload AutoRetainer before running voyages here. Crafting, shop purchases and retainer transfer must be stopped.

Native menu delivery, dialogs and departures still require in-game observation. Build and simulated workflow checks do not prove a successful live dispatch. First observe a manual pass before enabling automatic panel-start.

Native voyage details adapted from AutoRetainer (Puni.sh), BSD-3-Clause; see Workshoppa/AutoRetainer-LICENSE.md and Workshoppa/VOYAGE-REFERENCES.md. Original Workshoppa and LLib licensing/attribution are retained.

## Loot history and return alerts

Voyages contains Loot / profit history with per-collection loot, quantity, quality and NPC gross value, plus CSV export. Values use the game NPC sale price when observed; they are not realised sales or net profit and exclude fuel/repairs and market prices. Airship loot is valued as NQ because native quality flags are unverified. Records require an observed completed-voyage result followed by its collected timer transition; old or unseen collections cannot be reconstructed. History is stored separately as voyage-history.json in the plugin configuration folder.

Notify in chat when a vessel returns is enabled by default and can be toggled independently. Last observed vessel timers persist across restarts and notify once per voyage while logged in, even outside the workshop. Updates made by other players are unknown until this client observes fresh workshop data. This is not an out-of-game notification service.

## Route and build optimisation

Expand a vessel in Voyages, then Route / build optimisation. NPC value per hour is the submarine default; EXP per hour or voyage are alternatives. Select a map and duration cap, and optionally restrict parts to equipped/carried components. Search runs in the background. Save a recommendation to its per-vessel custom route and saved build. Automatic part installation is a separate default-off checkbox; saving a recommendation does not enable it. At the next redeployment the selected vessel must be idle and each changed part must be in carried inventory, legal for the rank/slot and within capacity. The parts picker and resulting installed row are verified between changes. Stop cancels remaining changes; already installed parts stay installed. It does not purchase or craft components.

NPC farming estimates use the bundled community submarine loot model, per-draw rates, retrieval quantities and NPC prices. These are gross estimates, exclude fuel/repair costs, and do not promise actual yields. Build farming recommendations search a shortlist of eight valuable sampled sectors on the chosen map, not every possible global route/build pair. EXP builds optimise the best current-build route. Estimates use offline distance/time models; actual chart fuel/range validation still gates dispatch. Airship NPC drop probabilities are unavailable, so its recommendations use EXP. Airship unlock information requires the selected vessel's open empty chart. Submarine unlocks come from live workshop data. Neither search ignores unlocks or permits arbitrary unavailable routes.

Optimiser calculation/model code adapted from Argus (ofnature), MIT, with its SubmarineTracker/community data attribution preserved in the packaged notices. Calculation tests do not validate native part-picker or dispatch behavior.

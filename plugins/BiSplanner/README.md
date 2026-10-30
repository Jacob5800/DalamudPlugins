# BiSplanner

BiSplanner is a modified fork of [BISBuddy by Lily / RajahOmen](https://github.com/RajahOmen/BisBuddy), included in Jacob5800's combined Dalamud plugin collection. Original credits and AGPL-3.0-or-later license are retained. KamiToolKit's MIT license is preserved separately.

## Local improvements

- Each gearset has a checkbox beside its name in the left-hand list. Checked means enabled for item tracking, assignment and highlights. Unchecked sets remain saved and can still be inspected or re-enabled. Changing the checkbox uses the original gearset-change/save mechanism.
- **Settings > General > Show BiSplanner in the server bar** adds a clickable shortcut. Disabled by default; clicking opens or closes the gearset window.
- Plugin windows, menus, commands, saved-data identity and IPC use **BiSplanner**. Commands: `/bisplanner` or `/bisp`; `/bisplanner config` opens settings.

The existing import, inventory synchronization and highlighting features are retained. See [upstream documentation](https://github.com/RajahOmen/BisBuddy) for the underlying workflow.

## Installation and existing sets

Install BiSplanner from this collection's custom repository. It uses its own settings and gearset directory and does not overwrite BISBuddy's data. Export/import existing sets using the existing JSON import options. Disable BISBuddy before using BiSplanner so both plugins do not modify the same game UI at once.

## Build and releases

Run `./plugins/BiSplanner/Package.ps1` from the repository root. It builds the plugin and exact vendored KamiToolKit revision, includes both licenses and provenance, stamps publication time, and creates plugin/source ZIPs under `plugins/BiSplanner/dist/package`.

Stable release tags: `BiSplanner-v<version>`. The release workflow publishes the package and corresponding source, updates the combined feed, and posts the changelog through the collection's existing Discord announcement script.

[Upstream revisions and local changes](UPSTREAM.md) · [BISBuddy license](LICENSE.md) · [KamiToolKit license](KamiToolKit/LICENSE)


## Recommended imports and fight filters

Open **Import**, choose your job and guide, then use **Find recommended sets**. Only published Xivgear/Etro set links are supported; spreadsheet-only recommendations are not imported. These are source recommendations, not measured popularity rankings. Requests are manual, limited to one per minute, and successful results are reused for ten minutes.

Icy Veins has no public gear-set API documented in the pages reviewed on 30 October 2026. Its FFXIV guide paths are allowed by robots.txt, but direct requests returned HTTP 403 during development. When a guide refuses access, open **Open source guide**, copy its desired Xivgear/Etro set link and use **Import pasted Xivgear / Etro link**. The plugin does not bypass refusals or retry automatically. Sources: [Icy Veins terms](https://www.icy-veins.com/terms), [robots.txt](https://www.icy-veins.com/robots.txt), [The Balance](https://www.thebalanceffxiv.com/).

Use **Fight / content** in a saved set's **Properties** to label existing sets. The selector above the Gearsets list filters by this label. Guide imports use the detected encounter or your selection. Identical builds are skipped by gear and materia contents, including repeated meld quantities and ignoring meld order; names alone never cause a set to be skipped.

Disabled sets can still be inspected, but are excluded from tracking, highlights and Materia-tab totals. Yellow filled circles indicate occupied but incorrect melds; hover to see actual and required materia. Empty missing melds stay unfilled.

Prerequisite purchase-cost lines come from the game's SpecialShop data and include mapped tomestones and directly represented currencies. They are informational and do not expand currency amounts into assignment nodes. Unsupported virtual currency mappings are omitted rather than guessed; not every vendor source is represented.


## Fight-matched Discord gear index (0.3.0.3)

The Balance choice now uses the bundled 30 October 2026 resource snapshot first. Select a job and fight, then Find recommended sets. Changing the fight filters the loaded results; it never converts current-raid builds into TOP/FRU gear. The index has 1,097 sheet-variant entries across 21 combat jobs plus BLU. Entries are not necessarily distinct builds: import-time duplicate checks compare actual gear and materia.

Link opens the selected gearset variant. Source opens its Discord resource post/channel. Sheet titles and metadata-check dates are shown so patch, Typical/Alternative and weapon variants remain distinguishable. Endwalker-history and SCH community entries are hidden by default with separate controls. Ambiguous fight assignments are Unspecified; nearby text mentioning an encounter alone does not establish an assignment. This is a dated capture of loaded posts, not a full-history export or a claim of mentor endorsement for community material.

Added DMU, AMR and AAI selectors. Shared fight labels such as AMR / AAI match either filter. All fights displays [fight] beside saved set names. Importing a duplicate build preserves the saved copy and merges the matched fight tag without enabling a disabled set. Runtime imports validate the selected job by class/job ID, including non-English clients.

The catalog contains gear/source URLs and set names, not copied Discord conversation text or authentication tokens. It runs without a Discord/Composio connection. Icy Veins remains a separate manual guide lookup with a pasted-link fallback if access is refused.

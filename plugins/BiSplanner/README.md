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

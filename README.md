# Jacob5800's Dalamud Plugins

A single custom Dalamud repository for three separately built API 15 plugins.

## Plugins

| Plugin | What it does | Project |
| --- | --- | --- |
| Portrait Atelier | Creates and applies curated portrait designs from the in-game Portrait Editor. | [Source](plugins/PortraitAtelier) |
| Strategy Board Library | Stores, searches, and imports FFXIV Strategy Board share codes. | [Source](plugins/StrategyBoardLibrary) |
| Retainer Pricer | Prices and manages retainer listings using Universalis. | [Source](plugins/RetainerPricer) |

Workshoppa-API15 remains in its [separate repository](https://github.com/Jacob5800/Workshoppa-API15) and is not included here.

## Install

Add this custom repository URL in Dalamud Settings → Experimental → Custom Plugin Repositories:

`https://raw.githubusercontent.com/Jacob5800/DalamudPlugins/main/repo.json`

Then install the plugins you want from the Plugin Installer. The feed lists each plugin separately; installing one does not install the others.

## Development

Each plugin has its own project, version, manifest, and build/release workflow under `plugins/`. They are packaged and released separately. The root `repo.json` combines their entries into one feed.

## Rights and attribution

Each plugin retains the rights notice from its original repository. There is no blanket license for this combined repository. See each plugin folder's `LICENSE` file and its README for project-specific terms and credits.

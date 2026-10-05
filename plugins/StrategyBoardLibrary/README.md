# Strategy Board Library

An API 15 Dalamud plugin prototype for keeping and searching a local collection of FFXIV Strategy Board share codes.

## Current features

- Search by board title, encounter, and tags; favorite boards for quick access.
- Keep the collection in a plugin-owned JSON file rather than using the game's limited saved-board slots.
- Add a pasted `[stgy:...]` share code or try a FFXIVStrats strategy URL. The URL importer only requests the exact `ffxivstrats.io` host and extracts a code if the page response contains one.
- Use **Read code or link from clipboard** to import a copied share code immediately or start fetching a copied FFXIVStrats link. Clipboard access only happens when that button is clicked.
- Copy a selected share code, then paste it into the game's Strategy Board Import screen.
- Export the local library as JSON and merge a JSON export from another machine.

## Import flow

1. Paste a FFXIVStrats strategy-page URL and choose **Try import from URL**. If its page response contains a share code, the plugin fills it in; otherwise copy the code from the site and paste it into the code box.
2. Add a title, encounter, and optional search tags, then choose **Add to library**.
3. Search/select the board and choose **Copy share code for in-game import**.
4. Open the game's Strategy Board Import UI and paste the code.

The game remains responsible for saving the board. This first version doesn't write game UI state or automate in-game clicks. That keeps import independent of unstable native UI internals.

## Catalog source limitation

The plugin searches its local library. FFXIVStrats currently labels itself beta, and I didn't find a documented public catalog feed. This prototype doesn't depend on undocumented search endpoints or scrape the whole site. A stable public catalog feed would allow a remote search provider to be added later without changing the local library or import flow.

## License

The original project content is © 2026 Jacob5800 and is not offered under an open source license. See [LICENSE](LICENSE) for the reuse restrictions. Third-party dependencies retain their own licenses. GitHub's terms grant users certain rights to view and fork public repositories; this notice does not technically prevent copying. Make the repository private if access to the source itself needs to be restricted.

## Build

Build with the locally installed Dalamud API 15 SDK:

```powershell
dotnet build .\StrategyBoardLibrary.csproj
```

The project writes Debug builds to `dist/dev` and Release packages to `dist/release`. Add the Debug output folder in Dalamud Settings → Experimental → Dev Plugin Locations, then scan dev plugins. The legacy `devPlugins` drop-folder is not the current developer workflow.

## Install through XIVLauncher / Dalamud

This plugin is distributed through the shared custom Dalamud repository. Add this repository URL in Dalamud Settings → Experimental → Custom Plugin Repositories:

```text
https://raw.githubusercontent.com/Jacob5800/DalamudPlugins/main/repo.json
```

Then open the Dalamud Plugin Installer, search for **Strategy Board Library**, and install it. This is a custom repository and is separate from the official Dalamud plugin list. The feed downloads `StrategyBoardLibrary.zip` from the repository's `main` branch. For each update, keep the project version, feed `AssemblyVersion`, and root ZIP in sync, then refresh the feed's `LastUpdate`. Pushing a `v*` tag also builds a GitHub release with an installable ZIP attached.

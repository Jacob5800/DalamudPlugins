# Portrait Atelier

Portrait Atelier creates portrait variations using lighting, framing, unlocked poses and expressions, and owned backgrounds, frames and decorations. Two switches let you keep the current character pose/expression or banner design.

## In-game workflow

1. Open the game's Portrait Editor and wait for the character preview to load.
2. For a hands-off starting point, click **Make a portrait for me**. It captures the current editor, selects the next curated look and applies it in one action.
3. Or click **Capture current portrait**, search the featured looks, set the style strength and generate a variation. A compatible Portrait Helper code can also be pasted as a base.
4. Review the result in the game's editor. If you want to keep it, use the game's own save button.

Capture and apply read or change the open portrait editor only after the user presses the corresponding button. Apply changes the editor preview; it never presses the game's save button. The plugin never sends portrait data to a server. It does not collect usage statistics or character data. Copy code creates a portable version 1 Portrait Helper preset string for sharing or importing with HaselTweaks.

The editor bridge uses version-sensitive FFXIV client structures. Patch changes can require plugin updates. If an imported code's frame or decoration is unavailable on the current character, the plugin keeps the current selections and still applies the camera and lighting. Preview changes have been checked in one live editor session; final saving and broader character coverage remain unverified.

## Featured looks

The starter gallery includes community-inspired Seamless Showcase and Weapon Showcase looks, alongside Golden Hour, Moonlit, Soft Studio, Rose Bloom, Ember Drama, Arcane Violet, Verdant Glow, and Hero Frame. These are original style directions inspired by recurring presentation ideas; they are not copied portraits or a measured popularity ranking. **Make a portrait for me** cycles through the built-in looks. Use **Refresh styles** to fetch the public catalog when online; refreshing is manual.

The automatic action works only while the Portrait Editor is open and its character has loaded. It uses full style strength. Browsing successive looks reuses the original composition to avoid accumulating camera changes; manual edits become a new starting point. Pose choices come from the current job's unlocked list; expressions and banner assets come from the editor's unlocked lists. Character choices prefer names associated with the style when available, and otherwise cycle through available options. Owned banner combinations vary by style and take. Changing a generated pose resets the camera to native default framing. Disable either selection switch to retain those current settings. Imported codes applied as-is retain their own framing. Final saving remains manual.

Before reporting success, the plugin reads back the lighting settings. If those settings were not accepted, it restores the previous preview and displays an error. The exported code contains the actual editor result. Native framing validity updates after rendering, so the game's Save status determines whether the preview can be saved; adjust framing in the editor if needed. The plugin remembers whether its window was open across reloads.

## Contributing looks

See [CONTRIBUTING.md](CONTRIBUTING.md) for the catalog schema and submission guidance. A look is a set of lighting and camera-distance targets, not a copied player portrait or image.

## Preset code compatibility

The encoder reads and writes version 1 `HTPS` portrait helper codes. It preserves fields it does not deliberately style. Unsupported versions and malformed input are rejected.

## Build

Requires the Dalamud API 15 development SDK and .NET 10. Build from the repository root with the Dalamud API 15 development SDK installed: `dotnet build .\plugins\PortraitAtelier\PortraitAtelier.csproj --configuration Release`. GitHub Actions builds against the public Dalamud staging SDK and creates release zips from version tags.

Run the standalone data checks with `dotnet run --project .\plugins\PortraitAtelier\checks\PortraitAtelier.Checks.csproj --configuration Release`. These use sample data without accessing FFXIV and cover style changes, field preservation, share-code round trips, invalid parameters and repeated automatic styling. Native editor behavior requires an in-game check too.

## Installation

Add this custom repository URL in Dalamud's Experimental settings:

`https://raw.githubusercontent.com/Jacob5800/DalamudPlugins/main/repo.json`

Then install **Portrait Atelier** through the plugin installer. The feed points to the latest published release.

## License

All rights reserved. See [LICENSE](LICENSE).

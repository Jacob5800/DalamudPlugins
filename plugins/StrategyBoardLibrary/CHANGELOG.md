## 0.1.2.3

- Improve disable/update cleanup: stop callbacks first, attempt remaining cleanup after an error, and ignore repeated shutdown calls.
- Cancel pending strategy-page downloads when the plugin is disabled.

## 0.1.2.2

- Fixed FFXIVStrats URL imports by reading the site's strategy API instead of searching the initial HTML. Strategy page titles and share codes are retrieved directly.
- Added folders and encounter filters, plus Recently used first sorting. Copying a board's code records its use.
- Duplicate-code messages identify the saved board; JSON imports report duplicate counts.
- Added library file export and merge import, preserving folders, favorites and recent-use dates. A timestamped backup of the current library is saved before importing.

## 0.1.2.1

- Replaced the generic gamepad on the Discord join button with a recognizable Discord-style symbol, linked to the community invite.

# Strategy Board Library changelog

## 0.1.2.0
- Updated compatibility for FFXIV patch 7.57.

## 0.1.1.0
- Added a Discord shortcut at the bottom of the plugin window.
- Added a custom strategy-board icon.

## 0.1.0.0
- First release of a searchable local library for FFXIV Strategy Board share codes.
- Import copied share codes or try importing from FFXIVStrats links, then copy a selected code for the game's import screen.
- Favorite boards and export or merge libraries as JSON.

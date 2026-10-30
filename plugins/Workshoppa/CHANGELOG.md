## 9.3.13.4

- Improve disable/update cleanup: stop callbacks first, attempt remaining cleanup after an error, and ignore repeated shutdown calls.
- Keep attempting transfer cancellation and external-plugin setting restoration if another cleanup step fails.

## 9.3.13.3

- Added a Settings tab inside the /ws menu with the existing Workshoppa options and settings backup controls.

## 9.3.13.2

- Added an optional WS server-bar button, disabled by default. Enable Show WS server-bar button in Workshoppa settings; clicking it opens the Workshoppa UI. The setting is included in settings backups.

## 9.3.13.1

- Retainer Deposit now opens manually by default. Enable Automatically open Retainer Deposit in settings to restore automatic opening.
- Added Add all inventory items to keep list, with clear text that those item types (HQ and NQ) will not be deposited. Refreshing the picker remains separate.
- Added settings export/import for the keep list, presets, queued projects and calculator settings. Import merges the keep list, replaces presets/queue/settings and saves a safety backup first. Live crafting progress is excluded.
- The Discord join button now uses a recognizable Discord-style symbol. Added a workshop-themed plugin icon.

# Workshoppa changelog

## 9.3.13.0
- Workshoppa is now available through the shared Jacob5800 plugin collection.

## 9.3.12.0
- Updated compatibility for FFXIV patch 7.57.
- Added a Discord shortcut to the main window.

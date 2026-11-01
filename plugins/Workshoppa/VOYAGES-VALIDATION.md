# Voyage implementation and validation

## Completed implementation

- Added the Voyages tab, one-run/manual start and stop, optional once-per-panel-visit automatic start, separate airship/submarine/collection/redeployment toggles, optional broken-part repairs, per-vessel inclusion and previous/custom ordered route settings.
- Routes use one to five unique points, one submarine map, rank checks, native chart row availability, acknowledged selection, fuel/range validation and selected-vessel identity gates. Settings key includes workshop house ID, type, slot and vessel name.
- Runs leave travelling vessels alone and do not recall, register, outfit, purchase, move, relog or optimize routes. AutoRetainer being loaded and other Workshoppa activity block voyage automation.
- A bounded 250 ms controller has distinct request/wait states, one-shot collection/repair/deployment requests, game-state completion checks, deadlines, character/workshop checks and owned repair-confirmation matching.
- Settings persist in plugin configuration and export/import; imports are blocked during voyage/crafting activity and retain the live options object. BSD-3-Clause AutoRetainer license and provenance are packaged alongside original Workshoppa/LLib attribution.

## Checks

- Release build against API 15 succeeded. Existing analyzer coupling/disposal warnings remain; no build errors.
- 36 isolated model/state-machine checks: completed/idle/away/disabled eligibility, both vessel types, collect-only, repairs on/off, absent fuel/previous route, delayed collection exactly once, selected-vessel/workshop change, custom route order, map/rank/duplicate/empty validation, automatic visit exactly once, manual stop and settings round trips/import isolation.
- Native event shapes and layouts checked against AutoRetainer revision cc020df0efee3ea65a45117e2b89999108c702e8, FFXIVClientStructs and ECommons AtkReader. Ship/sub dialogue strings checked against current game sheets.

## Pending native observation

- Panel-to-picker menu transitions, timer and component inventory readiness, result collection and route repetition on an actual completed vessel.
- Repair request, exact confirmation and material/condition changes.
- Submarine map selection and point-count acknowledgement; airship point selection and range change acknowledgement; fuel/range failure behavior.
- Actual dispatch completion/cutscene transitions, GUI layout and automatic once-per-visit start. Simulated tests do not establish native gameplay success.


## 9.3.13.6

- Saved observed reward records and fleet return timestamps, deduplicated notifications, CSV and NPC gross-value totals. No realised-sales/net-profit claim. Airship quality valued as NQ.
- Integrated Argus MIT models with local game sheets and community loot data for route search and build recommendations, explicit shortlist/model limitations, owned-part filter, duration/fuel constraints and manual saving of a chosen plan.
- Saved-build swapping is default-off, uses native component menu/picker flow and verifies actual installed parts before advancing. It verifies identity, idle status, rank, capacity, slot, inventory availability and deadlines; it performs no purchasing.
- Release build succeeded. 57 isolated checks pass, including 45 workflow/history checks and 12 optimiser checks using a game-data fixture: routes, fuel/unlocks, unavailable required sectors, embedded loot data, no airship gil fabrication, build rank/capacity/range, owned parts and cancellation.
- Pending native: loot capture/collection transition, alert presentation, optimiser UI, live selected-map data, part swap request/picker/installed verification and dispatch after swapping. These are not passed game tests.

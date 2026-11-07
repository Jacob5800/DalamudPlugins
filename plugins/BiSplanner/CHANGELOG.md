## 0.3.0.6

- Automatically recognize owned alternatives for supported item-level-synced XivGear gear when importing a set. Reimport existing sets to pick up alternatives.

## 0.3.0.5

- Show currency icons and green/red purchase costs according to your available balance, with owned and required amounts on hover.
- Label currency purchase options with the item they buy to distinguish base gear costs from upgrade materials.
- Show known source instances beneath unowned gear and prerequisites.
- Show owned item locations in inventory, armoury chest and equipped gear, plus last-seen saddlebag and retainer snapshots refreshed when their inventories are opened. Remote snapshots are saved separately for each character.

## 0.3.0.4

- Improve disable/update cleanup: stop callbacks first, attempt remaining cleanup after an error, and ignore repeated shutdown calls.
- Fix the inventory event handler remaining attached after disabling the plugin, guard pending refreshes, and stop queued actions during shutdown.

## 0.3.0.3

- Fix the Import fight selector: TOP, FRU and other choices now filter matching builds instead of relabelling current-raid gear.
- Add a metadata-checked Balance Discord gear index for all combat jobs and Blue Mage, including the supplied BLM, BRD and RPR links. Individual sheet variants keep their set indexes and source links.
- Add separate controls for historical Endwalker sets and community submissions, plus set/GCD search. Ambiguous fight assignments remain Unspecified.
- Add DMU, AMR and AAI fight filters, including shared AMR/AAI recommendations.
- Display [fight] beside saved set names when viewing All fights.
- When a duplicate build already exists, retain it and add the imported recommendation's fight tag.
- Validate the provider job before saving recommended imports and accept Xivgear's www/path-style links.

## 0.3.0.2

- Add an Import tab for guide-recommended sets: The Balance lookup, Icy Veins guide lookup where accessible, and direct Xivgear/Etro set-link import. Clearly explain refused lookups and provide a manual-link fallback.
- Add a fight filter above the gearset list. Assign existing sets to TOP, FRU, DSR and other content in Properties; guide imports can assign the fight automatically or use your selection.
- Show tomestone and other supported exchange currency costs beneath unowned prerequisite gear, using the game's shop data.
- Exclude disabled sets from materia totals, selection and overlap warnings.
- Fill incorrect meld circles yellow, including melds on owned upgrade precursor gear. Matching materia still count in any order.
- Check import capacity after removing duplicate builds.

## 0.3.0.1

- Skip duplicate imported builds by gear and materia contents, regardless of name or meld order.
- Keep different builds with the same name using numbered suffixes.
- Highlight incorrect melds in yellow and explain the actual and required materia in a tooltip. Matching materia are accepted in any order.

## 0.3.0.0

- Introduce BiSplanner, based on BISBuddy by Lily / RajahOmen.
- Add an enable/disable checkbox beside every gearset. Disabled sets stay saved and are excluded from tracking and highlights.
- Add Show BiSplanner in the server bar, disabled by default. Click the entry to open your sets.
- Update plugin windows, commands and menus to BiSplanner. Open with /bisplanner or /bisp.

# TODO

## 0.4.35.0 — Remember price-drop choices per run

Implemented:

- Keep approve or ignore decisions keyed by item ID for the active Update existing listings or Auto update run, across Auto update's retainer traversal.
- Recheck and apply later listings for an approved item; leave later listings unchanged for an ignored item. Clear decisions on completion, cancellation, or a new run.
- Update the review prompt, help, README, manifest, and changelog.

Pending in-game observation:

- Confirm approving or ignoring a large drop for an item applies the same choice to later retainers in the same Auto update run, and that the next run asks again.

## 0.4.34.0 — Region scope server labels

Implemented:

- Region scope now labels each server as `World (Data Center)`, for example `Alpha (Light)`. World and Data Center scopes keep their existing server labels.

Pending in-game observation:

- Confirm Region scope displays the correct Data Center for each deal while World and Data Center scopes show plain server names.
## 0.4.33.0 (testing) — Progressive Sniper deals

Implemented:

- Start the Universalis live feed alongside the initial history scan.
- Buffer listing additions for items whose history batch is not ready yet, then evaluate them when its HQ/NQ baselines are available. New 1-gil alerts appear immediately.
- Keep the live-listing buffer bounded and expose progress while deals appear during the scan.
- Update Sniper status, help, README, manifest, and changelog.

Pending in-game observation:

- Confirm a new 1-gil listing appears while the history scan is running.
- Confirm a normal listing is evaluated and shown after its item's history batch completes, before the full catalog scan ends.
- Confirm listing removals clear pending entries/deals, reconnects keep the history scan progressing, and Stop cancels both.

## 0.4.32.0 (testing) — Home-region Sniper scope and automatic venture options

Implemented:

- Sniper Region scope now queries only Data Centers in the home world's region. Oceania is included only for a home world in Oceania; it is not appended to other regions.
- Venture choices are generated from the game's task data and each retainer's current job and level. Gathering tasks remain gated by the Gathering Log, with the unlock cache refreshing once a minute while the Ventures tab is open.
- Replaced open-menu capture with a single refresh action that refreshes all loaded retainers without navigating their venture menus.
- The refresh result is shown next to its button, including the count of available options or a message explaining why refresh could not run.

Pending in-game observation:

- Confirm Region scope for a non-Oceania home world scans only its own region, while an Oceania home world still scans Oceania.
- Confirm the automatically generated venture lists match retainer job, level, and Gathering Log unlocks; refresh after an unlock and confirm assignment reaches the intended task.
- Confirm a temporary Universalis history failure retries once, and persistent failure status reports its cause in the home-region scope.

## 0.4.31.0 — Sniper history resilience

Implemented:

- Retry a Sniper history request once after transient network failures, timeouts, HTTP 408/429, or server errors. Explicit user cancellation remains immediate.
- Show the first failed history batch reason in the running status so persistent Universalis failures are diagnosable.
- Updated the Sniper help, README, manifest, and release changelog.

Pending in-game observation:

- Rechecked with the 0.4.32 home-region scope candidate above.

## 0.4.30.0 (testing) — Per-retainer venture selection

Implemented:

- Added per-retainer venture selection, defaulting to Quick Exploration, and task assignment through the game's venture menu.
- Captured venture task IDs from the open in-game venture list and checked Miner, Botanist, and Fisher items against the player's Gathering Log before saving or displaying them.
- Updated help, README, plugin manifest, and release changelog.

Pending in-game observation:

- Confirm Quick Exploration remains the default, selected tasks are assigned to the correct retainer, and completed/ongoing venture handling is unchanged.
- Stop the cycle during a retainer transition and confirm it leaves the current game window open without selecting another retainer.

## v0.4.8 — Saved gear-set protection

Implemented:

- Automatically protect items used by saved gear sets from automatic listing, existing-listing repricing, Auto update, and Auto vendor. The protection refreshes from the current character's saved gear sets; automation pauses if that data is unavailable.
- Updated the `?` help, README, plugin manifest, and release changelog.

Pending in-game observation:

- Verify a saved gear-set item is skipped by automatic listing, repricing, Auto update, and Auto vendor, and becomes eligible after removing it from all gear sets unless it remains in Exceptions.

## v0.4.7 — Retainer greeting, vendor action, and Sniper wording

Implemented:

- Auto update advances the greeting after selecting a retainer even while the game's selected-retainer ID is temporarily unset or still reports the previously selected retainer. It retries while the greeting remains open, while rejecting any other resolved retainer ID.
- Auto vendor uses the game's vendor sale action for a revalidated inventory slot, then confirms the expected stack was removed before continuing.
- Sniper shows the deal threshold as a percentage of median (91.0% default) instead of a decimal multiplier.
- Updated the `?` help, README, plugin manifest, and changelog for the new behavior.

Pending in-game observation:

- Confirm Auto update advances the “I have come, Master” greeting and continues into the retainer options and listing screens.
- Confirm Auto vendor sells a qualifying stack and proceeds to the next candidate after inventory verification.

## v0.4.6 — Inventory binding and vendor menu handling

Implemented:

- Automatically omit spiritbound equipment from market-listing inventory snapshots and recheck binding before opening or repricing a sale window.
- Safely close an already-open item menu owned by the active vendor before continuing.
- Add `/retainer` as an alternate command for opening the plugin.
- Update the `?` help, README, plugin manifest, and changelog for the new behavior.

Pending in-game observation:

- Confirm spiritbound gear is omitted from automatic listing while unbound copies remain eligible.
- Confirm Auto vendor safely handles a vendor-owned context menu before continuing.
- Confirm `/retainer` opens the plugin without conflicting with another installed command.

## v0.4.3 — Sniper batch pacing and large-drop review

Implemented:

- Sniper history queries send up to 100 item IDs in each Universalis request and space consecutive history batches at least one second apart.
- Added a 3–14-day selectable history window and updated the Sniper UI, help, README, manifest, and repo feed changelog.
- Existing-listing and Auto update runs hold proposed reprices more than 50% below the current listing price; after each retainer scan, the user can approve a fresh price check or ignore the item.
- Added standalone coverage for 100-item query construction, response parsing, and one-second batch spacing.
- A live read-only 100-ID request to Alpha (world 402) returned the batch response shape and all 100 submitted IDs; the sample included 82 unresolved IDs, so it validates the batch endpoint shape, not 100 live market histories.

Pending in-game observation:

- Confirm normal repricing continues past a held large-drop item, the popup offers both choices, and approval fetches a fresh quote before applying.
- Confirm Auto update pauses for the review before it navigates to the next retainer.
- Confirm Sniper scans real marketable items with at least one second between successive 100-item batches, then opens the live feed.

## v0.4.1 — Per-item repricing protection

Implemented:

- Added a persistent `Don't reprice` list separate from global exceptions.
- Items on the new list are omitted from existing-listing repricing and Auto update, but remain eligible for new listings.
- Added quick protection actions beside captured and reviewed existing listings.
- Updated the `?` help, README, plugin manifest, and repository feed changelog.

Pending in-game observation:

- Confirm a protected item is skipped by both repricing actions while an unprotected item still updates normally.
- Confirm new listing automation still accepts an item that is only on `Don't reprice`.

## v0.4.0 — Universalis Sniper

Implemented:

- Replaced the hand-maintained watchlist with a scan of every marketable item on the home world.
- Batches up to 100 item histories in one Universalis request, with at least one second between Sniper history-batch requests.
- Added a configurable 3–14-day sales window and calculates separate HQ/NQ median unit prices using up to 1,800 recent sales per item.
- Flags new 1-gil listings across marketable items for manual review; the plugin never purchases automatically.
- Updated the Sniper UI, `?` help tab, README, plugin manifest, and repository feed changelog.

Pending in-game observation:

- Confirm the WebSocket connects and detects a newly listed watched item on the home world; confirm listing removals clear corresponding deal rows.
- Confirm the history baseline and HQ/NQ filters with an item that has recent sales of both qualities.

## v0.3.1 — Auto update from the picker

Implemented:

- Auto update now starts either from the retainer picker in its displayed top-to-bottom order, or from an open selling list with that retainer first.
- The retainer menu Quit match tolerates trailing punctuation and waits for the picker to fully return before advancing.
- Picker rows are matched against the owned-retainer roster and availability flag before selecting; unavailable retainers are skipped.
- New listings cap each sale at 99 and verify/continue the remainder from the same inventory stack.
- Removed the extra fixed pauses after repricing, listing, and retainer-menu actions; the 250 ms state polling remains.
- Updated the in-plugin ? help, README, manifest changelog, and release reminder for help maintenance.

Pending in-game observation:

- Replay Auto update starting from the Retainer selection screen and verify it processes several retainers in visible top-to-bottom order.
- Replay with a carried stack above 99 and confirm it creates 99-item listings followed by the final remainder, subject to free market slots.

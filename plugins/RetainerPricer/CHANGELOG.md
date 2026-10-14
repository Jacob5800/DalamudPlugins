## 0.5.1.10

- Shifted retainer and Markets header shortcuts right while preserving space for close controls.
- Removed the redundant On-screen price check source selector.

- Marketboard ROI labels now sit inside Search Results, to the right of retainer names and before the scrollbar.

## 0.5.1.9

- Fixed marketboard ROI being hidden when the native loading flag remains set after results are displayed. Visible listing rows are checked directly.

- Added Retainer vendor items beside Start listing items in the Markets header, using the existing retainer-sales lists and protections. Shifted the picker header shortcuts slightly right.

- Hovering marketable items retries incomplete tooltip data at most once per minute per item while keeping available cached values visible. HQ data is only required for items that support HQ.

- Retainer sales skip items with no NPC selling price, including unsellable starter equipment, even when whitelisted or Universalis reports a low market price.

## 0.5.1.8

- Added Start listing items to the top-right of the retainer Markets window, using existing listing confirmations and protections.

- Moved Start venture cycle into the retainer menu header and added Update existing listings immediately to its left.

## 0.5.1.7

- Tooltip prices are orange with a gil symbol; sales/day amounts are orange without a symbol. Ages are gray below 7 days, yellow at 7 days, orange at 8–30 days, and red above 30 days.
- Added a yellow Start venture cycle shortcut to the top of the game retainer menu, beside the Venture heading.

## 0.5.1.5

- Venture readiness uses native retainer availability and the server clock; live venture timers take priority over old lookup snapshots. Tooltip explains live versus last-observed data.
- Marketboard ROI reads visible rows through the native list API and shows diagnostic status in Settings.

## 0.5.1.4

- Home prices now appear inside the native item tooltip instead of a separate panel.
- Tooltip prices, recent purchases and sales/day use one cached aggregate request per batch, with up to 50 items and a 90-minute local cache.
- Prefetches marketable items in loaded inventory, saddlebags and retainer inventory before hovering.

## 0.5.1.3

- Warns when Price Insight is loaded, since enabling both tooltip price displays may show duplicate panels.

- Optional marketboard ROI column follows visible search-result rows, comparing each offer against matching home-world HQ/NQ prices with configured fees.

- Optional item-tooltip panel shows home-world NQ/HQ listings, latest purchases, ages, and cached four-day sales/day. Enable Show home prices in item tooltips in Settings.

- Added Copy price beside the suggested lookup price, copying a plain gil amount without separators.

- Failed price lookups now offer Retry on the Retrieved price tab, preserving the item and HQ/NQ quality.

- Listing items with melded materia now pauses for a Proceed or Skip warning. Normal pricing does not match materia and may compare against unmelded items. Batch listing continues past skipped items. An optional Don't ask again during current listing checkbox suppresses further warnings after Proceed for that batch only.

## 0.5.1.2

- Retrieved price shows estimated ROI beside Cheapest and Home marketboard prices, based on resale on your home world with the configured Sniper fee assumptions. Hover ROI for its calculation.
- Sniper item selection now says Add item(s) to watch.

# Retainer Pricer changelog

## 0.5.1.1

- Search on Universalis from marketboard result menus now reads the selected listing's HQ/NQ flag, so an HQ offer retrieves matching HQ prices instead of defaulting to NQ.
- The selected listing index is checked against the loaded result count and item ID before its quality is used. Your configured Pricing scope still determines the lookup market.

## 0.5.1.0

- Added checkboxes to Buy list offers, with Select all offers and Clear selection. Totals cover selected whole stacks across all pages and show estimated purchase cost including assumed fees and estimated resale profit after tax. Unknown-profit offers are counted separately and excluded from the profit total; their purchase cost is included.
- Added Search this offer to Buy list right-click menus alongside the existing clipboard search. It copies the clicked item's name and searches its item ID directly. You must stand at a marketboard with its menu open and finish active pricing operations first. Searches use the current world; purchases remain manual.
- Updated the version series to 0.5.1.0.

- Adjusted Search on Universalis context-menu handling to recognize both Item Search and Search Results and read the active item from the search agent first. This targets the missing option reported in-game; runtime confirmation is still needed.

## 0.5.0.38

- Added Open buy list above Paste clipboard and search on the marketboard overlay. It opens Retainer Pricer directly on the Buy list tab.
- Buy list offers now have an X button beside their item name. X asks for confirmation and removes only that offer. Right-click Remove item from buy list remains immediate and also removes only that offer.
- Added Search on Universalis to context menus on marketboard result items. It opens Price lookup on Retrieved price and retrieves the selected item through the existing read-only lookup, using your Pricing scope settings. Purchases and listing prices are not changed.

## 0.5.0.37

- Remove item from buy list now removes only the clicked offer row. Other saved offers for the same item remain intact.
- Add to buy list continues to move all currently captured Sniper offers with that item name, preserving each offer's data. The item stays suppressed in Sniper while any of its rows remain in Buy list.

## 0.5.0.36

- Added a Paste clipboard and search button directly above the game's open Item Search marketboard menu. It follows the menu position and disappears when the menu closes; opening Buy list is no longer required.
- The button resolves an exact marketable item name from your clipboard and uses the existing marketboard search helper on your current world. It is disabled while pricing is busy. Invalid clipboard text displays an explanation. Purchases remain manual.

## 0.5.0.35

- Sniper and Buy list now place rows with known negative estimated profit or ROI at the bottom by default, before pagination. Unknown-history rows are not classified as losses.
- Clicking a column header still applies your selected ascending/descending sort. Headers support clearing the sort to restore the default loss-last order.
- Updated default table layouts so old saved sorts do not prevent the new default ordering from appearing.

## 0.5.0.34

- ROI now appears directly before Discount in Sniper and Buy list, with corrected sorting and default layouts.
- ROI is red below 0%, light green from 25% to under 100%, and green at 100% or more. Values from 0% to under 25% remain neutral. Tooltips explain the estimated return after configured fees and that actual resale can differ.
- Added Paste clipboard and search to Buy list item-name right-click menus. With a marketboard open and pricing idle, an exact marketable item name from the clipboard is resolved and searched using the existing game search helper. Use Copy item name first. Search uses the current world's marketboard; purchases remain manual.

## 0.5.0.33

- When Skip 1 gil lookup is enabled, live 1-gil alerts now require verified matching-quality history meeting your minimum sales before appearing. Failed verification remains pending for retry. With the option off, confirmed startup and live 1-gil alerts remain immediate.
- Added a sortable ROI column: estimated resale profit divided by purchase cost including assumed fees, expressed as a percentage. Unknown history shows a dash. Added Minimum ROI % under Scan options (0 = off), a display filter applying to all alert types without changing scanning.
- Increased retained Sniper results from 250 to 5,000 rows and added 100-row pages with Previous/Next controls. Filtering and sorting apply before pagination.
- Added Buy list immediately after Sniper. Right-click an item name in Sniper to move all currently captured rows with that name to Buy list, preserving their data. Items on Buy list are suppressed from Sniper until removed or cleared. Buy list is saved across restarts and uses the same data columns and pagination.
- Right-click a Buy list item to copy its name or remove that item. Clear list has a confirmation prompt. Buy list entries are captured snapshots, not purchases or guarantees that listings remain available.
- Last sale age now turns yellow at 3 through 7 days instead of over 1 day. Orange above 7 days, recent-sale green at 1–120 minutes, and red above 30 days in any-age mode remain.

## 0.5.0.32

- When Skip 1 gil lookup is off, confirmed 1-gil candidates now take priority over ordinary candidates for detailed-history verification, in addition to the existing startup listing-lookup priority.
- Fresh candidates still run ahead of previously failed candidates, preserving deferred retry behavior. All candidates still use the same sequential history checks, selected history window and minimum sales.
- Updated the Skip 1 gil lookup tooltip to explain both priorities. Changes apply when watching starts.
- Includes the preceding release's RP venture indicator fix: remembered current-character timers and newer saved lookup snapshots can turn the bar green after the picker closes, while unavailable retainers remain excluded.

## 0.5.0.31

- Fixed RP venture readiness turning neutral when the game retainer roster is unavailable after closing the picker. The indicator now retains last observed timers for the current character's confirmed available retainers and turns green when those timers expire.
- Newer saved venture snapshots shown in Universalis lookup can update the remembered timer. Greyed-out/unavailable retainers remain excluded. Remembered availability and timers clear on logout or character change; open the summoning bell to establish or refresh availability.
- Tooltip explains that readiness uses last observed/saved data and can be refreshed at the bell. This is an estimate from known timers, not a fresh server-side venture query.
- Includes the preceding update's cyan HQ labels, discount/recent-sale colors, negative profit/savings warnings and color explanations.

## 0.5.0.30

- Added tooltip explanations for the sales-rate colors: orange below 1, yellow from 1 through 3, light green from 15 to under 30, and green at 30+ estimated sale transactions per day.
- Last sale age is green from 1 through 120 minutes, with a recent-activity explanation. Tooltips also explain the existing yellow over 1 day, orange over 7 days, and red over 30 days in any-age mode.
- Discounts are light green from 25% to under 50%, and green at 50% or more, with explanations that discounts compare against the history median and do not guarantee profit or demand.
- Negative estimated profit/item and savings/item now appear red with a loss/cost explanation. Unknown values remain neutral.
- Includes cyan HQ labels from 0.5.0.29; NQ keeps the normal text color.

## 0.5.0.29

- HQ labels in the Sniper Quality column are highlighted cyan; NQ keeps the normal text color.
- Fixed existing 1-gil batches exceeding the response-size limit. Requests now return only price, quantity, quality and world fields, with sales history disabled. The targeted lookup has an 8 MB bounded response allowance; other market request limits remain unchanged.
- Existing 1-gil checks still batch up to 100 selected/flagged items. They are processed before history verification; failed lookups are deferred so verification can continue. A selected-item watch list limits both lookup and live verification scope.

## 0.5.0.28

- Last sale age is yellow when over 1 day and orange when over 7 days. In 0-day history mode, the existing red warning over 30 days takes priority.
- Estimated sales/day is light green from 15 up to (but not including) 30, and green at 30 or more. Existing warnings remain orange below 1 and yellow from 1 through 3.
- Added Hide 1gil deals under Scan options, off by default. It can be changed while watching and hides only 1-gil rows from the table. Alerts are still collected and reappear when the toggle is disabled; it does not skip listing lookups.

## 0.5.0.27

- Added Skip 1 gil lookup under Scan options, off by default. It skips targeted startup lookups for existing 1-gil offers; new live 1-gil alerts still appear normally. Applies when watching starts.
- Existing 1-gil listing lookups now request up to 100 flagged items together, with sales history disabled. Successful item responses are retained; only missing or failed items stay pending for retry. Confirmed offers still receive history verification afterward.
- Sniper deals-table default widths now fit complete column headings and sort arrows at the current font size. Updated the layout identifier so old narrow saved widths do not carry over. Columns remain resizable and the table scrolls horizontally when necessary.

## 0.5.0.26

- Set candidate sales-history requests to up to 750 recent sales per item, shared across HQ/NQ. The sample-limit calculations and labels use the same cap.
- Added search above the Sniper deals table: item names match without case sensitivity; 1 gil (also 1gil or 1-gil) shows only 1-gil alerts; deal or deals shows ordinary deals. Clear restores all alert types. Search does not change scanning or watching.
- Counts distinguish rows hidden by existing filters from rows hidden by search; a no-match message makes active searches clear.
- Includes the preceding update's existing 1-gil startup checks, sortable Last sale price column for 0-day history, and collapsible Scan options.

## 0.5.0.25

- Cached HQ/NQ minima priced at 1 gil now trigger targeted current-listing lookups at startup. Only confirmed 1-gil offers are shown, with server and quantity; stale cached offers produce no alert. Failed lookups remain pending and retry after 30 seconds. Live alerts continue alongside startup lookups.
- Increased candidate history from 500 to up to 1,000 recent sales per item, shared across HQ/NQ. Updated capped-sample calculations, logs and tooltips. This does not guarantee fewer upstream timeouts.
- Added a sortable Last sale price column, visible only with history days 0. It shows the most recent dated same-quality sale price per item in the selected market scope, with its date on hover. It is separate from the median; unknown values show a dash.
- Item selection, market scope and scan settings are now under a collapsible Scan options header. Start/Stop, scope summary, live status and deals remain visible to leave more room for results.

## 0.5.0.24

- Sniper status now updates live after cached discovery: watching for new candidates, verifying a named item, processing ready candidates, or waiting with a countdown for failed-candidate retries.
- Shows new listing events received, ready/deferred candidates, items checked, items with usable history, pending history failures and cache-batch failures. Empty but successfully checked history is distinct from a failed request.
- Clarifies that cached discovery finishes once and Sniper then watches new live listings; it does not repeatedly scan existing listings. Keeps six concurrent cache batches, sequential verification, persistent deferred retries and the request debug panel.

## 0.5.0.23

- Keeps the new two-stage Sniper scan: cached four-day references in 100-item batches with up to six concurrent requests, then sequential detailed-history verification for live candidates. Verified deal prices still use your selected history window and minimum sales. Cached freshness is not guaranteed; screening can miss some opportunities.
- Failed candidate verification now stays pending and continues retrying while watching is active. New ready candidates are checked first. Failed candidates wait 15, 30, 45, then 60 seconds between verification passes; later waits stay at 60 seconds. Stop watching cancels retries. Successful items leave the retry queue.
- Added Sniper request debug: current pending/failed item names and IDs, failed-pass counts, next retry times, and the latest 100 cache/history request successes and failures with scope, IDs, duration and error details. Copy Sniper debug captures the information. A 504 identifies a failed request, not a particular culprit item.
- Explicit API results saying no stored item data now count as an empty successful history response, preventing endless retries for items without data. Unexpected missing responses and actual request failures remain eligible for retry.

## 0.5.0.22

- Sniper now starts with cached aggregate discovery for all marketable items or your selected list: 100 items per batch, up to six concurrent cached requests. It no longer downloads detailed sales history for the entire catalog at startup.
- New live listings are screened against separate HQ/NQ cached four-day averages. To allow extra margin, candidates priced at or below your threshold plus 25 percentage points (capped at 125% of the cached average) enter history verification. Missing cached references are checked too; 1-gil alerts remain immediate.
- Ordinary deals appear only after detailed history verifies your chosen history window, minimum sales and pricing rules. The displayed median, latest sale, sales estimate and profit still come from detailed history, not the cached average.
- Added Pending history verification so you can see items waiting for checks or retry. Verification runs one request at a time, preserves successful history, and retries failed candidates after 15 and 30 seconds, up to three verification passes. Manual retry remains available. No smaller-batch fallback was added.
- Cache freshness is not guaranteed: the aggregate endpoint does not provide a cache-refresh timestamp. Four days describes its sales calculation window, not cache age. A generous screening margin reduces but cannot eliminate missed deals compared with downloading every item's history.
- RP shows scanning while candidate history is being verified. History request pacing now guarantees at least 1.01 seconds after the previous history request finishes.

## 0.5.0.21

- Restored up to 100 items per Sniper history batch in every scope and history mode.
- The automatic failed-item rescan now starts after a 15-second cooldown. Automatic and manual failed-item rescans wait 5 seconds after each history request finishes, including the retry of a temporarily failed request.
- Initial scans keep their normal one-second minimum between request starts and one retry for temporary errors. Rescans request only unresolved items; successful history and live watching are preserved. Smaller-batch fallbacks remain disabled.

## 0.5.0.20

- Sniper uses up to 90 items per history request only for Data Center or Region scope with history days set to 0. World scope and history days 1–14 keep 100-item requests. Automatic and manual retries use the same conditional limit; the 500-sale sample limit is unchanged.
- Fixed the selected-item search offering unmarketable items. It now uses the same marketable-item catalog as the full scan.
- All marketable items (default) now has a clear green label with its explanation underneath. Selected-item scope remains orange.

## 0.5.0.19

- Sniper can watch multiple selected items: search and add items, remove each with X, or clear the selection to scan all marketable items. All marketable items remains the default when the plugin loads; selected lists are temporary for focused scans and testing. HQ/NQ are handled separately.
- Selected-item scope and explanatory labels now reflect the selected list instead of incorrectly claiming all items are scanned. Limited scope is highlighted orange.
- Partial history responses now count results per item. Successful items are kept; only missing or failed items enter automatic and manual retry passes. A valid empty sales-history response counts as loaded, even when it cannot establish a deal baseline.
- Retained original-size requests, one retry for temporary request errors, and the retry pass after the full scan. No smaller-batch fallback or additional request delays were added.

## 0.5.0.18

- Removed the 50-item Sniper history fallback. Failed requests are no longer split into smaller batches, reducing time spent on failing requests during the initial scan.
- Temporary request failures still get one retry at the original batch size. Unresolved items are automatically retried once after the full scan and its five-second cooldown; manual retry remains available.
- History requests still run one batch at a time with at least one second between request starts. Live alerts continue during the scan and retry pass.

## 0.5.0.17

- Fixed the green RP venture indicator counting unavailable, greyed-out retainers. It now counts completed ventures only for retainers confirmed available in the summoning-bell picker.
- Retainer availability is refreshed when the picker is open and cleared when changing characters. Before availability is known, the indicator stays neutral.
- The tooltip identifies completed ventures on available retainers and explains how to refresh availability.

## 0.5.0.16

- The optional RP server-bar button turns green when the current character has completed ventures in loaded retainer data.
- Hover over RP to see the completed venture count and retainer names. If none are detected, the tooltip explains that opening the summoning bell refreshes the loaded roster.
- The scanning indicator remains visible while Sniper history or undercut checks are running.

## 0.5.0.15

- Sniper estimated sales/day is orange below 1 and yellow from 1 through 3, warning about slower resale.
- Removed the 25-item history fallback to reduce time spent retrying failed batches. Failed 100-item batches can still split into 50-item groups; unresolved items are retried automatically after the full scan.

## 0.5.0.14

- Added Backup / export settings in Settings. Export pricing, Sniper, venture settings and all item lists to a JSON file. Market caches and retainer listing snapshots are excluded; venture assignments retain their retainer IDs.
- Preview a backup before importing, then merge lists or replace included settings. Merge adds missing list/dictionary entries while preserving existing entries; included scalar settings are applied. A timestamped backup of your previous settings is saved beside the imported file first. Stop active operations and watching before import.

## 0.5.0.13

- After a full Sniper history scan, unresolved items are automatically retried once after a five-second cooldown before the scan is declared complete. Successful history and live watching are preserved. Stop watching cancels the retry; manual Retry failed items remains available for unresolved failures.
- Undercut notifier now uses World, Data Center and Region radio buttons, matching the scope layout elsewhere in the plugin.

## 0.5.0.12

- All Sniper history modes now use one batch at a time, with at least one second between request starts, including retries. Keeps the 500-sale cap and 100 → 50 → 25 fallback.
- Added Filter by minimum profit value beside Minimum item value. Enabling it turns on Show estimated resale profit and reveals a gil-per-item amount. Disabling the filter hides the amount; disabling profit display also disables the filter. Ordinary deals must have a known estimated profit at or above the amount.
- 1-gil alerts bypass the minimum-profit amount, including unknown-history alerts. The existing rule still hides known zero/negative-profit 1-gil alerts when estimated resale profit is enabled.
- The optional RP server-bar button shows RP (scanning…) while Sniper fetches or retries history, or Undercut notifier checks prices. It returns to RP between checks and during live-feed-only watching.
- Est. sales/day below 1 is yellow, with a hover warning that low sales activity may mean slower resale and less dependable price estimates. Unknown estimates remain a dash.

## 0.5.0.11

- When Show estimated resale profit is enabled, 1-gil alerts with a known estimated profit of zero or less are hidden. This updates immediately as sales history arrives or fee assumptions change.
- 1-gil alerts with unknown sales history remain visible in yellow. Turning Show estimated resale profit off shows the hidden alerts again. Estimates use the returned same-quality sales median and configured fee assumptions; actual resale results can differ.

## 0.5.0.10

- History days 0 (any age) now uses one history batch at a time, with at least one second between request starts, including retries and smaller fallback requests.
- Fixed-window scans keep two concurrent batches. Both modes retain 100-item starting batches, 50/25-item fallback and the 500-sale cap. This change reduces request pressure; persistent Universalis timeouts may still occur.

## 0.5.0.9

- Reduced Sniper to two concurrent 100-item history batches. Smaller 50/25-item fallback retries remain available. This reduces load but does not guarantee Universalis timeouts are resolved.
- Scan progress now says items and batches attempted, with separate history-loaded and failed-item counts. The disabled retry button explains that retries become available after the scan finishes.
- Added Dalamud log diagnostics for failed history requests: scope, item IDs, batch size, history days, sales cap, attempt number, elapsed time and error. Logs also record fallback splitting and scan summaries to help diagnose persistent failures.
- The bottom-left version label and feedback reports now display all four version numbers, such as 0.5.0.9.

## 0.5.0.8

- Reduced Sniper history requests from up to 1,800 to 500 sales per item (HQ and NQ combined) to reduce request size. The minimum-sales setting is now capped at 500.
- Renamed Sales/day to Est. sales/day. Fixed-window scans use the chosen window when fewer than 500 sales are returned. When the cap is reached, or history days is 0, the estimate uses the time from the oldest returned sale to the history check.
- Hover the estimate to see the same-quality sample count, actual time coverage and calculation basis. Sample-based rates show a dash when there are fewer than two matching-quality sales or no usable time span. Medians continue using the returned matching-quality sales; estimates can differ from actual demand.

## 0.5.0.7

- Fixed selecting or clearing a specific Sniper item corrupting UI text colors and making other labels disappear. Orange highlighting now resets correctly. Reload the plugin after updating if your UI is already affected.
- Added an independent World (default), Data Center, or Region (home region only) selector to Undercut notifier. The Server column identifies the lowest matching competitor in that scope.
- Starting Sniper with the same market scope also runs notifier checks every 60 seconds. Stopping Sniper ends linked checks unless independent notifier watching is active. Starting the notifier alone never starts Sniper. Notifier checks use separate current-listing requests, not Sniper sales-history estimates.

## 0.5.0.6

- Reduced Sniper to three concurrent history batches. The 100 → 50 → 25 fallback and Retry failed items remain available.
- Fixed 1-gil alerts failing to attach available same-quality sales history. They still appear immediately and now update when their history batch finishes, even if the sales count is below the ordinary-deal minimum.
- 1-gil alerts show their type, item name and listing price in yellow while the last-sale date is unknown, and red when it is available. Unknown can mean history is pending, failed, or no matching sales were returned.
- With History days set to 0, Last sale age turns red when the latest recorded sale is more than 30 days old.

## 0.5.0.5

- Sniper highlights specific-item mode in orange, making it clear when you are not watching all marketable items.
- History days now accepts 0 for any-age sales history. The configured minimum sales count still applies, using up to 1,800 returned sales per item. This removes the age cutoff; it does not retrieve unlimited history.
- Any-age mode labels the median accordingly and shows a dash for Sales/day because there is no fixed time window.

## 0.5.0.4

- Sniper now uses up to four concurrent history batches. Batches start at 100 items; persistent timeouts or temporary network/server failures retry as sequential groups of 50, then 25. Each new batch starts at 100 again.
- If history requests still fail, Sniper shows Scan incomplete and the unresolved item count instead of implying full coverage. Ordinary deals need successful sales history; 1-gil alerts remain available.
- Added Retry failed items. It requests only unresolved items while preserving successful history and the live listing feed. Stop watching also cancels retries.
- Includes the specific-item search from testing 0.5.0.3. All marketable items remains the default; select an item to scan its history and watch only its new HQ/NQ listings in the selected market scope.

## 0.5.0.3 (testing)

- Added a specific-item search and picker in Sniper. All marketable items remains the default. Select an item before Start watching to scan only its history and watch new HQ/NQ listings for that item in the selected market scope. Choose All marketable items to return to the full scan.

## 0.5.0.2

- Sniper scans history in 100-item batches with up to six batches running concurrently. Removed the fixed one-second pause between batches. All plugin requests still share limits of 25 requests per second and eight active connections; scan speed depends on Universalis response times.
- Renamed Sniper's Listing column to Listing price.
- Sniper hides the Data Center column in World and Data Center scope, and shows it in Region scope.

## 0.5.0.1

- Undercut notifier now shows a Server column beside Lowest competitor. It identifies the home-world server checked for that price; notifier checks remain home-world only.
- Sniper now has a sortable Type of alert column before Item, showing 1 gil or Deal. Item names no longer include the 1 GIL ALERT prefix. 1-gil alerts retain red text.

## 0.5.0.0

#### Inventory right-click shortcuts
- Added five separate Add/Remove options: Exceptions, Don't reprice, Batch selling, Retainer sale whitelist, and Don't vendor.
- Add options are green for market lists and yellow for the two vendor lists. Remove options are always red.
- Options are on by default. Turn them off with Settings → Show inventory right-click list options.
- Add options are hidden when the selected item cannot use that action, or a protection prevents it. Saved entries still have a Remove option. These lists apply to both HQ and NQ items.
- Adding to Batch selling starts at 10 items per listing with no total limit. Change those amounts in Batch selling.

#### Pricing rules and items with no competitors
- Added Custom gil amount (1–10,000 gil) and Percentage (1–100%, with up to two decimal places, such as 5.2%). The amount input appears only when that rule is selected. Match lowest and Undercut by 1 gil remain available.
- Auto update, Update existing listings, Start listing items, and Start batch selling only use current same-quality competitor listings first. A recent sale is no longer required when competitors exist.
- With no competing listings, existing prices stay unchanged. New-item listing and batch selling can use the same-quality sales median from the last 30 days, applying your selected pricing rule and minimum price. Without usable sales or listings, the item is skipped.
- The minimum price and large-price-drop approval bands still apply. A large drop during repricing is held for your approval rather than applied automatically.

#### Undercut notifier
- Added a tab showing saved retainer prices, cheaper home-world competitors, and the last recorded same-quality sale. Hover the sale price for its date.
- Start watching checks every 60 seconds. Echo alerts are on by default and can be turned off. Repeated alerts for the same retainer, item, quality and saved price are suppressed during the plugin session.
- Pause and hide in duties is on by default. This pauses checks and hides notifier results while you are in a duty.
- Starting Sniper in World scope also runs notifier checks. Stopping Sniper ends linked checks unless you started independent notifier watching. Starting the notifier alone never starts Sniper. Data Center and Region Sniper runs are not linked.
- Listing and batch-selling runs save the current retainer's listings after each confirmed new listing, without starting watching. Use Refresh all retainer listings to capture other retainers or refresh prices after sales and repricing.
- The notifier compares home-world listings of the same HQ/NQ quality. It ignores saved own-retainer IDs and unknown owners. Saved prices and Universalis data can be outdated; both timestamps are shown. Alerts do not require recent sales.

#### Existing listings
- Results now remain visible across the entire retainer update run, including reasons an item was skipped. An empty final retainer no longer clears earlier results.
- Added a retainer dropdown, item-name search, and clickable column sorting. Click Retainer to group results by retainer.
- Added Last sold price with the sale date on hover, and a 30-day sales median reference. Saved listings are shown when no live results are available. A dash means sales data has not been loaded or no matching sale was returned.

#### Sniper deal information
- Deal columns now show Item, Data Center, Server, Quality, Quantity, Listing, history-window median, Discount, Sales/day, Last sale age, Est. profit/item, and Savings/item.
- Discount shows how far the asking price is below the sales median. Sales/day shows recorded transactions divided by the chosen history window. Sniper's history window remains separately configurable from 3–14 days.
- Hover numbers for plain-language explanations. Hover Quantity or Listing for whole-stack purchase cost; hover profit/item or savings/item for whole-stack totals. Separate stack-profit and stack-savings columns have been removed.
- Estimated profit is shown by default. Turn off Show estimated resale profit to hide its column. Estimates assume resale at the median, using configurable purchase-fee and resale-tax assumptions, both defaulting to 5%. Actual fees and sale prices can vary.
- Added optional minimum discount, savings per item, and estimated profit per item filters. All three default to off and update the displayed deals immediately. 1-gil alerts bypass these filters and do not need sales history.
- Columns can be sorted and hidden. Latest-sale age and the history-check time on hover help identify stale estimates. Restart watching to refresh the sales baseline. Purchases remain manual.
- Fixed a different listing's removal incorrectly hiding a grouped 1-gil alert. Deals still disappear when their listing is removed or its updated price no longer qualifies.

#### Ventures and commands
- Added a yellow Start venture cycle button beside Start batch selling only at the top. The original Ventures-tab button remains available; both use your venture settings.
- Venture cycles can run while Sniper watches. Pricing, retainer selling and venture actions still avoid overlapping game menus.
- Added /rp as an alternative to /retainerpricer and /retainer. If another plugin already uses /rp, the existing commands remain available.

## 0.4.57.0
- Reorganized Sniper deals into Item, Data Center, Server, Quality, Quantity, Listing, sales median, Discount, Sales/day, Last sale age, estimated profit/item, and Savings/item.
- Whole-stack purchase cost, profit and savings remain in hover explanations instead of separate columns.

## 0.4.56.0
- Existing listings now supports retainer and item-name filters, clickable column sorting, and Last sold price with sale-date tooltips.
- Added plain-language hover explanations to Sniper deal values. Whole-stack profit and savings are shown on hover over per-item values instead of separate columns.
- Venture cycles can run while Sniper watches.
- Fixed unrelated listing removals hiding grouped 1-gil alerts.

## 0.4.55.0
- Sniper now shows discount, recorded sales per day, estimated purchase cost, savings, and estimated resale profit per item and stack. Profit columns are on by default and can be hidden.
- Added optional minimum discount, savings and profit filters with configurable fee assumptions. 1-gil alerts bypass these filters.
- Added latest-sale age and history-check age; deal columns can be sorted and hidden.

## 0.4.54.0
- Added Last recorded sale beside Lowest competitor in Undercut notifier. Shows the latest same-quality sale price, with its date on hover, or no sale in the last 30 days.

## 0.4.53.0
- Clarified the listing progress and help text for pricing from recent sales when no competitors exist.

## 0.4.52.0
- New-item listing and batch selling use the same-quality 30-day sales median when no competing listings exist. The selected pricing rule and minimum price still apply. Items without usable sales or listings are skipped.

## 0.4.51.0
- Current competing listings now drive all pricing actions without requiring recent sales. With no competitors, existing prices remain unchanged and new listings are skipped; same-quality 30-day sales medians are reference only.
- Existing listings now retains item results across the whole retainer run, including skip reasons, and shows saved listings when no live results are available.

## 0.4.50.0
- Percentage undercut pricing now accepts up to two decimal places, such as 5.2%.

## 0.4.49.0
- Added a yellow Start venture cycle button beside Start batch selling only at the top of the window. The existing Ventures-tab button remains available.

## 0.4.48.0
- Start listing items and Start batch selling only now refresh the current retainer's saved listings after each confirmed new listing. Undercut notifier can use these snapshots without starting watching automatically.

## 0.4.47.0
- Sniper watching in World scope also runs Undercut notifier checks. Independent notifier watching never starts Sniper and can continue after Sniper stops.

## 0.4.46.0
- Added /rp as an alternative command.
- Add to retainer sale whitelist and Add to don't vendor are yellow. Other Add options stay green and all Remove options stay red.

## 0.4.45.0
- Hide inventory Add options when an item cannot use the corresponding market or vendor action, including protected-item conflicts. Remove options remain available for saved entries.

## 0.4.44.0
- Added custom 1–10,000 gil and 1–100% undercut rules. Their amount inputs appear only when selected.
- Inventory right-click Add options are green; Remove options are red.

## 0.4.43.0
- Added separate inventory right-click Add/Remove options for Exceptions, Don't reprice, Batch selling, Retainer sale whitelist, and Don't vendor. Enable or disable them in Settings; enabled by default.
- Added Undercut notifier with saved-price comparisons, optional Echo alerts, and an option to pause and hide in duties.

## 0.4.42.0
- Updated compatibility for FFXIV patch 7.57.

## 0.4.41.0
- Expanded the venture table to use the available window height, widened Which Venture, and made its columns resizable.

## 0.4.40.0
- Added Time left in venture to saved retainer snapshots, with a countdown and Complete/No venture status after refresh.
- Added item-name search to each retainer's Which venture dropdown.

## 0.4.39.0
- Auto update now updates prices first, then runs ventures when enabled, and has an orange button.
- Update existing listings now processes all retainers without ventures.
- Added a green Update listings current retainer only button at the bottom of Existing listings.

## 0.4.38.0
- Made Start retainer sales, Start venture cycle, and Start watching green.
- Added collapsible Retainer sale whitelist and Don't vendor sections with item counts; the start button remains visible.

## 0.4.37.0
- Added a Don't vendor list to Retainer sell. Protected items skip NPC sales, including whitelisted items, while remaining eligible for marketboard listing.

## 0.4.36.0

- Retainer sell can now sell bound items added to its whitelist. Bound items not on the whitelist remain protected.

## 0.4.35.0

- Remember approve or ignore choices for a large price drop on the same item throughout the current repricing run; ask again on the next run.

## 0.4.34.0

- In Region scope, Sniper now shows each server's Data Center beside its world name.

## 0.4.33.0 (testing)

- Sniper now monitors live listings during the initial history scan. New 1-gil alerts appear immediately, and other deals appear as their history batches complete.

## 0.4.32.0 (testing)

- Sniper's Region scope now uses only the home-world region; Materia/Oceania is no longer added to other regions.
- Venture choices are generated from each retainer's job and level, with gathering choices limited to items recorded in the Gathering Log. No venture item menu capture is needed.
- Refresh available ventures now shows the result beside the button, including the number of options found or what is needed to refresh.

## 0.4.31.0

- Sniper retries temporary Universalis history request failures once, so a brief timeout or interrupted connection does not discard the whole item batch.
- Sniper reports the first failed batch reason when history could not be retrieved.

## 0.4.30.0 (testing)

- Added per-retainer venture selection, defaulting to Quick Exploration, with task options captured from the game's open venture list.
- Miner, botanist, and fisher choices are checked against the Gathering Log and only appear after the item has been gathered; capture options per retainer and level range.

## 0.4.29.0

- Added automatic retainer venture job detection and a per-retainer dropdown override for Combat, Miner, Botanist, and Fisher.

## 0.4.28.0 (testing)

- Keep marketboard results open for an extra 0.2 seconds after receiving complete listings before closing and continuing the queue.

## 0.4.27.0 (testing)

- Fixed queued marketboard lookups stopping on partially populated results while moving between items.

## 0.4.26.0 (testing)

- Added (testing) beside the window version and to feedback reports when running a Dalamud testing build.

## 0.4.25.0 (testing)

- Fixed selecting the matching marketboard search result by using the list item click event.
- Retrieved prices now show data age in days and hours instead of rounded day abbreviations.

## 0.4.24.0

- Fixed submitting queued item-name searches by activating the marketboard Search button.
- Redesigned Retrieved price with cheapest and home-world marketboard prices, recent purchase prices, world names, and data ages for the selected HQ/NQ quality.

## 0.4.23.0
- Fixed marketboard lookup waiting on the listings-state flag before it could search. Lookup completion now checks the received response and matching listing count.

## 0.4.22.0
- Fixed queued marketboard lookups timing out by searching the item name and selecting the matching search result before requesting listings.

## 0.4.21.0
- Renamed the marketboard lookup button for clarity, fixed detection of the open Item Search window, and fixed the marketboard prompt width.
- Added chocobo saddlebag refresh to Don't reprice.
- Removed New / selected item, moved Sniper to the second tab, and placed Help last.
- Fixed the venture cycle getting stuck on dialogue after assigning or collecting a venture.

## 0.4.20.0
- Fixed Refresh all retainer listings and Auto update getting stuck on retainer farewell dialogue after choosing Quit. The dialogue handling works across retainer personalities. Also fixed the venture cycle getting stuck on farewell dialogue after choosing Quit.

## 0.4.19.0
- Added an optional venture cycle with settings for idle Quick Exploration and repeating completed ventures. It uses Retainer Pricer's game UI and does not require AutoRetainer.
- Added a pricing rule to match the lowest eligible listing or undercut it by 1 gil.

## 0.4.18.0
- Added chocobo saddlebag snapshots to the Exceptions item picker, including the premium saddlebag when available.
- Added bulk addition of saddlebag items to Exceptions. Exclusions remain item-based and apply to carried inventory and Retainer sell.

## 0.4.17.0
- Added a Retrieved price subtab in Price lookup.
- Showed the retrieved price for Search and Captured items, including inventory and retainer-list Retrieve buttons.

## 0.4.16.0
- Added saved retainer listing snapshots and a deduplicated queue of changed item IDs.
- Added a separate marketboard lookup that waits for an open board, searches queued items, and opens each item's listings.

## 0.4.15.0
- Reverted automatic Compare Prices views after listings and repricing.
- Made the retrieved market price easier to spot in a dedicated Price Lookup result row.

## 0.4.14.0
- Added Universalis market data uploads after new listings and confirmed repricing, using Dalamud's Compare Prices flow when the uploader is enabled.

## 0.4.13.0
- Added carried-inventory pickers to Retainer sale whitelist and Don't reprice.
- Added an empty-Exceptions warning before listing, with an option to suppress future warnings.
- Enlarged the Discord button beside feedback in Help.

## 0.4.12.0
- Added explicit World, Data Center, and Region pricing choices in Settings.
- Added a Retainer sale whitelist that bypasses marketability and price checks for selected items; bound items, Exceptions, and saved gear-set items stay protected.
- Fixed Auto update to advance the retainer's departure dialogue when returning from a selling list.
- Restored the Discord invite button in Help.

## 0.4.11.0
- Added independent Sniper scope options for World, Data Center, and Region, including Materia.
- Changed Auto vendor to use the retainer's “Have Retainer Sell Items” option; a vendor window is no longer required.

## 0.4.10.0
- Added an ETA to Sniper's one-time initial market scan; background listening does not repeat the full scan.
- Added an approximate Auto update ETA after the first retainer finishes; it updates as the run progresses.
- Added a Settings toggle for the clickable server info bar shortcut.

## 0.4.9.0
- Added an Auto vendor list for items to sell regardless of market price; exceptions and saved gear set protections still apply.
- Added an optional server info bar button that opens Retainer Pricer.
- Added a Discord invite button beside Send feedback.

# Retainer Pricer changelog

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

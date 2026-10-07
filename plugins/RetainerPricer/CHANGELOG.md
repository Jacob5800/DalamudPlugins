# Retainer Pricer changelog

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

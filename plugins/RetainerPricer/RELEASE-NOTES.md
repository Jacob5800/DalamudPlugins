## 0.5.0.19

- Sniper can watch multiple selected items: search and add items, remove each with X, or clear the selection to scan all marketable items. All marketable items remains the default when the plugin loads; selected lists are temporary for focused scans and testing. HQ/NQ are handled separately.
- Selected-item scope and explanatory labels now reflect the selected list instead of incorrectly claiming all items are scanned. Limited scope is highlighted orange.
- Partial history responses now count results per item. Successful items are kept; only missing or failed items enter automatic and manual retry passes. A valid empty sales-history response counts as loaded, even when it cannot establish a deal baseline.
- Retained original-size requests, one retry for temporary request errors, and the retry pass after the full scan. No smaller-batch fallback or additional request delays were added.

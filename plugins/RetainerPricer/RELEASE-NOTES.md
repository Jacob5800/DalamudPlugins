## 0.5.0.33

- When Skip 1 gil lookup is enabled, live 1-gil alerts now require verified matching-quality history meeting your minimum sales before appearing. Failed verification remains pending for retry. With the option off, confirmed startup and live 1-gil alerts remain immediate.
- Added a sortable ROI column: estimated resale profit divided by purchase cost including assumed fees, expressed as a percentage. Unknown history shows a dash. Added Minimum ROI % under Scan options (0 = off), a display filter applying to all alert types without changing scanning.
- Increased retained Sniper results from 250 to 5,000 rows and added 100-row pages with Previous/Next controls. Filtering and sorting apply before pagination.
- Added Buy list immediately after Sniper. Right-click an item name in Sniper to move all currently captured rows with that name to Buy list, preserving their data. Items on Buy list are suppressed from Sniper until removed or cleared. Buy list is saved across restarts and uses the same data columns and pagination.
- Right-click a Buy list item to copy its name or remove that item. Clear list has a confirmation prompt. Buy list entries are captured snapshots, not purchases or guarantees that listings remain available.
- Last sale age now turns yellow at 3 through 7 days instead of over 1 day. Orange above 7 days, recent-sale green at 1–120 minutes, and red above 30 days in any-age mode remain.

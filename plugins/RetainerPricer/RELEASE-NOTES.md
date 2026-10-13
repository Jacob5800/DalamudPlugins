### 0.5.0.4

- Sniper now uses up to four concurrent history batches. Batches start at 100 items; persistent timeouts or temporary network/server failures retry as sequential groups of 50, then 25. Each new batch starts at 100 again.
- If history requests still fail, Sniper shows Scan incomplete and the unresolved item count instead of implying full coverage. Ordinary deals need successful sales history; 1-gil alerts remain available.
- Added Retry failed items. It requests only unresolved items while preserving successful history and the live listing feed. Stop watching also cancels retries.
- Includes the specific-item search from testing 0.5.0.3. All marketable items remains the default; select an item to scan its history and watch only its new HQ/NQ listings in the selected market scope.

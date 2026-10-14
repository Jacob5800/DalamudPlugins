## 0.5.0.20

- Sniper uses up to 90 items per history request only for Data Center or Region scope with history days set to 0. World scope and history days 1–14 keep 100-item requests. Automatic and manual retries use the same conditional limit; the 500-sale sample limit is unchanged.
- Fixed the selected-item search offering unmarketable items. It now uses the same marketable-item catalog as the full scan.
- All marketable items (default) now has a clear green label with its explanation underneath. Selected-item scope remains orange.

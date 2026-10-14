## 0.5.0.22

- Sniper now starts with cached aggregate discovery for all marketable items or your selected list: 100 items per batch, up to six concurrent cached requests. It no longer downloads detailed sales history for the entire catalog at startup.
- New live listings are screened against separate HQ/NQ cached four-day averages. To allow extra margin, candidates priced at or below your threshold plus 25 percentage points (capped at 125% of the cached average) enter history verification. Missing cached references are checked too; 1-gil alerts remain immediate.
- Ordinary deals appear only after detailed history verifies your chosen history window, minimum sales and pricing rules. The displayed median, latest sale, sales estimate and profit still come from detailed history, not the cached average.
- Added Pending history verification so you can see items waiting for checks or retry. Verification runs one request at a time, preserves successful history, and retries failed candidates after 15 and 30 seconds, up to three verification passes. Manual retry remains available. No smaller-batch fallback was added.
- Cache freshness is not guaranteed: the aggregate endpoint does not provide a cache-refresh timestamp. Four days describes its sales calculation window, not cache age. A generous screening margin reduces but cannot eliminate missed deals compared with downloading every item's history.
- RP shows scanning while candidate history is being verified. History request pacing now guarantees at least 1.01 seconds after the previous history request finishes.

## 0.5.0.25

- Cached HQ/NQ minima priced at 1 gil now trigger targeted current-listing lookups at startup. Only confirmed 1-gil offers are shown, with server and quantity; stale cached offers produce no alert. Failed lookups remain pending and retry after 30 seconds. Live alerts continue alongside startup lookups.
- Increased candidate history from 500 to up to 1,000 recent sales per item, shared across HQ/NQ. Updated capped-sample calculations, logs and tooltips. This does not guarantee fewer upstream timeouts.
- Added a sortable Last sale price column, visible only with history days 0. It shows the most recent dated same-quality sale price per item in the selected market scope, with its date on hover. It is separate from the median; unknown values show a dash.
- Item selection, market scope and scan settings are now under a collapsible Scan options header. Start/Stop, scope summary, live status and deals remain visible to leave more room for results.

## 0.5.0.24

- Sniper status now updates live after cached discovery: watching for new candidates, verifying a named item, processing ready candidates, or waiting with a countdown for failed-candidate retries.
- Shows new listing events received, ready/deferred candidates, items checked, items with usable history, pending history failures and cache-batch failures. Empty but successfully checked history is distinct from a failed request.
- Clarifies that cached discovery finishes once and Sniper then watches new live listings; it does not repeatedly scan existing listings. Keeps six concurrent cache batches, sequential verification, persistent deferred retries and the request debug panel.

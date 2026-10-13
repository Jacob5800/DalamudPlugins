### 0.5.0.9

- Reduced Sniper to two concurrent 100-item history batches. Smaller 50/25-item fallback retries remain available. This reduces load but does not guarantee Universalis timeouts are resolved.
- Scan progress now says items and batches attempted, with separate history-loaded and failed-item counts. The disabled retry button explains that retries become available after the scan finishes.
- Added Dalamud log diagnostics for failed history requests: scope, item IDs, batch size, history days, sales cap, attempt number, elapsed time and error. Logs also record fallback splitting and scan summaries to help diagnose persistent failures.
- The bottom-left version label and feedback reports now display all four version numbers, such as 0.5.0.9.

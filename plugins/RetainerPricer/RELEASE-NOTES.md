### 0.5.0.12

- All Sniper history modes now use one batch at a time, with at least one second between request starts, including retries. Keeps the 500-sale cap and 100 → 50 → 25 fallback.
- Added Filter by minimum profit value beside Minimum item value. Enabling it turns on Show estimated resale profit and reveals a gil-per-item amount. Disabling the filter hides the amount; disabling profit display also disables the filter. Ordinary deals must have a known estimated profit at or above the amount.
- 1-gil alerts bypass the minimum-profit amount, including unknown-history alerts. The existing rule still hides known zero/negative-profit 1-gil alerts when estimated resale profit is enabled.
- The optional RP server-bar button shows RP (scanning…) while Sniper fetches or retries history, or Undercut notifier checks prices. It returns to RP between checks and during live-feed-only watching.
- Est. sales/day below 1 is yellow, with a hover warning that low sales activity may mean slower resale and less dependable price estimates. Unknown estimates remain a dash.

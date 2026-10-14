## 0.5.0.32

- When Skip 1 gil lookup is off, confirmed 1-gil candidates now take priority over ordinary candidates for detailed-history verification, in addition to the existing startup listing-lookup priority.
- Fresh candidates still run ahead of previously failed candidates, preserving deferred retry behavior. All candidates still use the same sequential history checks, selected history window and minimum sales.
- Updated the Skip 1 gil lookup tooltip to explain both priorities. Changes apply when watching starts.
- Includes the preceding release's RP venture indicator fix: remembered current-character timers and newer saved lookup snapshots can turn the bar green after the picker closes, while unavailable retainers remain excluded.

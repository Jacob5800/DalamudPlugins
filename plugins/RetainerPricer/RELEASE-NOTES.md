## 0.5.0.15

- Sniper estimated sales/day is orange below 1 and yellow from 1 through 3, warning about slower resale.
- Removed the 25-item history fallback to reduce time spent retrying failed batches. Failed 100-item batches can still split into 50-item groups; unresolved items are retried automatically after the full scan.

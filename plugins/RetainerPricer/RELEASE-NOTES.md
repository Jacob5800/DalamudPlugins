### 0.5.0.8

- Reduced Sniper history requests from up to 1,800 to 500 sales per item (HQ and NQ combined) to reduce request size. The minimum-sales setting is now capped at 500.
- Renamed Sales/day to Est. sales/day. Fixed-window scans use the chosen window when fewer than 500 sales are returned. When the cap is reached, or history days is 0, the estimate uses the time from the oldest returned sale to the history check.
- Hover the estimate to see the same-quality sample count, actual time coverage and calculation basis. Sample-based rates show a dash when there are fewer than two matching-quality sales or no usable time span. Medians continue using the returned matching-quality sales; estimates can differ from actual demand.

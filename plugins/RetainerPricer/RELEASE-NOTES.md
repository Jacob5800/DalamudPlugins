### 0.5.0.6

- Reduced Sniper to three concurrent history batches. The 100 → 50 → 25 fallback and Retry failed items remain available.
- Fixed 1-gil alerts failing to attach available same-quality sales history. They still appear immediately and now update when their history batch finishes, even if the sales count is below the ordinary-deal minimum.
- 1-gil alerts show their type, item name and listing price in yellow while the last-sale date is unknown, and red when it is available. Unknown can mean history is pending, failed, or no matching sales were returned.
- With History days set to 0, Last sale age turns red when the latest recorded sale is more than 30 days old.

## Retainer Pricer 0.5.1.68

- World repricing uses recent matching-quality sales history when no competing listings are available.
- Require at least three matching-quality sales in the last 30 days for automatic history pricing. Otherwise, approve/edit a price or skip in a popup.
- Ask before using history containing sales below 25% or above 5x its median, or submitting a price outside those bounds. Existing price protections still apply.
- Added Show automation list in Visible tabs, disabled by default. Hiding the tab preserves automation settings.

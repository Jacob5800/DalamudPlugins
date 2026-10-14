## 0.5.1.9

- Fixed marketboard ROI being hidden when the native loading flag remains set after results are displayed. Visible listing rows are checked directly.

- Added Retainer vendor items beside Start listing items in the Markets header, using the existing retainer-sales lists and protections. Shifted the picker header shortcuts slightly right.

- Hovering marketable items retries incomplete tooltip data at most once per minute per item while keeping available cached values visible. HQ data is only required for items that support HQ.

- Retainer sales skip items with no NPC selling price, including unsellable starter equipment, even when whitelisted or Universalis reports a low market price.

## 1.0.5.15

- Improve disable/update cleanup: stop callbacks first, attempt remaining cleanup after an error, and ignore repeated shutdown calls.

## 1.0.5.14

- Replaced the palm tree beside HT with the Ishgard building icon.

## 1.0.5.13

- Added a housing-themed icon beside the HT server-bar entry.

# Changelog

## 1.0.5.12

- Automatic character creation requires the verified logged-in identity; read-only house lookups no longer create character records.
- Saved characters can be removed with confirmation to clean up unwanted entries.

- Saved character identity now comes from the logged-in player state instead of the object table.
- House overview only shows detected houses, excluding empty Unknown/ward 0/plot 0 entries.


## 1.0.5.11

- Added Remove from overview with confirmation for individual personal or FC houses. Deletes the saved address and visit timestamp, disables reminders, and keeps the house hidden until restored in character settings. Visit history is retained.

## 1.0.5.9

- House overview has a Remove from tracking button with confirmation showing the character and house. It stops reminders while keeping the address and visits; enable reminders in character settings to resume tracking.
- Days since visit shows completed whole days without decimals.
- New configurations default to a 1-hour repeat interval for dismissed reminders and a 1-hour snooze. Existing saved choices are preserved.
- Snooze duration has a tooltip explaining that it temporarily hides reminders without recording a visit or changing the demolition timer.

## 1.0.5.8

- House overview columns now have clearer starting widths, can be resized, and scroll horizontally when needed.
- Replaced the awkward reposition controls with a labeled Move reminder window checkbox in the shared footer on every tab.

## 1.0.5.7

- Improved settings layout with compact hour inputs and labels above them, a resizable window, and content that uses available space.
- Cleaned up reminder-window controls.
- Dalamud author display now shows Jacob5800. Original author credits and license are preserved.

## 1.0.5.6

- Added a housing import preview showing saved characters, addresses and visit dates before applying any changes. Import previewed houses and Cancel are separate actions.
- Import automatically saves a timestamped safety backup of current housing data in the user's plugin configuration folder before merging. Newer matching-address visits and existing conflicting addresses are preserved.

## 1.0.5.5

- Added a House overview showing all saved characters, house addresses, last interior visits, days since visit and tracking explanations. Missing houses, disabled reminders and FC access restrictions are clearly identified.
- Added configurable reminder repeat and snooze durations (1–168 hours, default 24). Dismissed reminders return after the chosen interval; Snooze temporarily hides a reminder without changing recorded visits.
- Added Visit history, retaining up to 500 new visit confirmations. Earlier visits are not reconstructed.
- Added a Date format dropdown: Day/Month/Year (default), Month/Day/Year, or Year/Month/Day, with a local-time example. The HT tooltip uses the chosen format.
- Added a Discord join button with a recognizable Discord-style symbol. Settings now have a scrollable area for the additional controls.
- Reminder dates are based on saved interior visits and your chosen interval, not Square Enix's demolition timer.

## 1.0.5.4

- Added an opt-in HT server-bar button. Click it to open settings; its tooltip shows the next enabled house needing a visit. Optional yellow/red colors indicate your reminder interval, not the game's demolition timer.
- Added an Echo confirmation when an interior visit is recorded, with a settings toggle.
- Added export and merge import of tracked characters, house addresses and visit dates. Import preserves newer matching-address visits and existing conflicting addresses. Backups are saved in this user's plugin configuration folder.
- Renamed Reset Warnings to Show dismissed reminders again. It restores dismissed reminders without resetting recorded visits.

## 1.0.5.3

- Updated compatibility for FFXIV patch 7.57.

## 1.0.5.2

- Add `/house` as an alternative to `/htimeout`.
- Add an Enable FC estate reminders setting, enabled by default. Turning it off hides FC reminders and FC settings for all saved characters while preserving their saved housing details and personal-house reminders.

## 1.0.5.1

- Make the reminder window draggable by its title bar and give it an opaque background. Save its position after dragging.
- Skip FC-estate reminders for characters whose rank lacks Estate Hall Access, using the actual permission instead of rank names or position.
- Cache FC permissions per character after reading their own row in the game's FC Members tab. Display unknown permissions explicitly until the data is available.
- Refresh automatically detected FC addresses when a character changes FC estate.

## 1.0.5.0

- Automatically detect the logged-in character's owned personal house and FC estate, including district, ward, and plot.
- Remember each character after login and show reminders for saved alts by default. Keep personal-house visit records separate across characters.
- Record visits only inside the matching estate. Standing outside, entering an apartment, or entering an FC private chamber does not count.
- Remove apartment reminders and configuration.
- Simplify housing settings with detected addresses, clear visit information, a Detect my housing button, and manual address editing when needed.
- Fix the all-character reminder toggle, warning pagination, and repeated warning-window growth.

## 1.0.4.0

- Update for Dalamud API 15 and .NET 10.

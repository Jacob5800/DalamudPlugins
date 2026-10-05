# Local verification — 2026-10-01

- Local dev build: 0.1.3.0; build succeeded with zero warnings and zero errors.
- Standalone suite: 183 checks across 10 styles, 100 consecutive design changes, share-code round trips and invalid inputs passed.
- Live editor: confirmed visible changes for Seamless Showcase, Weapon Showcase, Golden Hour, Moonlit and Soft Studio on the current character.
- Fixed immediate native validity checks that rolled changes back before the preview rendered.
- Fixed native lighting sliders staying stale after importing a style. Moonlit displayed key RGB 150/190/255; Soft Studio displayed 248/240/222.
- Fixed strength percentage display: default now shows 85%, rather than 1%.
- Pose, expression, background, frame and decoration retained in the observed previews.
- Native Save button enabled after styling. Final Save was left to the user; persistence after saving was not tested.
- This verifies one character and editor session. Other races, poses and unlocked banner assets still need broader live coverage.
- Local DLL updated; public release/feed not published as part of these checks.

## Pose, expression and owned-design extension

- Expanded standalone suite passed 235 checks, including selection from supplied owned lists, empty-list fallback and complete portrait share-code round trips.
- Live editor changed Welcome/Smile to Standing 3/Smirk, followed by other standing poses and expressions.
- Owned background, frame and decoration changed visibly; native dropdowns synchronized.
- A reused camera crop cut off the character in a different pose. Added native camera reset for generated pose changes; Standing 6/Smile rendered centered with the new owned design.
- Both selection switches default to enabled and are saved in plugin settings.
- Generated portraits and final Save remain user-reviewed; no persistence test or public release performed.

## Release preparation

- Fixed manual Apply to capture accepted camera and banner settings for the copied share code.
- Imported poses now also require a matching current job and unlocked status.
- Final package build and the existing 235 data checks passed. These final two editor fixes were reviewed and compiled but were not exercised in-game.
- Publishing status is tracked by the repository's tagged release and Actions run, separately from these local checks.

## 1.0.8.21

- Show the MGC cap warning as a notification once per session when the configured threshold is reached, including at startup outside Keybound.
- Enlarge the floating duty keyboard button and click area.
- Retry registration after five seconds when a queue has not started, including when a party member is still leaving.
- Start with Illegal mode disabled on each plugin load; it can still be enabled for the session.

## 1.0.8.20

- Add an optional floating keyboard button inside Keybound duty to reopen the interface while the game UI is hidden.
- Fix party auto-commence when the leader is out of view or a loop is active, and match duty names without case sensitivity.

## 1.0.8.19

- Add optional automatic Keybound Brawler commencement for party members who are not the leader, disabled by default.
- Check completed-duty exits every framework update for automatic leaving and active loops, removing the previous 200 ms polling delay.

## 1.0.8.18

- Added Automatically leave duty when finished, disabled by default.
- Fixed saving Illegal mode settings with zero-millisecond intervals.

## 1.0.8.17

- Illegal mode refreshes prompts every framework update and bypasses mechanic pauses and background-key readiness waits. Normal typing is unchanged.

## 1.0.8.16

- Removed Illegal mode's fixed 1.5-second wait before resubmitting the same visible prompt.

## 1.0.8.15

- Highlight active playback controls in teal and show loop status or availability in its tooltip.
- Allow the Loop button to resume a paused loop.
- Show Play as active only when automation is running.

## 1.0.8.14

- Removed the fixed 1.5-second delay between different whole-word prompts in Illegal mode; the submission delay setting now controls pacing.
- Allow zero character intervals in Illegal mode and clarify that it bypasses character timing.

## 1.0.8.13

- Moved playback controls beside the status area.
- Simplified About to Keybound Automation and Tataru Brawler credits.

## 1.0.8.12

- Moved playback controls to the bottom-left edge of the window.
- Kept numeric values and interval labels white; only plus/minus symbols are teal-blue.

## 1.0.8.10

- Restored teal-blue navigation and window icons.
- Matched numeric input and plus/minus controls to the teal-blue interface.

## 1.0.8.9

- Fixed status text overlapping the activity ring.
- Added rounded section cards and switch-style toggles, including the red Danger switch.
- Reduced Quick Controls clutter and improved field spacing.

## 1.0.8.8

- Replaced the Cyberpunk interface with an adapted Auto Keybound Brawler layout, navigation rail and persistent playback dock.
- Added compact mode, a live prompt board and dedicated Log, Changelog and About pages.
- Illegal mode is now red and labeled Danger.

## 1.0.8.7

- Added optional Illegal mode to insert and submit a complete Keybound answer at once.
- Disabled by default; normal per-letter typing remains available.

## 1.0.8.6

- Automatically resume after temporary input-send failures once the duty input is ready again.
- Manual Pause and Stop remain respected.

## 1.0.8.5

- Added configurable MGC cap warnings, enabled at 90% by default, with an additional alert at the cap.
- Added optional English punctuation skipping in Settings, preserving required spaces, periods and commas.

## 1.0.8.4

- Use FFXIV-directed key messages for English background typing without activating the game window.
- Start play automatically on entering a new Keybound duty.
- Fix reward tracking gates and capture results before automatic dismissal.
- Show reward tracking status in the Main tab.

## 1.0.8.3

- Added session and saved totals for completed Keybound games, MGP and MGC earned.
- Detect Cursed Gaze and both Sensory Jammer variants by action ID across client languages.

## 1.0.8.2

- Prevent Keybound-specific inactivity timeouts during active automated play.
- Added a keyboard icon to the plugin window.

## 1.0.8.1

- Added an optional KB button in the server info bar to open or close Keybound Automation.
- The button is enabled by default and can be hidden in Settings.

## 1.0.8.0

- Added Keybound Automation with configurable typing speed, English and Japanese answers, and repeat queueing.

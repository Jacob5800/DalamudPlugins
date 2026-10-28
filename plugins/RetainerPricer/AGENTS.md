# Retainer Pricer release rules

- Keep Retainer Pricer work inside `plugins/RetainerPricer`.
- For every release, update `Changelog` in `RetainerPricer.json` with concise user-facing notes. Keep the project version, plugin manifest version, and packaged manifest version aligned.
- The shared feed lives at the repository root in `repo.json`. Release workflows must update only the `RetainerPricer` entry using `scripts/Update-RepoFeed.ps1`, then commit the feed update to `main`.
- Before publishing, verify the packaged plugin manifest and root feed entry have the same assembly version and changelog.
- Keep `README.md` batch-selling documentation aligned with current behavior.
- Review the plugin's `?` tab in `MainWindow.DrawHelp()` for every release and check the version label at the bottom-left of the menu.

- Publish Retainer Pricer changes directly to the stable release channel by default. Use the testing channel only when the user explicitly requests testing. Stable tags use `RetainerPricer-v<version>`; explicitly requested testing tags use `RetainerPricer-testing-v<version>`.

- For stable and testing releases, stamp `LastUpdate` with `scripts/Stamp-PackageDate.ps1` before uploading. Inspect the downloaded ZIP to confirm the packaged manifest has a positive numeric Unix timestamp in seconds. Missing/zero timestamps cause Dalamud changelogs to display 1970; a correct root feed timestamp alone does not repair that.

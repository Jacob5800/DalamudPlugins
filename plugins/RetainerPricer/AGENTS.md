# Retainer Pricer release rules

- Keep Retainer Pricer work inside `plugins/RetainerPricer`.
- For every release, update `Changelog` in `RetainerPricer.json` with concise user-facing notes. Keep the project version, plugin manifest version, and packaged manifest version aligned.
- The shared feed lives at the repository root in `repo.json`. Release workflows must update only the `RetainerPricer` entry using `scripts/Update-RepoFeed.ps1`, then commit the feed update to `main`.
- Before publishing, verify the packaged plugin manifest and root feed entry have the same assembly version and changelog.
- Keep `README.md` batch-selling documentation aligned with current behavior.
- Review the plugin's `?` tab in `MainWindow.DrawHelp()` for every release and check the version label at the bottom-left of the menu.

- Publish future Retainer Pricer changes to the testing channel first. Keep the stable feed version and download links unchanged until the user confirms the testing build works and authorizes promotion. Testing tags use `RetainerPricer-testing-v<version>`; public tags retain `RetainerPricer-v<version>`.

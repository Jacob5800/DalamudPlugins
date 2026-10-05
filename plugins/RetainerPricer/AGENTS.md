# Retainer Pricer release rules

- Keep Retainer Pricer work inside `plugins/RetainerPricer`.
- For every release, update `Changelog` in `RetainerPricer.json` with concise user-facing notes. Keep the project version, plugin manifest version, and packaged manifest version aligned.
- The shared feed lives at the repository root in `repo.json`. Release workflows must update only the `RetainerPricer` entry using `scripts/Update-RepoFeed.ps1`, then commit the feed update to `main`.
- Before publishing, verify the packaged plugin manifest and root feed entry have the same assembly version and changelog.
- Keep `README.md` batch-selling documentation aligned with current behavior.
- Review the plugin's `?` tab in `MainWindow.DrawHelp()` for every release and check the version label at the bottom-left of the menu.

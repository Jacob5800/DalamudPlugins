# Consolidated Dalamud plugins

## Canonical source and scope
- This repository is the canonical source and release location for Portrait Atelier, Strategy Board Library, Retainer Pricer, Workshoppa, and Tug of War.
- Make plugin code, version, metadata, changelog, and release changes only in the matching `plugins/<PluginName>/` folder here.
- Do not update the old standalone repositories for these plugins. They are historical copies; keep their source, workflows, and releases unchanged except for their migration notices and repository-level `AGENTS.md` instructions.
- Workshoppa source and releases belong in plugins/Workshoppa; Workshoppa-API15 is a historical copy.
- Preserve every plugin's own license and rights notice. There is no combined license.

## Changelog requirements
- For every plugin release, update `plugins/<PluginName>/CHANGELOG.md` with a user-facing entry under the released version.
- Set the plugin's embedded `<InternalName>.json` `Changelog` field to the same concise release notes, formatted as Markdown.
- Keep the matching entry in the root `repo.json` synchronized. The custom repository entry's `Changelog` is what users see in Dalamud's installer, so it must never be empty for a published plugin version.
- Describe user-visible features and fixes. Leave build, workflow, and other internal implementation details out of installer notes.
- Add the changelog before creating the version tag. The release workflow packages the manifest and copies its `Changelog` into the root feed; verify the release asset, feed version, download links, and changelog after the workflow completes.
- After the combined feed update, release workflows should post the released plugin's changelog through `scripts/Send-DiscordRelease.ps1` when the `DISCORD_WEBHOOK_URL` Actions secret is configured.
- Read the Discord webhook only from that Actions secret; never commit it or print it in logs. Notification failures must not fail a plugin release.

## Version and release alignment
- Keep each plugin's project version, JSON manifest `AssemblyVersion`, and its tag version aligned.
- Release tags use `PortraitAtelier-v<version>`, `StrategyBoardLibrary-v<version>`, `RetainerPricer-v<version>`, `Workshoppa-v<version>`, and `TugOfWar-v<version>`.
- Run the relevant build/validation workflow and inspect its result. Do not publish a version whose build failed.

- Plugins from separate repositories (including Housing Timeout Reminder) are announced by announce-external-plugin-feed.yml when their stable version changes in repo.json. The consolidated plugins retain their release-workflow announcements. Keep external feed changelogs populated; testing-only changes do not announce.


## Publication dates in Dalamud
- Every stable and testing package must contain a numeric, positive Unix timestamp in seconds in its manifest `LastUpdate`. A missing or zero value displays as 1970 in Dalamud.
- Run `scripts/Stamp-PackageDate.ps1` after packaging and before copying/uploading the release ZIP. It must update both the loose manifest and the manifest inside the ZIP.
- Before declaring publication complete, inspect the downloaded release ZIP and verify its manifest version, changelog and nonzero `LastUpdate`; verify the root feed version, links and publication date too. Preserve the stable changelog/version/links when publishing testing builds.
- New or changed release workflows, including external plugin workflows, must include this timestamp step. Do not assume a valid feed date repairs an installed package with a zero date.
- If an older installed release still shows 1970, identify its version and explain that the correction applies to newly packaged updates; do not modify user-installed files to disguise the old metadata.

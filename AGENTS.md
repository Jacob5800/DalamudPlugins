# Consolidated Dalamud plugins

## Canonical source and scope
- This repository is the canonical source and release location for Portrait Atelier, Strategy Board Library, and Retainer Pricer.
- Make plugin code, version, metadata, changelog, and release changes only in the matching `plugins/<PluginName>/` folder here.
- Do not update the old standalone repositories for these plugins. They are historical copies; keep their source, workflows, and releases unchanged except for their migration notices and repository-level `AGENTS.md` instructions.
- Workshoppa-API15 stays in its separate repository and is not part of this repo.
- Preserve every plugin's own license and rights notice. There is no combined license.

## Changelog requirements
- For every plugin release, update `plugins/<PluginName>/CHANGELOG.md` with a user-facing entry under the released version.
- Set the plugin's embedded `<InternalName>.json` `Changelog` field to the same concise release notes, formatted as Markdown.
- Keep the matching entry in the root `repo.json` synchronized. The custom repository entry's `Changelog` is what users see in Dalamud's installer, so it must never be empty for a published plugin version.
- Describe user-visible features and fixes. Leave build, workflow, and other internal implementation details out of installer notes.
- Add the changelog before creating the version tag. The release workflow packages the manifest and copies its `Changelog` into the root feed; verify the release asset, feed version, download links, and changelog after the workflow completes.

## Version and release alignment
- Keep each plugin's project version, JSON manifest `AssemblyVersion`, and its tag version aligned.
- Release tags use `PortraitAtelier-v<version>`, `StrategyBoardLibrary-v<version>`, and `RetainerPricer-v<version>`.
- Run the relevant build/validation workflow and inspect its result. Do not publish a version whose build failed.

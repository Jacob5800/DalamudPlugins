# Keybound Automation

Custom Tataru Brawler 1.0.8.0 binary distribution, displayed as **Keybound Automation** in the installer. Open `/tatarubrawler` in game.

This package preserves the supplied custom DLL unchanged, including its interface, English/Japanese typing, repeat queue controls and startup defaults. Diagnostic recording, automatic typing and MapID 1359 play are enabled at startup in this supplied build; their settings checkboxes are hidden. Stop controls remain available.

Disable any other plugin controlling Keybound Brawler before enabling this one.

## Provenance

- Supplied by the user with the original owner's permission.
- Original source: https://github.com/Roxyz0501/KeyboundPilot (Roxyz0501).
- Custom modifications credited to Tataru-business; see the preserved LICENSE and NOTICE.
- The custom source was not supplied. The original repository is a reference, not the source of this custom DLL. This repository does not claim the custom binary can be rebuilt from that original source.
- Original archive SHA-256: `20A6FA784FF086DF349DE3C70AF0409A9D3855408D93D05EC84BA7E724E3A47C`.

## Packaging

Run `./plugins/TataruBrawler/Package.ps1` from the repository root. It checks the supplied archive hash and assembly identity, preserves the DLL byte-for-byte, updates installer metadata, includes the original documentation/notices, and stamps the package date. Output: `plugins/TataruBrawler/dist/package/latest.zip`.

This is a binary packaging workflow, not a source build. Packaging checks do not establish in-game compatibility. InternalName remains `TataruBrawler` to preserve settings identity.

# Keybound Automation

Custom Tataru Brawler distribution, displayed as **Keybound Automation** in the installer. Open `/tatarubrawler` or click **KB** in the server info bar. Settings includes **Show button in the server info bar**, enabled by default.

The custom interface, English/Japanese typing, repeat queue controls and startup defaults are retained. Diagnostic recording, automatic typing and MapID 1359 play are enabled at startup; their settings checkboxes are hidden. Stop controls remain available.

Disable any other plugin controlling Keybound Brawler before enabling this one.

## Provenance

- The user supplied the custom build with the original owner's permission.
- Original source: https://github.com/Roxyz0501/KeyboundPilot (Roxyz0501).
- Custom modifications credited to Tataru-business; see LICENSE and NOTICE.
- Version 1.0.8.0 redistributed the supplied DLL byte-for-byte.
- Version 1.0.8.1 builds `src/`, reconstructed from that custom DLL using ILSpy 11.2.0.10050, with server-bar integration added. This is not a build of the newer original repository, which differs from the supplied custom build.
- Original archive is retained in `vendor/` for reference and rollback. SHA-256: `20A6FA784FF086DF349DE3C70AF0409A9D3855408D93D05EC84BA7E724E3A47C`.

## Build and package

Run `./plugins/TataruBrawler/Package.ps1` from the repository root with .NET 10 and Dalamud API 15 references available. `DalamudLibPath` may point to the SDK directory; it defaults to the local XIVLauncher dev folder. Output: `plugins/TataruBrawler/dist/package/latest.zip`.

The original custom source was not supplied. Reconstructed source compiled before and after the server-bar addition. Gameplay and native interaction still require in-game verification; compilation does not establish behavioral equivalence. InternalName remains `TataruBrawler` to preserve settings identity.

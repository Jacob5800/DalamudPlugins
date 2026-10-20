# 7.57 hotfix1 source audit (20-10-2026)

Scope: all six entries in repo.json. Published main-branch source was checked against the user-supplied DalamudAssets and Dalamud comparisons. This is a source review, not an in-game compatibility certification.

## Evidence

- https://github.com/goatcorp/DalamudAssets/pull/363 updates PDB symbols and .ver to 2026.10.16.0000.0000.
- https://github.com/goatcorp/DalamudAssets/commit/b83097c168392129508d99ba27790ee832cd3e15 increments asset version to 446.
- https://github.com/goatcorp/Dalamud/compare/f01df39634bf...71a28c8f70d1 updates ClientStructs d6397a76 to dd57986b. Native signature changes include PacketDispatcher.HandlePrepareZoningPacket and HandleMapEffectPacket. Other additions concern Mahjong, CharacterUtility, CharacterBase documentation and NumberArrayData helpers.
- https://github.com/goatcorp/Dalamud/compare/1d8da0470524...13c08b282b91 updates Lumina.Excel's experimental schema dependency.
- https://github.com/xivdev/EXDSchema/compare/f3cacf3bb7bcea2e1dcd2ce1204f8be22a6c9922...a1e24a0c72058353f30f23beb996792cdb926868 includes World.LogicalDataCenter. The upstream commit says 7.57 worlds use a single physical DC and the new link identifies logical DCs.

## Results

| Plugin | Finding | Remaining checks |
| --- | --- | --- |
| Retainer Pricer | Actionable schema concern: NativeMarketBridge.GetHomeDataCenterName reads HomeWorld.Value.DataCenter. Evaluate LogicalDataCenter for Universalis pricing scope with matching schema; do not blindly replace region logic. Custom retainer signatures and InfoProxyItemSearch hooks are not directly changed in the supplied ClientStructs diff. | Correct logical DC resolution; confirm signature resolution, local price scans, pricing scope and retainer operations in game. |
| Portrait Atelier | No direct use of the changed packet handlers or changed graphics structures. Uses AgentBannerEditor, BannerModule/UI objects and Banner Excel sheets, whose definitions are not changed in this comparison. | Capture/apply portrait and load design choices. |
| Strategy Board Library | No direct game-native hooks or Excel-sheet dependency found; unsafe clipboard interop is unrelated. | Open, search, clipboard import/export. |
| Housing Timeout Reminder | Uses HousingManager and FC info/string arrays, with no matching changes in the supplied ClientStructs diff. No custom signature hooks found. | Detect owned housing, inside-only visit tracking and FC permission refresh. |
| Workshoppa | Uses inventory, TargetSystem, UI agents and crafting/retainer sheets; no direct match to changed packet handlers/schema names. | Workshop UI, inventory reads, crafting/turn-ins and retainer deposits. |
| Tug of War | No direct ClientStructs hooks or Excel-sheet dependency found. | Roll parsing, score display and user-triggered announcements. |

## Build/release observations

Workshoppa and Tug of War release workflows pin the pre-hotfix staging SDK at a44d77c7; Housing's imported release workflow preserves that existing pin. Builds using that SDK do not establish hotfix compatibility. Refresh to a verified hotfix-compatible API 15 SDK before a hotfix release; the supplied comparisons alone do not confirm a stable rollout. Do not migrate to API16 just because an API16 preview track exists.

Housing's consolidated Release build succeeded locally with 0 warnings/errors using the installed API15 SDK. No runtime checks were performed, no tests were run, and no plugin release was published.

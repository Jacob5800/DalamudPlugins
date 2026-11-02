# Validation scope

The supplied 1.0.8.0 DLL was reconstructed with ILSpy and compiled successfully before changes. Version 1.0.8.1 compiles with the server-bar button, settings persistence and unload cleanup added. Package checks verify the archived input hash, DLL/manifest identity and version, changelog and positive publication date.

The original archive contains its author's earlier validation notes. Those earlier checks were not rerun against this reconstruction. No in-game behavior, native interaction or server-bar appearance has yet been verified for 1.0.8.1.

Version 1.0.8.2 adds the Keybound input-agent inactivity counter reset, guarded by territory 1359 and the input-agent to HUD-agent identity check. Existing active-play, pause/stop and 10-second scheduling gates are retained. It also adds the FontAwesome keyboard icon in the plugin window; the native server bar remains KB. Local compilation and package verification succeeded. Runtime timer-layout and icon rendering checks remain pending.

Version 1.0.8.3: 27 offline checks cover all three blocking action IDs, timing/reset behavior, repeated-result deduplication, delayed settlement, aborted visits, zero/capped currency, missing baselines and decreases. Build/package verification passed. Actual casts, native result-agent ownership and live currency updates still require in-game observation.

Version 1.0.8.4: local build and 34 offline checks pass, including English background character/submission progression and rejection of unrelated text, partial Enter, IME and Escape. Background window delivery, duty entry and live result/currency behavior remain pending game observation. The prior user config already had BackgroundInput=true; diagnostics showed SendFailed stops. Source review found the result-dismissal versus 100 ms reward sampling race and removed unnecessary result-agent/currency-container gates.

Version 1.0.8.5: offline checks cover punctuation enable/disable, required punctuation, original prompt identity, Japanese preservation, shortened background submission, threshold/cap transitions, suppression of repeat warnings, transient missing cap data and rearming after spending. Local compilation and packaging pass. Live CurrencyManager cap availability, warning display and shortened-answer acceptance remain pending observation.

1.0.8.6: automatic SendFailed recovery requires two seconds of stable ready input, with five-second retry spacing. Manual pause/stop, results, unsupported input and NoProgress are excluded. Partial answers enter existing clearing recovery. Offline policy checks pass; live phase transition recovery remains pending.

1.0.8.7: whole-word callback path compiled and package metadata verified; 62 existing offline checks pass. Native field insertion/submission and game acceptance remain unverified. No successful gameplay claim is made.

1.0.8.8: navigation, compact expansion/restoration, header actions and callback bindings reviewed; all original Settings, queue, manual-loop, playback, diagnostics and language controls remain reachable. Local build and 62 gameplay policy checks pass. World/gameplay code is unchanged by this UI port. Live visual layout and native mouse interaction remain pending in-game observation; compilation is not a visual test.

1.0.8.9: fixed the reported status-ring text overlap by indenting both status lines together; added measured rounded section backgrounds with draw-channel ordering and switch-style controls. Quick Controls long help moved to hover text. Build and 62 gameplay checks pass; live visual confirmation at the user's scale remains pending.

1.0.8.11: separate numeric value/label and teal step buttons; dock left inset is 16 scaled pixels. Local build and existing gameplay checks pass; live layout appearance remains pending. Required component license files restored for packaging.

1.0.8.24: build and 74 offline checks pass, including native Keybound identity without text, rejection of unrelated native duties despite a matching title, whitespace-normalized title fallback and missing identity. Party-only gates remain enabled. Logging records distinct blocked reasons or button dispatch while the confirmation is visible, at most once per second and only when the status changes. Actual party invitation acceptance remains pending in-game observation.

1.0.8.26: live diagnostics reported "not in a party" on repeated ready prompts. Added the native cross-world party membership, count and leader checks before the regular party-list branch. Keybound invitation identity and button readiness remain required. Native cross-world acceptance remains pending after installing the update. API reference: https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/UI/Info/InfoProxyCrossRealm.cs

1.0.8.27: optional Installed Plugins button uses the public plugin installer API and its InstalledPlugins page. Setting is default-off and included in snapshot creation, deserialization and updates. The duty bar remains visible with hidden game UI when either button is enabled. Build/package verification passed; native layout/click observation remains pending.

1.0.8.28: duty bar shows Playing during active typing, Paused for manual or temporary pauses, and the existing translated status otherwise. Hover shows the detailed typing state. Build/package checks passed; in-game layout remains pending.

1.0.8.29: Illegal-mode selection resolves Airbuster BNpcName row IDs once from English game data, then matches battle-NPC NameId and recognized prompt entity IDs in Keybound territory. Missing identity/prompt falls back to the existing ranking. Normal typing selection is unchanged. Native boss/barrier target acceptance remains pending.

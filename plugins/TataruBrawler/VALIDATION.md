# Validation scope

The supplied 1.0.8.0 DLL was reconstructed with ILSpy and compiled successfully before changes. Version 1.0.8.1 compiles with the server-bar button, settings persistence and unload cleanup added. Package checks verify the archived input hash, DLL/manifest identity and version, changelog and positive publication date.

The original archive contains its author's earlier validation notes. Those earlier checks were not rerun against this reconstruction. No in-game behavior, native interaction or server-bar appearance has yet been verified for 1.0.8.1.

Version 1.0.8.2 adds the Keybound input-agent inactivity counter reset, guarded by territory 1359 and the input-agent to HUD-agent identity check. Existing active-play, pause/stop and 10-second scheduling gates are retained. It also adds the FontAwesome keyboard icon in the plugin window; the native server bar remains KB. Local compilation and package verification succeeded. Runtime timer-layout and icon rendering checks remain pending.

Version 1.0.8.3: 27 offline checks cover all three blocking action IDs, timing/reset behavior, repeated-result deduplication, delayed settlement, aborted visits, zero/capped currency, missing baselines and decreases. Build/package verification passed. Actual casts, native result-agent ownership and live currency updates still require in-game observation.

Version 1.0.8.4: local build and 34 offline checks pass, including English background character/submission progression and rejection of unrelated text, partial Enter, IME and Escape. Background window delivery, duty entry and live result/currency behavior remain pending game observation. The prior user config already had BackgroundInput=true; diagnostics showed SendFailed stops. Source review found the result-dismissal versus 100 ms reward sampling race and removed unnecessary result-agent/currency-container gates.

Version 1.0.8.5: offline checks cover punctuation enable/disable, required punctuation, original prompt identity, Japanese preservation, shortened background submission, threshold/cap transitions, suppression of repeat warnings, transient missing cap data and rearming after spending. Local compilation and packaging pass. Live CurrencyManager cap availability, warning display and shortened-answer acceptance remain pending observation.

1.0.8.6: automatic SendFailed recovery requires two seconds of stable ready input, with five-second retry spacing. Manual pause/stop, results, unsupported input and NoProgress are excluded. Partial answers enter existing clearing recovery. Offline policy checks pass; live phase transition recovery remains pending.

1.0.8.7: whole-word callback path compiled and package metadata verified; 62 existing offline checks pass. Native field insertion/submission and game acceptance remain unverified. No successful gameplay claim is made.

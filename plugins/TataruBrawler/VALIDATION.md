# Validation scope

The supplied 1.0.8.0 DLL was reconstructed with ILSpy and compiled successfully before changes. Version 1.0.8.1 compiles with the server-bar button, settings persistence and unload cleanup added. Package checks verify the archived input hash, DLL/manifest identity and version, changelog and positive publication date.

The original archive contains its author's earlier validation notes. Those earlier checks were not rerun against this reconstruction. No in-game behavior, native interaction or server-bar appearance has yet been verified for 1.0.8.1.

Version 1.0.8.2 adds the Keybound input-agent inactivity counter reset, guarded by territory 1359 and the input-agent to HUD-agent identity check. Existing active-play, pause/stop and 10-second scheduling gates are retained. It also adds the FontAwesome keyboard icon in the plugin window; the native server bar remains KB. Local compilation and package verification succeeded. Runtime timer-layout and icon rendering checks remain pending.

Version 1.0.8.3: 27 offline checks cover all three blocking action IDs, timing/reset behavior, repeated-result deduplication, delayed settlement, aborted visits, zero/capped currency, missing baselines and decreases. Build/package verification passed. Actual casts, native result-agent ownership and live currency updates still require in-game observation.

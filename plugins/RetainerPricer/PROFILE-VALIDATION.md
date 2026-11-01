# 0.5.1.37 validation

- Local plugin build: passed with zero warnings/errors.
- 21 isolated checks passed using real profile/config/backup/Shift-policy source: migration preserves settings; create/switch saves outgoing changes; settings, lists and quantities are independent; serialized round trip; caches/library survive imports; export excludes registry/cache; rename/delete/name validation; malformed profile leaves current settings intact; standalone/full-cycle Shift gates and disabled/idle/outside-bell cases.
- Reviewed Profiles UI busy gates, deletion confirmation, default preservation, automatic save callback, Settings help and dynamic assembly version label.
- Withdrawal now opens the listing-owned menu once, waits on subsequent controller ticks, rechecks listing/retainer identity, and submits the selected action once. Existing returned-stack and sale verification remain. Reference: FFXIVClientStructs AgentInventoryContext and Dalamud ContextMenu implementation.
- Pending in-game: profile UI appearance, live setting side effects, Shift during reward/assignment dialogs, and approved withdrawal/sale. No native success or timing/performance improvement is claimed by compilation or isolated checks.

Materia extraction/retrieval uses the same open-once, select-on-subsequent-tick sequence. The controller waits for an owned ready menu and rechecks the exact bag/slot/item and protection state. It still waits for spiritbond reset or meld changes before desynthesis. Live first-attempt behavior remains pending.

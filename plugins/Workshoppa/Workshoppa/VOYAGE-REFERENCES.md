# Voyage automation references

Original Workshoppa by Liza Carvelli. Existing Workshoppa and LLib attribution/licenses remain unchanged.

Native voyage callback shapes, point-input structures, component inventory offsets, menu translations and UI row layouts are adapted from AutoRetainer, copyright 2023 Puni.sh, BSD-3-Clause. The complete license accompanies this file as AutoRetainer-LICENSE.md.

Reference revision: PunishXIV/AutoRetainer cc020df0efee3ea65a45117e2b89999108c702e8.
- https://github.com/PunishXIV/AutoRetainer/tree/cc020df0efee3ea65a45117e2b89999108c702e8/AutoRetainer/Modules/Voyage
- https://github.com/PunishXIV/AutoRetainer/blob/cc020df0efee3ea65a45117e2b89999108c702e8/AutoRetainer/Internal/Memory.cs
- https://github.com/PunishXIV/AutoRetainer/blob/cc020df0efee3ea65a45117e2b89999108c702e8/AutoRetainer/Lang.cs

Also checked FFXIVClientStructs WorkshopTerritory, HousingManager, AgentSubmersibleExploration and AddonAirShipExploration, and ECommons AtkReader argument order. No AutoRetainer runtime dependency or hook registration is used.

Native event delivery, repairs, route selection and reward collection remain pending in-game observation. Compilation and simulated tests do not establish live success.


Optimisation models, loot tables, part mappings and calculation code adapted from ofnature/Argus (MIT). See Argus-LICENSE.md and Argus-THIRD-PARTY-NOTICES.txt. The latter preserves its sources, including Infi/SubmarineTracker and community voyage spreadsheets. Adaptations rename namespaces, load local Lumina sheets, remove plugin services from pure calculations and use Workshoppa-owned UI/controller state. Part-picker event shapes follow Argus PartsInterop; build recommendations and execution remain separate.

using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Memory;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using NativeObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace RetainerPricer;

internal sealed record SubmarineState(int Index, string Name, DateTimeOffset? Returns, IReadOnlyList<BrokenPart> Broken, bool PartsKnown)
{
    public bool NeedsRepair => Broken.Count > 0;
}

/// <summary>
/// At the voyage control panel: for each submersible whose voyage is complete or that is idle, open its report
/// (the previous voyage log when idle) and redeploy it on the same route. A finished report is finalized first, parts
/// at 0% are repaired, and nothing is bought or re-routed; anything unexpected stops it.
/// </summary>
internal sealed unsafe class SubmarineController(IGameGui gui, IDataManager data, IObjectTable objects,
    ITargetManager targets, ICondition condition, NativeMarketBridge bridge, Func<bool> otherBusy,
    Func<bool> autoRetainerLoaded, IDalamudPluginInterface pi)
{
    private const uint PanelNameRow = 2005274, ManagementTalkRow = 721343;
    private enum Stage { Idle, Travel, OpenPanel, ChooseSubmarines, PickVessel, Finalize, AfterFinalize, VesselMenu, Repair, Report, Confirm, Verify, Leave }

    private Stage stage;
    private DateTimeOffset next, deadline;
    private readonly HashSet<int> handled = [];
    private readonly List<string> problems = [];
    private SubmarineState? vessel;
    private int redeployed, finalized;
    private int repairSlot = -1, repairTries;
    private bool unrepairedApproved;
    private DateTimeOffset travelDeadline;
    private DateTimeOffset? workshopRequestedAt;
    private bool walking;

    /// <summary>Set while the player decides about a submersible there are not enough repair materials for.</summary>
    public string? MaterialsQuestion { get; private set; }

    public void AnswerMaterials(bool continueAnyway)
    {
        if (MaterialsQuestion is null || !IsRunning) return;
        MaterialsQuestion = null;
        var now = DateTimeOffset.UtcNow;
        deadline = now.AddSeconds(30);
        if (continueAnyway) { unrepairedApproved = true; Status = $"Continuing with {vessel!.Name} without repairing."; }
        else unrepairedSkip = true;
        bridge.LogAutomationDiagnostic($"Submarines: {vessel!.Name} short of repair materials; chose {(continueAnyway ? "continue anyway" : "skip")}.");
    }
    private bool unrepairedSkip;

    public bool IsRunning => stage != Stage.Idle;
    public bool AutoRetainerLoaded => autoRetainerLoaded();
    public string Status { get; private set; } = "Submarines are idle.";

    public IReadOnlyList<SubmarineState> Read()
    {
        var result = new List<SubmarineState>();
        var housing = HousingManager.Instance();
        if (housing == null || housing->WorkshopTerritory == null) return result;
        var inventory = InventoryManager.Instance();
        var parts = inventory == null ? null : inventory->GetInventoryContainer(InventoryType.HousingInteriorPlacedItems2);
        for (var index = 0; index < 4; index++)
        {
            var sub = housing->WorkshopTerritory->Submersible.DataPointers[index].Value;
            if (sub == null || string.IsNullOrEmpty(sub->NameString)) continue;
            var broken = new List<BrokenPart>();
            var known = parts != null && parts->IsLoaded;
            if (known)
                for (var part = 0; part < 4; part++)
                {
                    var slot = parts->GetInventorySlot(index * 5 + part);
                    if (slot == null || slot->IsEmpty()) { known = false; continue; }
                    if (slot->Condition == 0) broken.Add(new BrokenPart(part, RepairCost(slot->ItemId)));
                }
            result.Add(new SubmarineState(index, sub->NameString,
                sub->ReturnTime == 0 ? null : DateTimeOffset.FromUnixTimeSeconds(sub->ReturnTime), broken, known));
        }
        return result;
    }

    public void Start()
    {
        if (IsRunning) return;
        // AutoRetainer drives the same panel on its own; two plugins answering the same windows is unsafe.
        if (AutoRetainerLoaded) { Status = "Disable AutoRetainer before redeploying submarines; it controls the same voyage control panel."; return; }
        if (otherBusy()) { Status = "Wait for other Retainer Pricer operations to finish."; return; }
        if (bridge.IsClientStateUnavailable) { Status = "Log in first."; return; }
        var atPanel = PanelInReach();
        if (!atPanel)
        {
            // Getting there needs both: Lifestream enters the workshop, vnavmesh walks to the panel.
            try
            {
                _ = pi.GetIpcSubscriber<bool>("Lifestream.IsBusy").InvokeFunc();
                _ = pi.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning").InvokeFunc();
            }
            catch { Status = "Enable Lifestream and vnavmesh to travel to the workshop, or stand next to the voyage control panel."; return; }
        }
        handled.Clear();
        problems.Clear();
        vessel = null;
        redeployed = finalized = 0;
        var now = DateTimeOffset.UtcNow;
        workshopRequestedAt = null;
        walking = false;
        travelDeadline = now.AddMinutes(5);
        if (atPanel) Move(Stage.OpenPanel, now, "Opening the voyage control panel...");
        else Move(Stage.Travel, now, "Going to your free company workshop...");
    }

    public void Stop(string reason = "Submarine redeploy stopped.")
    {
        if (!IsRunning) return;
        EndTravel();
        stage = Stage.Idle;
        MaterialsQuestion = null;
        Status = $"{reason} {redeployed} redeployed.";
        bridge.LogAutomationDiagnostic($"Submarines: {Status}");
    }

    public void Update()
    {
        if (!IsRunning) return;
        var now = DateTimeOffset.UtcNow;
        if (now < next) return;
        next = now.AddMilliseconds(700);
        // The player may take any time to answer.
        if (MaterialsQuestion is not null) { deadline = now.AddSeconds(30); return; }
        if (bridge.IsClientStateUnavailable || condition[ConditionFlag.InCombat] || condition[ConditionFlag.BoundByDuty])
        { Stop("Submarine redeploy stopped: character unavailable."); return; }
        if (now > deadline) { Stop($"Submarine redeploy stopped: the game did not respond while '{Status}'. Check the open window."); return; }
        if (AutoRetainerLoaded) { Stop("Submarine redeploy stopped: AutoRetainer was enabled."); return; }
        try { if (!SkipCutscene(now)) Tick(now); }
        catch (Exception ex) { Stop("Submarine redeploy stopped: " + ex.Message); }
    }

    private void Tick(DateTimeOffset now)
    {
        switch (stage)
        {
            case Stage.Travel:
                Travel(now);
                break;

            case Stage.OpenPanel:
                if (MenuContains(ManagementText())) { Move(Stage.ChooseSubmarines, now, "Choosing submersible management..."); return; }
                if (condition[ConditionFlag.OccupiedInEvent] || condition[ConditionFlag.OccupiedInQuestEvent]) return;
                if (!UsePanel()) { Stop("Stand next to the voyage control panel and start again."); return; }
                next = now.AddSeconds(2);
                break;

            case Stage.ChooseSubmarines:
                if (Select(text => text == ManagementText())) Move(Stage.PickVessel, now, "Waiting for the submersible list...");
                break;

            case Stage.PickVessel:
                // The list is open once its closing "Nothing." entry is there.
                if (!MenuContains(Dialogue(6))) return;
                var states = Read();
                if (states.Count == 0) return;
                // Finished voyages open their report directly; idle ones go through their previous voyage log.
                var due = states.FirstOrDefault(sub => !handled.Contains(sub.Index) && (sub.Returns is null || sub.Returns <= now));
                if (due is null)
                {
                    if (Select(text => text == Dialogue(6))) Move(Stage.Leave, now, "Closing the voyage control panel...");
                    return;
                }
                handled.Add(due.Index);
                unrepairedApproved = unrepairedSkip = false;
                if (!Select(text => text.StartsWith(due.Name + ".", StringComparison.Ordinal)))
                { Stop($"Could not pick {due.Name} from the list."); return; }
                vessel = due;
                if (due.Returns is null) Move(Stage.VesselMenu, now, $"Opening {due.Name}...");
                else Move(Stage.Finalize, now, $"Opening {due.Name}'s voyage report...");
                break;

            case Stage.Finalize:
                var finished = Addon("AirShipExplorationResult");
                if (finished == null) return;
                // 0 finalizes: the rewards are collected before anything else is decided.
                Fire(finished, 0);
                finalized++;
                Move(Stage.AfterFinalize, now, $"Finalizing {vessel!.Name}'s voyage...");
                next = now.AddSeconds(2);
                break;

            case Stage.AfterFinalize:
                if (Addon("AirShipExplorationResult") != null) return;
                if (MenuContains(Dialogue(8))) { Move(Stage.VesselMenu, now, $"Checking {vessel!.Name}'s parts..."); return; }
                // Back on the list instead: open the same submersible again.
                if (MenuContains(Dialogue(6)) && Select(text => text.StartsWith(vessel!.Name + ".", StringComparison.Ordinal)))
                    Move(Stage.VesselMenu, now, $"Checking {vessel!.Name}'s parts...");
                break;

            case Stage.VesselMenu:
                // The vessel's own menu offers a deployment, and the previous log only when it has one.
                if (!MenuContains(Dialogue(8))) return;
                var current = Read().FirstOrDefault(sub => sub.Index == vessel!.Index);
                if (current is null || !current.PartsKnown)
                { Stop($"{vessel!.Name}'s parts could not be read, so it was not redeployed."); return; }
                if (current.NeedsRepair && !unrepairedApproved)
                {
                    var shortfall = SubmarineRepairPlan.Shortfall(current.Broken, CarriedRepairMaterials());
                    if (shortfall > 0)
                    {
                        if (unrepairedSkip)
                            LeaveVessel(now, $"{vessel!.Name} is short {shortfall} magitek repair material(s) and was left docked");
                        else
                            MaterialsQuestion = $"You don't have enough magitek repair materials for {vessel!.Name}: {shortfall} short, with {current.Broken.Count} part(s) at 0%.";
                        return;
                    }
                    if (!Select(text => text == Dialogue(11))) return;
                    repairSlot = -1;
                    repairTries = 0;
                    Move(Stage.Repair, now, $"Repairing {vessel!.Name}...");
                    return;
                }
                if (Select(text => text == Dialogue(9))) { Move(Stage.Report, now, $"Reading {vessel!.Name}'s previous voyage log..."); return; }
                LeaveVessel(now, $"{vessel!.Name} has no previous voyage to repeat");
                break;

            case Stage.Repair:
                var prompt = (AddonSelectYesno*)Addon("SelectYesno");
                if (prompt != null)
                {
                    var asked = MemoryHelper.ReadSeStringNullTerminated((nint)prompt->PromptText->NodeText.StringPtr.Value).TextValue;
                    if (!IsRepairPrompt(asked)) { Stop($"Unexpected question while repairing, left unanswered: {asked}"); return; }
                    Fire((AtkUnitBase*)prompt, 0);
                    next = now.AddSeconds(2);
                    return;
                }
                var supply = Addon("CompanyCraftSupply");
                if (supply == null) return;
                var damaged = Read().FirstOrDefault(sub => sub.Index == vessel!.Index);
                if (damaged is null || !damaged.PartsKnown) { Stop("The parts could not be read during the repair."); return; }
                if (!damaged.NeedsRepair)
                {
                    Fire(supply, 5);
                    Move(Stage.VesselMenu, now, $"{vessel!.Name} repaired.");
                    next = now.AddSeconds(2);
                    return;
                }
                var part = damaged.Broken[0].Slot;
                repairTries = part == repairSlot ? repairTries + 1 : 1;
                if (repairTries > 3) { Stop($"{vessel!.Name}'s part {part + 1} did not repair."); return; }
                repairSlot = part;
                RequestRepair(supply, part);
                deadline = now.AddSeconds(30);
                next = now.AddSeconds(2);
                break;

            case Stage.Report:
                var report = Addon("AirShipExplorationResult");
                if (report == null) return;
                // 1 is "redeploy": the same route is offered for confirmation.
                Fire(report, 1);
                Move(Stage.Confirm, now, $"Redeploying {vessel!.Name} on the same route...");
                next = now.AddSeconds(2);
                break;

            case Stage.Confirm:
                var detail = Addon("AirShipExplorationDetail");
                if (detail == null) return;
                if (!TryReadFuel(detail, out var required, out var carried))
                { Stop("The ceruleum tank count could not be read; nothing was confirmed."); return; }
                if (required > carried)
                {
                    Fire(detail, -1);
                    Stop($"Not enough ceruleum tanks for {vessel!.Name}: {required} needed, {carried} carried.");
                    return;
                }
                Fire(detail, 0);
                Move(Stage.Verify, now, $"Waiting for {vessel!.Name} to leave...");
                next = now.AddSeconds(3);
                break;

            case Stage.Verify:
                var sent = Read().FirstOrDefault(sub => sub.Index == vessel!.Index);
                if (sent?.Returns is { } returns && returns > now)
                {
                    redeployed++;
                    bridge.LogAutomationDiagnostic($"Submarines: {vessel!.Name} redeployed, returns {returns:O}.");
                    Move(Stage.PickVessel, now, "Checking the next submersible...");
                }
                break;

            case Stage.Leave:
                if (!condition[ConditionFlag.OccupiedInEvent] && !condition[ConditionFlag.OccupiedInQuestEvent])
                {
                    stage = Stage.Idle;
                    Status = $"Submarines done: {finalized} report(s) finalized, {redeployed} redeployed." + (problems.Count > 0 ? $" {string.Join("; ", problems)}." : "");
                    bridge.LogAutomationDiagnostic($"Submarines: {Status}");
                    return;
                }
                SelectLeave();
                next = now.AddSeconds(1);
                break;
        }
    }

    private void LeaveVessel(DateTimeOffset now, string why)
    {
        if (!SelectLeave()) return;
        problems.Add(why);
        Move(Stage.PickVessel, now, why + ". Checking the next submersible...");
    }

    // The item's data row points at the submersible part, which carries its fixed repair cost.
    private int RepairCost(uint itemId)
    {
        var item = data.GetExcelSheet<Item>().GetRowOrDefault(itemId);
        var part = item is { } found ? data.GetExcelSheet<SubmarinePart>().GetRowOrDefault(found.AdditionalData.RowId) : null;
        // An unknown part counts as unaffordable rather than free.
        return part is { RepairMaterials: > 0 } known ? known.RepairMaterials : int.MaxValue / 4;
    }

    private static int CarriedRepairMaterials()
    {
        var inventory = InventoryManager.Instance();
        return inventory == null ? 0 : inventory->GetInventoryItemCount(SubmarineRepairPlan.RepairMaterialsItemId);
    }

    private bool IsRepairPrompt(string asked)
    {
        // The game's wording with its two item names left out; the longest remaining piece identifies it.
        var wording = data.GetExcelSheet<Addon>().GetRow(6587).Text.ExtractText().Split("  ")
            .OrderByDescending(piece => piece.Length).First().Trim().TrimEnd('?', ' ');
        return wording.Length > 5 && asked.Contains(wording, StringComparison.OrdinalIgnoreCase);
    }

    private static void RequestRepair(AtkUnitBase* supply, int slot)
    {
        var values = stackalloc AtkValue[6];
        int[] arguments = [3, 0, slot, 0, 0, 0];
        for (var i = 0; i < arguments.Length; i++) values[i] = new AtkValue { Type = AtkValueType.Int, Int = arguments[i] };
        supply->FireCallback(6, values, true);
    }

    private bool SelectLeave()
    {
        var quit = data.GetExcelSheet<Addon>().GetRow(5).Text.ToString();
        var cancel = data.GetExcelSheet<Addon>().GetRow(2).Text.ToString();
        return Select(text => text == quit || text == cancel);
    }

    private void Move(Stage to, DateTimeOffset now, string status)
    {
        stage = to;
        deadline = now.AddSeconds(30);
        next = now.AddMilliseconds(700);
        Status = status;
        bridge.LogAutomationDiagnostic($"Submarines: {status}");
    }

    private string ManagementText() => data.GetExcelSheet<CustomTalk>().GetRow(ManagementTalkRow).MainOption.ToString();

    private string Dialogue(uint row) => data.GetExcelSheet<RawRow>(name: "custom/004/HouFixCompanySubmarine_00447")
        .GetRow(row).ReadStringColumn(1).ExtractText();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr key, IntPtr state);
    private const uint KeyDown = 0x0100, KeyUp = 0x0101;
    private const int EscapeKey = 0x1B;

    // Deploying plays a departure scene. Escape opens the game's skip prompt, whose first entry is "yes".
    // Only ever acts while the game reports a cutscene, so no key reaches ordinary windows.
    private bool SkipCutscene(DateTimeOffset now)
    {
        if (!condition[ConditionFlag.OccupiedInCutSceneEvent] && !condition[ConditionFlag.WatchingCutscene] &&
            !condition[ConditionFlag.WatchingCutscene78]) return false;
        deadline = now.AddSeconds(60);
        var prompt = Addon("SelectString");
        if (prompt != null && MenuEntries().Count == 2)
        {
            Fire(prompt, 0);
            bridge.LogAutomationDiagnostic("Submarines: skipping the cutscene.");
        }
        else
        {
            var window = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (window != IntPtr.Zero)
            {
                PostMessage(window, KeyDown, EscapeKey, IntPtr.Zero);
                PostMessage(window, KeyUp, EscapeKey, IntPtr.Zero);
            }
        }
        next = now.AddSeconds(1);
        return true;
    }

    private void Travel(DateTimeOffset now)
    {
        if (now > travelDeadline) { Stop($"The trip to the workshop took too long while '{Status}'."); return; }
        // Loading and Lifestream's own steps take as long as they take, within the overall limit.
        deadline = now.AddSeconds(30);
        if (condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51] || condition[ConditionFlag.Casting])
        {
            if (!condition[ConditionFlag.Casting]) walking = false;
            if (workshopRequestedAt is not null) workshopRequestedAt = now;
            return;
        }
        if (objects.LocalPlayer == null) return;
        if (pi.GetIpcSubscriber<bool>("Lifestream.IsBusy").InvokeFunc())
        {
            if (workshopRequestedAt is not null) workshopRequestedAt = now;
            return;
        }
        if (PanelInReach())
        {
            EndTravel();
            Move(Stage.OpenPanel, now, "Opening the voyage control panel...");
            return;
        }
        if (NearestPanel() is { } panel)
        {
            if (walking || !pi.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady").InvokeFunc()) return;
            if (!pi.GetIpcSubscriber<System.Numerics.Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo").InvokeFunc(panel.Position, false))
            { Stop("No walking path to the voyage control panel was found."); return; }
            walking = true;
            Status = "Walking to the voyage control panel...";
            return;
        }
        if (workshopRequestedAt is null)
        {
            pi.GetIpcSubscriber<string, object>("Lifestream.ExecuteCommand").InvokeAction("ws");
            workshopRequestedAt = now;
            Status = "Asking Lifestream to go to your free company workshop...";
            bridge.LogAutomationDiagnostic($"Submarines: {Status}");
        }
        else if (now - workshopRequestedAt.Value > TimeSpan.FromSeconds(30))
            Stop("No voyage control panel was found. Check that this character can enter a free company workshop.");
    }

    private void EndTravel()
    {
        try
        {
            if (walking) pi.GetIpcSubscriber<object>("vnavmesh.Path.Stop").InvokeAction();
            if (stage == Stage.Travel && workshopRequestedAt is not null && pi.GetIpcSubscriber<bool>("Lifestream.IsBusy").InvokeFunc())
                pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();
        }
        catch (Exception ex) { bridge.LogAutomationDiagnostic($"Submarines: travel could not be stopped: {ex.Message}"); }
        walking = false;
        workshopRequestedAt = null;
    }

    private Dalamud.Game.ClientState.Objects.Types.IGameObject? NearestPanel()
    {
        var local = objects.LocalPlayer;
        var name = data.GetExcelSheet<EObjName>().GetRowOrDefault(PanelNameRow)?.Singular.ToString();
        if (local == null || string.IsNullOrEmpty(name)) return null;
        return objects.Where(candidate => candidate.ObjectKind == ObjectKind.EventObj && candidate.IsTargetable &&
                string.Equals(candidate.Name.TextValue, name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => System.Numerics.Vector3.Distance(local.Position, candidate.Position)).FirstOrDefault();
    }

    private bool PanelInReach() => objects.LocalPlayer is { } local && NearestPanel() is { } panel &&
        System.Numerics.Vector3.Distance(local.Position, panel.Position) <= 4.6f;

    private bool UsePanel()
    {
        var panel = NearestPanel();
        if (panel == null || !PanelInReach()) return false;
        targets.Target = panel;
        TargetSystem.Instance()->InteractWithObject((NativeObject*)panel.Address);
        return true;
    }

    private AtkUnitBase* Addon(string name)
    {
        var addon = (AtkUnitBase*)gui.GetAddonByName(name).Address;
        return addon != null && addon->IsReady && addon->IsVisible ? addon : null;
    }

    private List<string> MenuEntries()
    {
        var entries = new List<string>();
        var menu = (AddonSelectString*)Addon("SelectString");
        if (menu == null) return entries;
        var popup = menu->PopupMenu.PopupMenu;
        if (popup.EntryNames == null || popup.EntryCount is <= 0 or > 64) return entries;
        for (var index = 0; index < popup.EntryCount; index++)
        {
            var name = popup.EntryNames[index].Value;
            entries.Add(name == null ? "" : MemoryHelper.ReadSeStringNullTerminated((nint)name).TextValue);
        }
        return entries;
    }

    private bool MenuContains(string text) => MenuEntries().Contains(text);

    private bool Select(Func<string, bool> match)
    {
        var entries = MenuEntries();
        var found = entries.Select((text, index) => (text, index)).Where(entry => match(entry.text)).ToList();
        if (found.Count != 1) return false;
        Fire(Addon("SelectString"), found[0].index);
        return true;
    }

    private static void Fire(AtkUnitBase* addon, int value)
    {
        if (addon == null) return;
        var argument = new AtkValue { Type = AtkValueType.Int, Int = value };
        addon->FireCallback(1, &argument, true);
    }

    // The deployment window shows the tank cost as "required/carried".
    private static bool TryReadFuel(AtkUnitBase* detail, out int required, out int carried)
    {
        required = carried = 0;
        if (detail->AtkValues == null || detail->AtkValuesCount < 2) return false;
        var value = detail->AtkValues[1];
        if (value.Type is not (AtkValueType.String or AtkValueType.ManagedString or AtkValueType.ConstString) ||
            value.String.Value == null) return false;
        var text = MemoryHelper.ReadSeStringNullTerminated((nint)value.String.Value).TextValue.Split('/');
        return text.Length == 2 && int.TryParse(text[0].Trim(), out required) && int.TryParse(text[1].Trim(), out carried);
    }
}

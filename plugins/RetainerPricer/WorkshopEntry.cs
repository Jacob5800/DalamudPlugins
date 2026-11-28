using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
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

/// <summary>
/// Gets the character into its free company workshop: through the estate hall door and the additional chambers door.
/// Lifestream is only used to teleport to the house, because it walks in only when a house path was registered there.
/// </summary>
internal sealed unsafe class WorkshopEntry(IGameGui gui, IDataManager data, IObjectTable objects, ITargetManager targets,
    IClientState client, IDalamudPluginInterface pi, NativeMarketBridge bridge)
{
    private const uint EntranceRow = 2002737, ChambersDoorRow = 2004353, EnterHallWarpRow = 131148, WorkshopMenuRow = 11;
    private const string WorkshopMenuSheet = "custom/001/CmnDefHousingPersonalRoomEntrance_00178";

    private bool commandSent;
    private DateTimeOffset? progressAt;
    private DateTimeOffset walkStartedAt, next;

    public bool Walking { get; private set; }
    public string Status { get; private set; } = "";

    public void Reset()
    {
        Walking = commandSent = false;
        progressAt = null;
        next = default;
        Status = "";
    }

    /// <summary>Call while loading or while Lifestream works: neither counts as being stuck, and both end a path.</summary>
    public void Waiting(DateTimeOffset now)
    {
        if (progressAt is not null) progressAt = now;
        Walking = false;
    }

    public void Stop()
    {
        try
        {
            if (Walking) pi.GetIpcSubscriber<object>("vnavmesh.Path.Stop").InvokeAction();
            if (commandSent && pi.GetIpcSubscriber<bool>("Lifestream.IsBusy").InvokeFunc())
                pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();
        }
        catch (Exception ex) { bridge.LogAutomationDiagnostic($"Workshop travel could not be stopped: {ex.Message}"); }
        Reset();
    }

    /// <summary>One step towards the workshop. Null while under way, otherwise why it cannot be reached.</summary>
    public string? Step(DateTimeOffset now)
    {
        if (objects.LocalPlayer is not { } local || now < next) return null;

        // Inside the house: the additional chambers door offers the workshop.
        var workshop = data.GetExcelSheet<RawRow>(name: WorkshopMenuSheet).GetRow(WorkshopMenuRow).ReadStringColumn(1).ExtractText();
        if (MenuIndex(workshop) is { } entry)
        {
            Fire(Addon("SelectString"), entry);
            return Progress(now, "Entering the company workshop...");
        }
        var question = (AddonSelectYesno*)Addon("SelectYesno");
        if (question != null)
        {
            var asked = MemoryHelper.ReadSeStringNullTerminated((nint)question->PromptText->NodeText.StringPtr.Value).TextValue;
            if (asked.Trim() != data.GetExcelSheet<Warp>().GetRow(EnterHallWarpRow).Question.ExtractText().Trim())
                return $"an unexpected question was left unanswered: {asked}";
            Fire((AtkUnitBase*)question, 0);
            return Progress(now, "Entering the estate hall...");
        }
        if (Nearest(ChambersDoorRow) is { } chambers) return UseDoor(chambers, now, "Walking to the additional chambers door...");

        // Outside: only this character's own free company door, or the one just teleported to, never a neighbour's.
        if (Nearest(EntranceRow) is { } door && (OnOwnPlot() ||
                commandSent && System.Numerics.Vector3.Distance(local.Position, door.Position) <= 20))
            return UseDoor(door, now, "Walking to your free company house door...");

        if (!commandSent)
        {
            pi.GetIpcSubscriber<string, object>("Lifestream.ExecuteCommand").InvokeAction("fc");
            commandSent = true;
            next = now.AddSeconds(5);
            progressAt = now;
            return Note("Asking Lifestream to go to your free company house...");
        }
        return progressAt is { } last && now - last > TimeSpan.FromSeconds(45)
            ? "no way into the workshop was found; check that this character's free company has a house with a workshop"
            : null;
    }

    /// <summary>Walks until within reach of the target. True once there; used for the panel or bell inside, too.</summary>
    public bool WalkTowards(IGameObject target, DateTimeOffset now, string status, out string? failure)
    {
        failure = null;
        if (objects.LocalPlayer is not { } local) return false;
        if (System.Numerics.Vector3.Distance(local.Position, target.Position) <= 3.5f)
        {
            if (Walking) { pi.GetIpcSubscriber<object>("vnavmesh.Path.Stop").InvokeAction(); Walking = false; }
            return true;
        }
        // A path that ended short is asked for again after a while.
        if (Walking && now - walkStartedAt < TimeSpan.FromSeconds(10)) return false;
        if (!pi.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady").InvokeFunc()) { Note("Waiting for the navigation mesh..."); return false; }
        if (!pi.GetIpcSubscriber<System.Numerics.Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo").InvokeFunc(target.Position, false))
        { failure = "no walking path was found"; return false; }
        Walking = true;
        walkStartedAt = now;
        progressAt = now;
        Note(status);
        return false;
    }

    private string? UseDoor(IGameObject door, DateTimeOffset now, string status)
    {
        if (!WalkTowards(door, now, status, out var failure)) return failure;
        targets.Target = door;
        TargetSystem.Instance()->InteractWithObject((NativeObject*)door.Address);
        next = now.AddSeconds(2);
        return null;
    }

    private string? Progress(DateTimeOffset now, string status)
    {
        progressAt = now;
        Walking = false;
        next = now.AddSeconds(2);
        return Note(status);
    }

    private string? Note(string status)
    {
        if (status != Status) bridge.LogAutomationDiagnostic($"Workshop travel: {status}");
        Status = status;
        return null;
    }

    private bool OnOwnPlot()
    {
        var housing = HousingManager.Instance();
        if (housing == null) return false;
        var owned = HousingManager.GetOwnedHouseId(EstateType.FreeCompanyEstate);
        return owned.TerritoryTypeId == client.TerritoryType && owned.WardIndex == housing->GetCurrentWard() &&
            owned.PlotIndex == housing->GetCurrentPlot();
    }

    private IGameObject? Nearest(uint nameRow)
    {
        var local = objects.LocalPlayer;
        var name = data.GetExcelSheet<EObjName>().GetRowOrDefault(nameRow)?.Singular.ToString();
        if (local == null || string.IsNullOrEmpty(name)) return null;
        return objects.Where(candidate => candidate.ObjectKind == ObjectKind.EventObj && candidate.IsTargetable &&
                string.Equals(candidate.Name.TextValue, name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => System.Numerics.Vector3.Distance(local.Position, candidate.Position)).FirstOrDefault();
    }

    private AtkUnitBase* Addon(string name)
    {
        var addon = (AtkUnitBase*)gui.GetAddonByName(name).Address;
        return addon != null && addon->IsReady && addon->IsVisible ? addon : null;
    }

    private int? MenuIndex(string text)
    {
        var menu = (AddonSelectString*)Addon("SelectString");
        if (menu == null) return null;
        var popup = menu->PopupMenu.PopupMenu;
        if (popup.EntryNames == null || popup.EntryCount is <= 0 or > 64) return null;
        for (var index = 0; index < popup.EntryCount; index++)
        {
            var name = popup.EntryNames[index].Value;
            if (name != null && MemoryHelper.ReadSeStringNullTerminated((nint)name).TextValue == text) return index;
        }
        return null;
    }

    private static void Fire(AtkUnitBase* addon, int value)
    {
        if (addon == null) return;
        var argument = new AtkValue { Type = AtkValueType.Int, Int = value };
        addon->FireCallback(1, &argument, true);
    }
}
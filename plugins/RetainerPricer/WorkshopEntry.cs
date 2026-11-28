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
/// Lifestream is asked for the workshop; it walks in only when a house path is registered, so the doors are also handled here.
/// </summary>
internal sealed unsafe class WorkshopEntry(IGameGui gui, IDataManager data, IObjectTable objects, ITargetManager targets,
    IClientState client, IDalamudPluginInterface pi, NativeMarketBridge bridge)
{
    private const uint EntranceRow = 2002737, ChambersDoorRow = 2004353, EnterHallWarpRow = 131148, WorkshopMenuRow = 11;
    private const string WorkshopMenuSheet = "custom/001/CmnDefHousingPersonalRoomEntrance_00178";

    private bool commandSent;
    private DateTimeOffset? progressAt;
    private DateTimeOffset walkStartedAt, next;
    private System.Numerics.Vector3 lastPosition;

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
        // A door already in reach is used at once: that is where Lifestream's registered paths stop.
        var chambers = Nearest(ChambersDoorRow);
        if (chambers != null && InReach(local, chambers)) return UseDoor(chambers, now, "Opening the additional chambers door...");
        var nearestDoor = Nearest(EntranceRow);
        var onOwnPlot = OnOwnPlot();
        if (nearestDoor != null && InReach(local, nearestDoor) && (onOwnPlot || commandSent))
            return UseDoor(nearestDoor, now, "Opening your free company house door...");

        // Lifestream first: with the house paths registered it walks to each door, which vnavmesh cannot.
        // It is asked once; afterwards it is asked again only when the character is back outside the house.
        if (!commandSent)
        {
            pi.GetIpcSubscriber<string, object>("Lifestream.ExecuteCommand").InvokeAction("ws");
            commandSent = true;
            next = now.AddSeconds(5);
            progressAt = now;
            return Note("Asking Lifestream to take you to your free company workshop...");
        }

        // Lifestream is done and no door is in reach: walk there ourselves as a last resort.
        if (chambers != null) return UseDoor(chambers, now, "Walking to the additional chambers door...");
        var doorDistance = nearestDoor == null ? -1 : System.Numerics.Vector3.Distance(local.Position, nearestDoor.Position);
        if (nearestDoor != null && (onOwnPlot || doorDistance <= 60))
        {
            var failure = UseDoor(nearestDoor, now, "Walking to your free company house door...");
            return failure is null ? null
                : $"{failure}; register Path to house and Path to workshop for this character in Lifestream (House Registration > Free Company House)";
        }
        Note($"Looking for your free company house door (territory {client.TerritoryType}, own plot {onOwnPlot}, nearest entrance {(doorDistance < 0 ? "none" : $"{doorDistance:0} yalms")})...");
        return progressAt is { } last && now - last > TimeSpan.FromSeconds(45)
            ? "no way into the workshop was found; check that this character's free company has a house with a workshop"
            : null;
    }

    private static bool InReach(IGameObject local, IGameObject door) =>
        new System.Numerics.Vector2(local.Position.X - door.Position.X, local.Position.Z - door.Position.Z).Length() <= 4.5f &&
        Math.Abs(local.Position.Y - door.Position.Y) <= 3f;

    /// <summary>Walks until within reach of the target. True once there; used for the panel or bell inside, too.</summary>
    public bool WalkTowards(IGameObject target, DateTimeOffset now, string status, out string? failure)
    {
        failure = null;
        if (objects.LocalPlayer is not { } local) return false;
        // Doors and panels sit just off the walkable mesh, so the path aims at the nearest point on it.
        var destination = Approach(local.Position, target.Position);
        if (destination is null) { failure = "no walkable spot was found near the entrance"; return false; }
        // Measured along the ground: doors stand on a step, and Lifestream's paths stop a few yalms short of them.
        var across = new System.Numerics.Vector2(local.Position.X - target.Position.X, local.Position.Z - target.Position.Z).Length();
        if (across <= 4.5f && Math.Abs(local.Position.Y - target.Position.Y) <= 3f ||
            System.Numerics.Vector3.Distance(local.Position, destination.Value) <= 1f &&
            System.Numerics.Vector3.Distance(local.Position, target.Position) <= 6f)
        {
            if (Walking) { pi.GetIpcSubscriber<object>("vnavmesh.Path.Stop").InvokeAction(); Walking = false; }
            return true;
        }
        // Only actual movement counts as progress; asking for the same path again does not.
        if (progressAt is null || System.Numerics.Vector3.Distance(local.Position, lastPosition) > 1f)
        {
            lastPosition = local.Position;
            progressAt = now;
        }
        else if (now - progressAt.Value > TimeSpan.FromSeconds(25))
        { failure = $"the character did not move while {status.TrimEnd('.').ToLowerInvariant()}"; return false; }
        // A path that ended short is asked for again after a while.
        if (Walking && now - walkStartedAt < TimeSpan.FromSeconds(10)) return false;
        if (!pi.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady").InvokeFunc()) { Note("Waiting for the navigation mesh..."); return false; }
        if (!pi.GetIpcSubscriber<System.Numerics.Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo").InvokeFunc(destination.Value, false))
        { failure = "no walking path was found"; return false; }
        bridge.LogAutomationDiagnostic($"Workshop travel: path from {local.Position} to {destination} (target {target.Position}).");
        Walking = true;
        walkStartedAt = now;
        Note(status);
        return false;
    }

    // The door's own point is off the mesh. Try spots in front of it, on the character's side, then wider searches.
    private System.Numerics.Vector3? Approach(System.Numerics.Vector3 from, System.Numerics.Vector3 target)
    {
        var nearest = pi.GetIpcSubscriber<System.Numerics.Vector3, float, float, System.Numerics.Vector3?>("vnavmesh.Query.Mesh.NearestPoint");
        var flat = new System.Numerics.Vector3(from.X - target.X, 0, from.Z - target.Z);
        var toward = flat.LengthSquared() > 0.01f ? System.Numerics.Vector3.Normalize(flat) : System.Numerics.Vector3.UnitZ;
        foreach (var step in new[] { 2f, 3.5f, 5f })
            if (nearest.InvokeFunc(target + toward * step, 2, 6) is { } spot && System.Numerics.Vector3.Distance(spot, target) <= 6f)
                return spot;
        return nearest.InvokeFunc(target, 6, 10);
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
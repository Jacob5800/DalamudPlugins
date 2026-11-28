using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using NativeObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace RetainerPricer;

internal sealed unsafe class MultiCharacterHost(IDalamudPluginInterface pi, IGameGui gui, IDataManager data,
    IObjectTable objects, ITargetManager targets, IClientState client, IPlayerState player, ICondition condition,
    PluginConfig config, NativeMarketBridge bridge, AutomationController automation, Func<bool> otherBusy) : IMultiCharacterHost
{
    private const uint SummoningBellNameRow = 2000401;
    private readonly TeleportRetry teleport = new();
    private bool walking;
    private DateTimeOffset aethernetRetryAt;
    private DateTimeOffset? nearbyUntil;
    private bool nearbyGivenUp;

    public ulong ContentId => client.IsLoggedIn ? player.ContentId : 0;
    public bool CharacterReady => ContentId != 0 && objects.LocalPlayer != null &&
        !condition[ConditionFlag.BetweenAreas] && !condition[ConditionFlag.BetweenAreas51];
    public bool LifestreamBusy => pi.GetIpcSubscriber<bool>("Lifestream.IsBusy").InvokeFunc();
    public int ChangeCharacter(string name, string world) =>
        pi.GetIpcSubscriber<string, string, int>("Lifestream.ChangeCharacter").InvokeFunc(name, world);

    public string? BlockedReason =>
        automation.AutoRetainerLoaded ? "Unload AutoRetainer before starting a multi-character run."
        : otherBusy() || automation.IsRunning ? "Finish other Retainer Pricer operations first."
        : condition[ConditionFlag.InCombat] || condition[ConditionFlag.BoundByDuty] ? "Leave combat and duties first."
        : null;

    public bool RetainerPickerOpen => bridge.IsRetainerPickerVisible;
    public bool BellInteraction => condition[ConditionFlag.OccupiedSummoningBell];

    private Dalamud.Game.ClientState.Objects.Types.IGameObject? NearestBell()
    {
        var local = objects.LocalPlayer;
        var bellName = data.GetExcelSheet<EObjName>().GetRowOrDefault(SummoningBellNameRow)?.Singular.ToString();
        if (local == null || string.IsNullOrEmpty(bellName)) return null;
        // Covers both public bells and placed housing bells, which share the name.
        return objects
            .Where(candidate => candidate.ObjectKind is ObjectKind.EventObj or ObjectKind.HousingEventObject
                && candidate.IsTargetable
                && string.Equals(candidate.Name.TextValue, bellName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => System.Numerics.Vector3.Distance(local.Position, candidate.Position))
            .FirstOrDefault();
    }

    public bool TryUseBell()
    {
        var local = objects.LocalPlayer;
        var bell = NearestBell();
        if (local == null || bell == null || System.Numerics.Vector3.Distance(local.Position, bell.Position) > 4.6f) return false;
        targets.Target = bell;
        TargetSystem.Instance()->InteractWithObject((NativeObject*)bell.Address);
        return true;
    }

    public bool TravelEnabled => config.MultiCharacterTeleport;

    // Main aetheryte zone, bell zone and the aethernet shard (Aetheryte sheet row) nearest that bell; 0 means walk.
    private (string Name, uint MainZone, uint BellZone, uint Shard) City => config.MultiCharacterCity switch
    {
        MultiCharacterCity.Gridania => ("Gridania", 132, 133, 26),
        MultiCharacterCity.LimsaLominsa => ("Limsa Lominsa", 129, 129, 0),
        _ => ("Ul'dah", 130, 131, 125),
    };

    public string? Travel(DateTimeOffset now)
    {
        if (condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51] || condition[ConditionFlag.Casting])
        {
            // A zone change ends any path.
            if (!condition[ConditionFlag.Casting]) walking = false;
            teleport.Busy(now);
            return null;
        }
        if (!CharacterReady || teleport.Waiting(now)) return null;
        try
        {
            // Check vnavmesh before spending gil on a teleport it could not follow up.
            var pathRunning = pi.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning").InvokeFunc();
            if (pathRunning && !walking) return "another vnavmesh path is running";
            if (LifestreamBusy) return null;
            var city = City;
            var bell = NearestBell();
            // A bell a short walk away is tried before spending a teleport; one that cannot be reached falls back to the city.
            if (!nearbyGivenUp && teleport.Attempts == 0 && bell != null &&
                System.Numerics.Vector3.Distance(objects.LocalPlayer!.Position, bell.Position) <= 30)
            {
                if (TryUseBell()) return Arrived();
                nearbyUntil ??= now.AddSeconds(20);
                if (now < nearbyUntil && WalkTo(bell.Position) is null) return null;
                StopWalking();
                nearbyGivenUp = true;
            }
            if (client.TerritoryType == city.BellZone && bell != null)
                return TryUseBell() ? Arrived() : WalkTo(bell.Position);
            // The bell zone can take a moment to load its objects after the aethernet hop.
            if (city.Shard != 0 && client.TerritoryType == city.BellZone && now < aethernetRetryAt) return null;
            if (client.TerritoryType != city.MainZone) return Teleport(now, city);

            var local = objects.LocalPlayer!;
            var aetheryte = objects.Where(candidate => candidate.ObjectKind == ObjectKind.Aetheryte)
                .OrderBy(candidate => System.Numerics.Vector3.Distance(local.Position, candidate.Position)).FirstOrDefault();
            // In the right zone but away from the plaza: teleport to it once rather than walking across town.
            if (teleport.Attempts == 0 && (city.Shard == 0 ? bell == null
                    : aetheryte == null || System.Numerics.Vector3.Distance(local.Position, aetheryte.Position) > 30))
                return Teleport(now, city);
            if (city.Shard == 0 || aetheryte == null) return null;
            if (System.Numerics.Vector3.Distance(local.Position, aetheryte.Position) > 9) return WalkTo(aetheryte.Position);

            StopWalking();
            if (now < aethernetRetryAt) return null;
            var shard = data.GetExcelSheet<Aetheryte>().GetRowOrDefault(city.Shard)?.AethernetName.ValueNullable?.Name.ToString();
            if (string.IsNullOrEmpty(shard)) return "the aethernet destination is unknown";
            var accepted = pi.GetIpcSubscriber<string, bool>("Lifestream.AethernetTeleport").InvokeFunc(shard);
            aethernetRetryAt = now.AddSeconds(accepted ? 20 : 3);
            return null;
        }
        catch (Dalamud.Plugin.Ipc.Exceptions.IpcNotReadyError) { return "vnavmesh or Lifestream is not installed or enabled"; }
    }

    private string Arrived() { StopTravel(); return ""; }

    private string? WalkTo(System.Numerics.Vector3 destination)
    {
        if (walking || !pi.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady").InvokeFunc()) return null;
        if (!pi.GetIpcSubscriber<System.Numerics.Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo").InvokeFunc(destination, false))
            return "no walking path was found";
        walking = true;
        return null;
    }

    private string? Teleport(DateTimeOffset now, (string Name, uint MainZone, uint BellZone, uint Shard) city)
    {
        if (teleport.Exhausted) return $"the {city.Name} teleport did not complete after {TeleportRetry.MaxAttempts} attempts";
        var telepo = Telepo.Instance();
        if (telepo == null) return "teleport is unavailable";
        telepo->UpdateAetheryteList();
        var destination = telepo->TeleportList.ToArray().FirstOrDefault(entry => entry.TerritoryId == city.MainZone);
        if (destination.AetheryteId == 0) return $"the {city.Name} aetheryte is not unlocked";
        StopWalking();
        // A rejected request still counts as an attempt and is retried.
        telepo->Teleport(destination.AetheryteId, destination.SubIndex);
        teleport.Requested(now);
        return null;
    }

    private void StopWalking()
    {
        if (walking)
            try { pi.GetIpcSubscriber<object>("vnavmesh.Path.Stop").InvokeAction(); }
            catch (Exception ex) { bridge.LogAutomationDiagnostic($"vnavmesh could not stop the path: {ex.Message}"); }
        walking = false;
    }

    public void StopTravel()
    {
        StopWalking();
        teleport.Reset();
        aethernetRetryAt = default;
        nearbyUntil = null;
        nearbyGivenUp = false;
    }

    public void ClosePicker()
    {
        var picker = gui.GetAddonByName<AtkUnitBase>("RetainerList");
        if (picker != null) picker->Close(true);
    }

    public void StartSequence()
    {
        if (config.AutoRunFullSequence || config.AutoRunVentures || config.AutoUpdateExistingListings ||
            config.AutoListNewItems || config.AutoReassignVentures) automation.StartConfigured();
        else automation.Start();
    }

    public bool SequenceRunning => automation.IsRunning;
    public int CompletedSequences => automation.CompletedRuns;
    public string SequenceStatus => automation.Status;
    public void CancelSequence(string reason) => automation.Cancel(reason);

    /// <summary>Lists the logged-in character, unticked, so it can be chosen. Returns true when the list changed.</summary>
    public bool RememberCurrentCharacter()
    {
        if (!CharacterReady) return false;
        var name = player.CharacterName;
        var world = player.HomeWorld.ValueNullable?.Name.ToString();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(world)) return false;
        var known = config.MultiCharacters.FirstOrDefault(character => character.ContentId == player.ContentId);
        if (known == null)
        {
            config.MultiCharacters.Add(new MultiCharacter { ContentId = player.ContentId, Name = name, World = world });
            return true;
        }
        if (known.Name == name && known.World == world) return false;
        (known.Name, known.World) = (name, world);
        return true;
    }
}

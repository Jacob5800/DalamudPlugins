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
    PluginConfig config, NativeMarketBridge bridge, AutomationController automation, Func<bool> otherBusy,
    WorkshopEntry workshop) : IMultiCharacterHost
{
    private const uint SummoningBellNameRow = 2000401;
    private readonly TeleportRetry teleport = new();
    private bool walking;
    private DateTimeOffset aethernetRetryAt;
    private DateTimeOffset? nearbyUntil;
    private bool nearbyGivenUp;
    private DateTimeOffset? homeRequestedAt;
    private MultiCharacterCity? fallback;
    private MultiCharacterCity Destination => fallback ?? config.MultiCharacterCity;
    public string TravelStatus { get; private set; } = "";

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
        // Same reach as the walk uses, measured along the ground, so the walk never stops just outside it.
        if (local == null || bell == null || !WorkshopEntry.InReach(local, bell)) return false;
        targets.Target = bell;
        TargetSystem.Instance()->InteractWithObject((NativeObject*)bell.Address);
        return true;
    }

    public bool TravelEnabled => config.MultiCharacterTeleport;

    // Main aetheryte zone, bell zone and the aethernet shard (Aetheryte sheet row) nearest that bell; 0 means walk.
    private (string Name, uint MainZone, uint BellZone, uint Shard) City => Destination switch
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
            if (homeRequestedAt is not null) homeRequestedAt = now;
            workshop.Waiting(now);
            Note(condition[ConditionFlag.Casting] ? "casting" : "changing areas");
            return null;
        }
        if (!CharacterReady || teleport.Waiting(now)) return null;
        try
        {
            // Check vnavmesh before spending gil on a teleport it could not follow up.
            var pathRunning = pi.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning").InvokeFunc();
            if (pathRunning && !walking && !workshop.Walking) return "another vnavmesh path is running";
            if (LifestreamBusy)
            {
                if (homeRequestedAt is not null) homeRequestedAt = now;
                workshop.Waiting(now);
                Note("waiting for Lifestream to finish");
                return null;
            }
            var city = City;
            var bell = NearestBell();
            // Any bell the game has loaded around the character is tried before spending a teleport; one that cannot
            // be reached falls back to the destination. The game only loads objects within roughly a hundred yalms.
            if (!nearbyGivenUp && teleport.Attempts == 0)
                Note(bell == null ? "no summoning bell in sight"
                    : walking ? TravelStatus : $"nearest summoning bell is {System.Numerics.Vector3.Distance(objects.LocalPlayer!.Position, bell.Position):0} yalms away");
            if (!nearbyGivenUp && teleport.Attempts == 0 && bell != null)
            {
                if (TryUseBell()) return Arrived();
                nearbyUntil ??= now.AddSeconds(45);
                if (now < nearbyUntil && WalkToBell(bell, now) is null) return null;
                StopWalking();
                nearbyGivenUp = true;
            }
            // Amber's workshop falls back to the free company estate, that to the private estate, and that to Limsa Lominsa.
            if (Destination == MultiCharacterCity.FreeCompanyWorkshop)
                return OrFallBack(TravelWorkshop(now, bell), MultiCharacterCity.FreeCompanyEstate);
            if (Destination == MultiCharacterCity.FreeCompanyEstate)
                return OrFallBack(TravelHome(now, bell, "fc", "your free company estate"), MultiCharacterCity.OwnEstate);
            if (Destination == MultiCharacterCity.OwnEstate)
                return OrFallBack(TravelHome(now, bell, "home", "your estate"), MultiCharacterCity.LimsaLominsa);
            if (client.TerritoryType == city.BellZone && bell != null)
                return TryUseBell() ? Arrived() : WalkToBell(bell, now);
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
            Note(accepted ? $"taking the aethernet to {shard}" : $"Lifestream did not accept the aethernet hop to {shard}; retrying");
            aethernetRetryAt = now.AddSeconds(accepted ? 20 : 3);
            return null;
        }
        catch (Dalamud.Plugin.Ipc.Exceptions.IpcNotReadyError) { return "vnavmesh or Lifestream is not installed or enabled"; }
    }

    // A bell already in sight is used; otherwise walk in through the house doors.
    private string? TravelWorkshop(DateTimeOffset now, Dalamud.Game.ClientState.Objects.Types.IGameObject? bell)
    {
        if (bell != null) return TryUseBell() ? Arrived() : WalkToBell(bell, now);
        var failure = workshop.Step(now);
        if (failure is null && workshop.Status.Length > 0) Note(workshop.Status);
        return failure;
    }

    private string? OrFallBack(string? outcome, MultiCharacterCity next)
    {
        if (outcome is null or "") return outcome;
        var name = next switch
        {
            MultiCharacterCity.FreeCompanyEstate => "your free company estate",
            MultiCharacterCity.OwnEstate => "your private estate",
            _ => "Limsa Lominsa",
        };
        bridge.LogAutomationDiagnostic($"Multi-character travel: {outcome}; trying {name} instead.");
        StopTravel();
        fallback = next;
        Note($"{outcome}; trying {name} instead");
        return null;
    }

    // Lifestream knows where the character's own house and workshop are; the bell is then walked to like any other.
    private string? TravelHome(DateTimeOffset now, Dalamud.Game.ClientState.Objects.Types.IGameObject? bell,
        string command, string place)
    {
        if (homeRequestedAt is null)
        {
            StopWalking();
            pi.GetIpcSubscriber<string, object>("Lifestream.ExecuteCommand").InvokeAction(command);
            homeRequestedAt = now;
            Note($"asking Lifestream to go to {place}");
            return null;
        }
        var idle = now - homeRequestedAt.Value;
        if (idle < TimeSpan.FromSeconds(5)) return null;
        if (bell != null) return TryUseBell() ? Arrived() : WalkToBell(bell, now);
        if (idle > TimeSpan.FromSeconds(30)) return $"no summoning bell was found at {place}";
        Note($"looking for a summoning bell at {place}");
        return null;
    }

    private string Arrived() { StopTravel(); return ""; }

    // Shown in the run status and logged once per change, so a trip that stalls says where.
    private void Note(string step)
    {
        if (step == TravelStatus) return;
        TravelStatus = step;
        bridge.LogAutomationDiagnostic($"Multi-character travel: {step} (territory {client.TerritoryType})");
    }

    // Bells, like doors, often stand just off the walkable mesh: walk to a spot in front of one and use it from there.
    private string? WalkToBell(Dalamud.Game.ClientState.Objects.Types.IGameObject bell, DateTimeOffset now)
    {
        if (workshop.WalkTowards(bell, now, "Walking to the summoning bell...", out var failure))
            return TryUseBell() ? Arrived() : null;
        if (failure is null && workshop.Status.Length > 0) Note(workshop.Status);
        return failure is null ? null : $"the summoning bell could not be reached: {failure}";
    }

    private string? WalkTo(System.Numerics.Vector3 destination)
    {
        if (walking || !pi.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady").InvokeFunc()) return null;
        if (!pi.GetIpcSubscriber<System.Numerics.Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo").InvokeFunc(destination, false))
            return "no walking path was found";
        walking = true;
        Note("walking with vnavmesh");
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
        var accepted = telepo->Teleport(destination.AetheryteId, destination.SubIndex);
        teleport.Requested(now);
        Note($"teleport to {city.Name} {(accepted ? "requested" : "rejected by the game")}, attempt {teleport.Attempts}");
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
        // A trip home that is still under way is ours to cancel.
        if (homeRequestedAt is not null)
            try { if (LifestreamBusy) pi.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction(); }
            catch (Exception ex) { bridge.LogAutomationDiagnostic($"Lifestream could not be stopped: {ex.Message}"); }
        homeRequestedAt = null;
        workshop.Stop();
        fallback = null;
        TravelStatus = "";
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

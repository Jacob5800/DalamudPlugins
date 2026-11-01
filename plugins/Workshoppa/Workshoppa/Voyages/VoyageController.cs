using System;
using System.Collections.Generic;
using System.Linq;

namespace Workshoppa.Voyages;

internal sealed class VoyageController(IVoyageGame game, VoyageOptions options, Func<string?> busy, Action<string> notify)
{
    private enum Step { Idle, EnterKind, WaitPicker, PickVessel, WaitVessel, WaitCollected, Prepare, WaitPrevious,
        WaitDetail, WaitDeparture, ReturnPicker, ExitPicker, WaitRoot, WaitRepair, RepairPart, WaitRepairDialog,
        WaitRepairDone, WaitRepairClosed, WaitMap, WaitEditor, PickPoint, WaitPoint, ReviewRoute }
    private Step step;
    private readonly Queue<VesselKind> kinds = new();
    private readonly Queue<(Vessel Vessel, VesselPlan Plan)> vessels = new();
    private VesselKind kind;
    private Vessel? vessel;
    private VesselPlan plan = new();
    private ulong character, house;
    private bool collect, redeploy, repair, visitHandled;
    private long deadline, nextTick, closedSince;
    private int part, pointIndex;
    private string previousDistance = "";
    public bool Running => step != Step.Idle;
    public string Status { get; private set; } = "Open the Voyage Control Panel, then start a voyage run.";
    public void Stop(string message = "Voyage run stopped. Already completed actions remain applied.", bool error = false)
    {
        var wasRunning = Running;
        game.CancelBuild();
        step = Step.Idle; kinds.Clear(); vessels.Clear(); vessel = null; visitHandled = true;
        Status = message;
        if (error && wasRunning) notify(message);
    }
    private void Go(Step target, long now, string status, int seconds = 30)
    { step = target; deadline = now + seconds * 1000; Status = status; }
    public void Start(long now)
    {
        if (Running) return;
        visitHandled = true;
        var blocker = busy();
        if (blocker != null) { Status = blocker; return; }
        if (!game.Available || !game.Root || game.Character == 0 || game.House == 0)
        { Status = "Open the Voyage Control Panel's Airship/Submersible Management menu in your workshop first."; return; }
        if (!options.Airships && !options.Submarines) { Status = "Enable airships or submarines."; return; }
        if (!options.Collect && !options.Redeploy) { Status = "Enable reward collection or redeployment."; return; }
        character = game.Character; house = game.House;
        collect = options.Collect; redeploy = options.Redeploy; repair = options.Repair;
        kinds.Clear(); vessels.Clear();
        if (options.Airships) kinds.Enqueue(VesselKind.Airship);
        if (options.Submarines) kinds.Enqueue(VesselKind.Submarine);
        Go(Step.EnterKind, now, "Starting voyage run...");
    }
    private Vessel? Current() => vessel == null ? null : game.Roster(kind).FirstOrDefault(v => v.Slot == vessel.Slot && v.Name == vessel.Name);
    public void Update(long now, long unixSeconds)
    {
        if (now < nextTick) return;
        nextTick = now + 250;
        if (!Running)
        {
            if (!game.AnyWindow)
            { if (closedSince == 0) closedSince = now; if (now - closedSince >= 2000) visitHandled = false; }
            else closedSince = 0;
            if (options.AutoRunOnPanelOpen && !visitHandled && game.Root) Start(now);
            return;
        }
        var blocker = busy();
        if (blocker != null || !game.Available || game.Character != character || game.House != house)
        { Stop(blocker ?? "Voyage run stopped: character or workshop changed, loading, or character unavailable.", true); return; }
        if (now > deadline) { Stop($"Voyage run timed out: {Status} Check the open game window before retrying.", true); return; }
        if (game.Cutscene) return;
        if (vessel != null && Current() == null) { Stop("Selected vessel changed or vessel data unloaded; voyage run stopped.", true); return; }
        if (vessel != null && (game.Result || game.Detail || game.Editor || game.RepairWindow || game.VesselMenu || step is Step.WaitRepairDialog or Step.WaitRepairDone)
            && !game.SelectedVesselMatches(vessel))
        { Stop("The game's selected vessel changed; no further voyage actions submitted.",true); return; }
        switch (step)
        {
            case Step.EnterKind:
                if (kinds.Count == 0) { Stop("Voyage run complete. Vessels still travelling were left alone."); return; }
                if (!game.Root) return;
                kind = kinds.Dequeue();
                if (game.SelectKind(kind)) Go(Step.WaitPicker, now, $"Opening {kind} management...");
                break;
            case Step.WaitPicker:
                if (!game.Picker(kind)) return;
                vessels.Clear();
                foreach (var v in game.Roster(kind))
                {
                    var p = options.Vessels.TryGetValue(v.Key(house), out var saved) ? saved.Copy() : new VesselPlan();
                    if (VoyagePolicy.Eligible(v,p,collect,redeploy,unixSeconds)) vessels.Enqueue((v,p));
                }
                Go(Step.PickVessel,now,$"Checking eligible {kind} vessels...");
                break;
            case Step.PickVessel:
                if (!game.Picker(kind)) return;
                if (vessels.Count == 0) { vessel = null; Go(Step.ExitPicker,now,"Returning to the main voyage menu..."); return; }
                var next = vessels.Dequeue(); vessel = next.Vessel; plan = next.Plan;
                var current = Current();
                if (current == null) { Stop("Vessel data changed before selection.",true); return; }
                if (current.ReturnTime > unixSeconds) { vessel = null; return; }
                if (redeploy && VoyagePolicy.ValidateRoute(plan,kind,game.Destinations,current.Rank) is { } routeError)
                { Stop($"{current.Name}: {routeError}",true); return; }
                if (game.SelectVessel(current)) Go(Step.WaitVessel,now,$"Opening {current.Name}...");
                else { Stop("The vessel picker no longer matches the queued vessel. Nothing selected.",true); }
                break;
            case Step.WaitVessel:
                if (game.Result)
                {
                    if (!collect || Current()!.ReturnTime > unixSeconds) { Stop("Unexpected reward window; collection was not requested.",true); return; }
                    if (game.Collect()) Go(Step.WaitCollected,now,$"Collecting {vessel!.Name} rewards...");
                }
                else if (game.VesselMenu)
                {
                    if (Current()!.ReturnTime != 0) { Stop("Vessel still has an uncollected voyage; no deployment submitted.",true); return; }
                    Go(Step.Prepare,now,$"Preparing {vessel!.Name}...");
                }
                break;
            case Step.WaitCollected:
                if (Current()!.ReturnTime == 0 && game.VesselMenu) Go(redeploy ? Step.Prepare : Step.ReturnPicker,now,$"Rewards collected from {vessel!.Name}.");
                break;
            case Step.Prepare:
                if (!game.VesselMenu && !plan.AutoSwapBuild) return;
                if (!redeploy) { Go(Step.ReturnPicker,now,"Collection complete."); return; }
                var ready = Current()!;
                if (ready.ReturnTime != 0) { Stop("Vessel is not idle; deployment stopped.",true); return; }
                if(plan.AutoSwapBuild)
                {
                    if(plan.BuildParts.Count!=4){Stop("The saved build must contain four component rows.",true);return;}
                    if(!game.EnsureBuild(ready,plan.BuildParts,out var swapError))
                    {if(swapError!=null)Stop(swapError,true);return;}
                    ready=Current()!;
                }
                if (!ready.PartsReady) { Stop("Vessel component data is not ready; no deployment submitted.",true); return; }
                if (ready.Conditions.Any(c => c == 0))
                {
                    if (!repair) { Stop($"{ready.Name} needs repairs. Enable automatic repairs or repair it manually.",true); return; }
                    if (game.OpenRepair(kind)) Go(Step.WaitRepair,now,$"Opening repairs for {ready.Name}...");
                }
                else if (plan.UseCustomRoute)
                {
                    if (game.OpenRoute()) Go(Step.WaitMap,now,$"Opening configured route for {ready.Name}...");
                }
                else if (game.PreviousLog()) Go(Step.WaitPrevious,now,$"Opening {ready.Name}'s previous voyage...");
                break;
            case Step.WaitPrevious:
                if (!game.Result) return;
                if (game.Repeat(out var repeatError)) Go(Step.WaitDetail,now,"Reviewing previous route...");
                else if (repeatError != null) Stop(repeatError,true);
                break;
            case Step.WaitDetail:
                if (!game.Detail) return;
                if (Current()!.ReturnTime != 0 || Current()!.Conditions.Any(c => c == 0)) { Stop("Vessel readiness changed before deployment.",true); return; }
                if (game.Deploy(out var deployError)) Go(Step.WaitDeparture,now,$"Waiting for {vessel!.Name} departure...",120);
                else if (deployError != null) Stop(deployError,true);
                break;
            case Step.WaitDeparture:
                if (Current()!.ReturnTime > unixSeconds && !game.Detail && !game.Result)
                    Go(Step.ReturnPicker,now,$"{vessel!.Name} deployed successfully.");
                break;
            case Step.ReturnPicker:
                if (game.Picker(kind)) { vessel = null; Go(Step.PickVessel,now,"Checking the next vessel..."); }
                else if (game.VesselMenu && game.QuitVessel()) { vessel = null; Go(Step.PickVessel,now,"Returning to the vessel picker..."); }
                break;
            case Step.ExitPicker:
                if (game.Picker(kind) && game.QuitPicker()) Go(Step.WaitRoot,now,"Returning to the main panel...");
                break;
            case Step.WaitRoot:
                if (game.Root) Go(Step.EnterKind,now,"Checking the next vessel type...");
                break;
            case Step.WaitRepair:
                if (game.RepairWindow) Go(Step.RepairPart,now,"Checking broken vessel parts...");
                break;
            case Step.RepairPart:
                if (!game.RepairWindow) return;
                var parts = Current()!;
                if (!parts.PartsReady) { Stop("Vessel repair inventory unloaded.",true); return; }
                part = Array.FindIndex(parts.Conditions,c => c == 0);
                if (part < 0)
                { if (game.CloseRepair()) Go(Step.WaitRepairClosed,now,"Closing vessel repairs..."); return; }
                if (game.RequestRepair(parts,part,out var repairError)) Go(Step.WaitRepairDialog,now,$"Repairing {parts.Name}, part {part+1}...",15);
                else if (repairError != null) Stop(repairError,true);
                break;
            case Step.WaitRepairDialog:
                if (Current()!.PartsReady && Current()!.Conditions[part] > 0) { Go(Step.RepairPart,now,"Repair completed; checking remaining components..."); return; }
                if (game.ConfirmRepair(Current()!,part,out var confirmError)) Go(Step.WaitRepairDone,now,"Waiting for repaired component...",15);
                else if (confirmError != null) Stop(confirmError,true);
                break;
            case Step.WaitRepairDone:
                if (Current()!.PartsReady && Current()!.Conditions[part] > 0) Go(Step.RepairPart,now,"Checking remaining components...");
                break;
            case Step.WaitRepairClosed:
                if (game.VesselMenu) Go(Step.Prepare,now,"Repairs completed; preparing route...");
                break;
            case Step.WaitMap:
                var destination = game.Destinations.First(d=>d.Kind==kind && d.Id==plan.Points[0]);
                if (game.MapSelect)
                { if (game.PickMap(destination.Map,out var mapError)) Go(Step.WaitEditor,now,"Opening the selected voyage map..."); else if (mapError != null) Stop(mapError,true); }
                else if (game.Editor) Go(Step.WaitEditor,now,"Checking voyage editor...");
                break;
            case Step.WaitEditor:
                if (!game.Editor) return;
                if (game.SelectedCount(kind) < 0) return;
                if (game.SelectedCount(kind) > 0) { Stop("The route editor already has destinations selected; no automatic changes submitted.",true); return; }
                if (kind == VesselKind.Submarine && game.SelectedMap != game.Destinations.First(d=>d.Kind==kind && d.Id==plan.Points[0]).Map)
                { Stop("The open submarine map does not match the configured route.",true); return; }
                pointIndex = 0; Go(Step.PickPoint,now,"Selecting configured destinations...");
                break;
            case Step.PickPoint:
                if (!game.Editor) return;
                if (pointIndex == plan.Points.Count) { Go(Step.ReviewRoute,now,"Reviewing the configured route..."); return; }
                var point = game.Destinations.First(d=>d.Kind==kind && d.Id==plan.Points[pointIndex]);
                previousDistance = game.RouteDistance;
                if (game.PickPoint(point,out var pointError)) Go(Step.WaitPoint,now,$"Selecting {point.Name}...",10);
                else if (pointError != null) Stop(pointError,true);
                break;
            case Step.WaitPoint:
                if (!game.Editor) return;
                if (game.SelectedCount(kind) == pointIndex+1 || (kind == VesselKind.Airship && game.RouteDistance != previousDistance))
                { pointIndex++; Go(Step.PickPoint,now,"Selecting the next destination..."); }
                break;
            case Step.ReviewRoute:
                if (game.ReviewRoute(out var reviewError)) Go(Step.WaitDetail,now,"Checking fuel and range before departure...");
                else if (reviewError != null) Stop(reviewError,true);
                break;
        }
    }
}

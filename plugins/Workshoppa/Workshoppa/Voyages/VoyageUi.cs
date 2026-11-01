using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace Workshoppa.Voyages;

internal sealed class VoyageUi(VoyageNative game, VoyageController controller, VoyageOptions options, Action save)
{
    private string search = "";
    private readonly Dictionary<string,uint> maps = new();
    private Vessel[] roster = Array.Empty<Vessel>();
    private long nextRefresh;
    public void Draw()
    {
        ImGui.TextWrapped("Collect finished voyages and resend vessels on their previous route, or a per-vessel destination plan. Open the Voyage Control Panel's main menu before starting. Travelling vessels are never recalled.");
        ImGui.TextWrapped(controller.Status);
        if(controller.Running)
        { if(ImGui.Button("Stop voyage run"))controller.Stop(); }
        else if(ImGui.Button("Run voyages now"))controller.Start(Environment.TickCount64);
        ImGui.BeginDisabled(controller.Running);
        bool changed=false;
        var auto=options.AutoRunOnPanelOpen;
        if(ImGui.Checkbox("Automatically run when opening Voyage Control Panel",ref auto)){options.AutoRunOnPanelOpen=auto;changed=true;}
        var air=options.Airships;var sub=options.Submarines;
        if(ImGui.Checkbox("Process airships",ref air)){options.Airships=air;changed=true;}
        ImGui.SameLine();
        if(ImGui.Checkbox("Process submarines",ref sub)){options.Submarines=sub;changed=true;}
        var collect=options.Collect;var resend=options.Redeploy;var repair=options.Repair;
        if(ImGui.Checkbox("Collect completed voyage rewards",ref collect)){options.Collect=collect;changed=true;}
        if(ImGui.Checkbox("Redeploy idle/collected vessels",ref resend)){options.Redeploy=resend;changed=true;}
        if(ImGui.Checkbox("Repair broken parts before redeploying",ref repair)){options.Repair=repair;changed=true;}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Uses Magitek repair materials in carried inventory. Repairs parts at zero condition. When disabled, a vessel needing repairs stops the run and notifies you.");
        if(changed)save();
        ImGui.TextWrapped("Missing fuel, repairs, unavailable routes or permissions stop the run with a message. This does not buy materials, change vessel parts, register vessels, move the character or choose an optimized route. Unload AutoRetainer before running voyages here.");
        ImGui.Separator();
        if(Environment.TickCount64>=nextRefresh)
        {
            nextRefresh=Environment.TickCount64+500;
            roster=game.Roster(VesselKind.Airship).Concat(game.Roster(VesselKind.Submarine)).ToArray();
        }
        if(roster.Length==0)ImGui.TextWrapped("Open airship/submersible management once to load this workshop's vessel data. Per-vessel settings are saved for this workshop, vessel type, slot and name.");
        foreach(var vessel in roster)
        {
            string key=vessel.Key(game.House);
            if(!ImGui.TreeNode($"{vessel.Kind}: {vessel.Name} (rank {vessel.Rank})##{key}"))continue;
            ImGui.PushID(key);
            var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            ImGui.TextUnformatted(vessel.ReturnTime==0?"Idle":vessel.ReturnTime<=now?"Voyage complete":$@"Returns in {TimeSpan.FromSeconds(vessel.ReturnTime-now):d\.hh\:mm}");
            var plan=options.Vessels.TryGetValue(key,out var saved)?saved:new VesselPlan();
            void SavePlan(){options.Vessels[key]=plan;save();}
            var enabled=plan.Enabled;
            if(ImGui.Checkbox("Include this vessel",ref enabled)){plan.Enabled=enabled;SavePlan();}
            var custom=plan.UseCustomRoute;
            if(ImGui.Checkbox("Use specific destinations instead of previous voyage",ref custom)){plan.UseCustomRoute=custom;SavePlan();}
            if(custom)
            {
                var destinations=game.Destinations.Where(d=>d.Kind==vessel.Kind).ToArray();
                uint map=plan.Points.Count>0?destinations.FirstOrDefault(d=>d.Id==plan.Points[0])?.Map??1:maps.GetValueOrDefault(key,1u);
                if(vessel.Kind==VesselKind.Submarine && ImGui.BeginCombo("Voyage map",game.MapNames.GetValueOrDefault(map,$"Map {map}")))
                {
                    foreach(var entry in game.MapNames)
                        if(ImGui.Selectable(entry.Value,entry.Key==map))
                        {map=entry.Key;maps[key]=map;plan.Points.Clear();SavePlan();}
                    ImGui.EndCombo();
                }
                for(int i=0;i<plan.Points.Count;i++)
                {
                    int index=i;var point=destinations.FirstOrDefault(d=>d.Id==plan.Points[index]);
                    ImGui.PushID(i);
                    ImGui.TextWrapped($"{index+1}. {point?.Name??$"Unknown destination {plan.Points[index]}"}");
                    ImGui.BeginDisabled(i==0);
                    if(ImGui.SmallButton("Up")){(plan.Points[index-1],plan.Points[index])=(plan.Points[index],plan.Points[index-1]);SavePlan();}
                    ImGui.EndDisabled();ImGui.SameLine();
                    ImGui.BeginDisabled(i==plan.Points.Count-1);
                    if(ImGui.SmallButton("Down")){(plan.Points[index+1],plan.Points[index])=(plan.Points[index],plan.Points[index+1]);SavePlan();}
                    ImGui.EndDisabled();ImGui.SameLine();
                    bool remove=ImGui.SmallButton("Remove");
                    ImGui.PopID();if(remove){plan.Points.RemoveAt(index);SavePlan();break;}
                }
                ImGui.InputText("Destination search",ref search,100);
                ImGui.BeginDisabled(plan.Points.Count>=5);
                if(ImGui.BeginCombo("Add destination","Select up to five points"))
                {
                    foreach(var point in destinations.Where(d=>(vessel.Kind==VesselKind.Airship||d.Map==map)&&!plan.Points.Contains(d.Id)&&d.Name.Contains(search,StringComparison.OrdinalIgnoreCase)))
                        if(ImGui.Selectable($"{point.Name} (rank {point.Rank})")){plan.Points.Add(point.Id);SavePlan();}
                    ImGui.EndCombo();
                }
                ImGui.EndDisabled();
                if(VoyagePolicy.ValidateRoute(plan,vessel.Kind,game.Destinations,vessel.Rank) is { } error)ImGui.TextWrapped(error);
                ImGui.TextWrapped("Listed destinations come from game data. Actual unlocks, availability, fuel and range are checked in the voyage chart before dispatch.");
            }
            ImGui.PopID();ImGui.TreePop();
        }
        ImGui.EndDisabled();
    }
}

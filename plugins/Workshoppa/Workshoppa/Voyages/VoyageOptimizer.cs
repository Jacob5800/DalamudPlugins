using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using Workshoppa.Voyages.Optimization.Calc;
using Workshoppa.Voyages.Optimization.Model;
using Workshoppa.Voyages.Optimization.Data;
using Workshoppa.Voyages.Optimization.Game;
namespace Workshoppa.Voyages;
internal sealed class VoyageOptimizer:IDisposable
{
    private readonly VoyageNative game;
    private readonly PlannerData tables;
    private readonly Dictionary<uint,int> prices;
    private readonly VoyageOptions options;
    private readonly Action save;
    private Task<List<Result>>? task;
    private CancellationTokenSource? cancellation;
    private string selected="",status="";
    private List<Result> results=new();
    private int goal,cap=48;
    private uint map=1;
    private bool owned=true;
    private sealed record Result(Build Build,uint[] Route,TimeSpan Duration,double Gil,double Exp,string Note);
    internal VoyageOptimizer(VoyageNative game,IDataManager data,VoyageOptions options,Action save)
    {
        this.game=game;this.options=options;this.save=save;
        tables=GameDataLoader.Load(data);game.PlanningTables=tables;
        prices=LootTable.Items(VesselType.Submarine).ToDictionary(id=>id,id=>(int)(data.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id)?.PriceLow??0));
    }
    public void Dispose(){cancellation?.Cancel();cancellation?.Dispose();}
    internal void Draw(Vessel vessel,bool running)
    {
        if(!ImGui.CollapsingHeader("Route / build optimisation"))return;
        string key=vessel.Key(game.House);
        if(task is {IsCompleted:true})
        {
            try{results=task.GetAwaiter().GetResult();status=$"{results.Count} recommendations. Estimates use model formulas and community loot samples.";}
            catch(Exception ex){status=ex.GetBaseException().Message;results.Clear();}
            task=null;
        }
        ImGui.TextWrapped("NPC gross value/hour is the default. Estimates exclude repair/fuel costs, market prices and unsampled loot. Build farming search considers the eight highest-value sampled sectors of the selected map; this is not proof of a global optimum.");
        ImGui.BeginDisabled(task!=null||running);
        ImGui.Combo("Optimisation goal",ref goal,new[]{"NPC value per hour (submarines)","EXP per hour","EXP per voyage"},3);
        if(vessel.Kind==VesselKind.Submarine&&ImGui.BeginCombo("Optimiser map",game.MapNames.GetValueOrDefault(map,$"Map {map}")))
        {foreach(var m in game.MapNames)if(ImGui.Selectable(m.Value,m.Key==map))map=m.Key;ImGui.EndCombo();}
        ImGui.InputInt("Maximum voyage hours",ref cap);cap=Math.Clamp(cap,1,168);
        ImGui.Checkbox("Recommend only equipped / carried parts",ref owned);
        if(ImGui.Button("Find routes and builds"))
        {
            try
            {
                var type=VoyageNative.ModelKind(vessel.Kind);uint planningMap=type==VesselType.Airship?1:map;
                var current=game.ReadBuild(vessel);if(current.Length!=4)throw new InvalidOperationException("Vessel build unavailable.");
                var build=Build.From(tables,type,(int)vessel.Rank,current[0],current[1],current[2],current[3]);
                var unlocked=game.Unlocked(vessel,tables,planningMap);
                if(unlocked.Count==0)throw new InvalidOperationException(type==VesselType.Airship?"Open this airship's empty voyage chart to read unlocked destinations first.":"No unlocked destinations available on this map.");
                var allowed=owned?game.OwnedParts(vessel,tables):null;int fuel=game.FuelCount;
                if(fuel<=0)throw new InvalidOperationException("No carried ceruleum tanks; refill before planning dispatchable routes.");
                bool farming=goal==0&&type==VesselType.Submarine;
                var searchGoal=goal==2?RouteGoal.ExpPerVoyage:RouteGoal.ExpPerHour;
                var durationCap=TimeSpan.FromHours(cap);selected=key;results.Clear();status="Calculating...";
                cancellation?.Dispose();cancellation=new();var token=cancellation.Token;
                task=Task.Run(()=>
                {
                    var output=new List<Result>();token.ThrowIfCancellationRequested();
                    var request=new RouteRequest(type,planningMap,build,unlocked,new HashSet<uint>(),searchGoal,durationCap,true,FuelAvailable:fuel,Prices:farming?prices:null);
                    foreach(var route in RouteSearch.FindTop(tables,request,5,token))
                        output.Add(new(build,route.Sectors,route.Duration,route.Gil,route.Exp.Average,"Current build"));
                    token.ThrowIfCancellationRequested();
                    if(farming)
                    {
                        foreach(var recommendation in PartOptimizer.BestForGil(tables,(int)vessel.Rank,planningMap,unlocked,prices,0,searchGoal,10,allowed,current))
                        {
                            token.ThrowIfCancellationRequested();
                            if(recommendation.Duration>durationCap||recommendation.Route.Sum(id=>tables.Sector(type,id).Fuel)>fuel)continue;
                            output.Add(new(recommendation.Build,recommendation.Route,recommendation.Duration,recommendation.Gil,0,"Recommended farming build"));
                        }
                    }
                    else if(output.Count>0)
                    {
                        foreach(var recommendation in PartOptimizer.Best(tables,type,(int)vessel.Rank,planningMap,output[0].Route,searchGoal,true,5,allowed))
                            if(recommendation.Duration<=durationCap)
                                output.Add(new(recommendation.Build,output[0].Route,recommendation.Duration,0,recommendation.Exp.Average,"Build optimised for the best current-build route"));
                    }
                    return output.OrderByDescending(r=>(farming?r.Gil:r.Exp)/(searchGoal==RouteGoal.ExpPerHour?Math.Max(.01,r.Duration.TotalHours):1)).Take(10).ToList();
                },token);
            }
            catch(Exception ex){status=ex.Message;}
        }
        ImGui.EndDisabled();
        if(task!=null&&ImGui.Button("Cancel calculation"))cancellation?.Cancel();
        ImGui.TextWrapped(status);
        if(selected!=key)return;
        var plan=options.Vessels.TryGetValue(key,out var p)?p:new VesselPlan();
        var swap=plan.AutoSwapBuild;
        ImGui.BeginDisabled(running);
        if(ImGui.Checkbox("Automatically install saved recommended build before redeployment",ref swap))
        {plan.AutoSwapBuild=swap;options.Vessels[key]=plan;save();}
        ImGui.TextWrapped("Part swapping is off by default. Save a recommendation below, then enable swapping if desired. Components must be in carried inventory; no purchase or crafting is performed. Changes stop on identity/quantity/readiness errors.");
        if(plan.BuildParts.Count==4)ImGui.TextWrapped("Saved part rows: "+string.Join(" / ",plan.BuildParts));
        for(int i=0;i<results.Count;i++)
        {
            var result=results[i];ImGui.PushID(i);
            ImGui.TextWrapped($"{result.Note}: {result.Build.Identifier} — {result.Duration.TotalHours:F1} h, estimated NPC {result.Gil:N0} gil / {result.Gil/Math.Max(.01,result.Duration.TotalHours):N0} per hour, EXP {result.Exp:N0}");
            ImGui.TextWrapped(string.Join(" → ",result.Route.Select(id=>tables.Sector(result.Build.Type,id).Name)));
            ImGui.TextWrapped($"Surv {result.Build.Surveillance} | Retrieval {result.Build.Retrieval} | Speed {result.Build.Speed} | Range {result.Build.Range} | Favor {result.Build.Favor} | Capacity {result.Build.Cost}/{result.Build.Capacity}");
            if(ImGui.Button("Save this route and build"))
            {
                var current=game.Roster(vessel.Kind).FirstOrDefault(v=>v.Slot==vessel.Slot&&v.Name==vessel.Name);
                if(current==null||current.Rank!=vessel.Rank)status="Vessel changed; recalculate before saving.";
                else
                {
                    plan.Points=result.Route.ToList();plan.UseCustomRoute=true;plan.BuildParts=result.Build.Parts.Select(p=>p.Id).ToList();
                    options.Vessels[key]=plan;save();status="Route/build saved. Swapping remains controlled by its checkbox.";
                }
            }
            ImGui.PopID();
        }
        ImGui.EndDisabled();
    }
}

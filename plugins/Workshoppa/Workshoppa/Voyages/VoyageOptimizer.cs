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
    private readonly VoyageMarketCache market;
    private readonly uint[] marketItems;
    private bool marketResults;
    private readonly Action save;
    private Task<List<Result>>? task;
    private CancellationTokenSource? cancellation;
    private string selected="",status="";
    private List<Result> results=new();
    private int goal,cap=48;
    private uint map=1;
    private bool owned=true;
    private sealed record Result(Build Build,uint[] Route,TimeSpan Duration,double Gil,double Exp,string Note);
    internal VoyageOptimizer(VoyageNative game,IDataManager data,VoyageOptions options,Action save,string configDirectory)
    {
        this.game=game;this.options=options;this.save=save;
        tables=GameDataLoader.Load(data);game.PlanningTables=tables;
        market=new(configDirectory);
        marketItems=LootTable.Items(VesselType.Submarine).Where(id=>data.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id) is { } item && !item.IsUntradable && item.ItemSearchCategory.RowId!=0).ToArray();
        prices=LootTable.Items(VesselType.Submarine).ToDictionary(id=>id,id=>(int)(data.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRowOrDefault(id)?.PriceLow??0));
    }
    public void Dispose(){cancellation?.Cancel();cancellation?.Dispose();market.Dispose();}
    internal void Draw(Vessel vessel,bool running)
    {

        string key=vessel.Key(game.House);
        if(task is {IsCompleted:true})
        {
            try{results=task.GetAwaiter().GetResult();status=$"{results.Count} recommendations. Estimates use model formulas and community loot samples.";}
            catch(Exception ex){status=ex.GetBaseException().Message;results.Clear();}
            task=null;
        }
        ImGui.TextWrapped("Choose a goal, then find routes and builds. Save a recommendation to use it for this vessel.");
        ImGui.BeginDisabled(task!=null||running);
        ImGui.SetNextItemWidth(300);
        ImGui.Combo("Goal",ref goal,new[]{"NPC value per hour (submarines)","EXP per hour","EXP per voyage","Gil profit per hour (Universalis)"},4);
        if(vessel.Kind==VesselKind.Submarine&&ImGui.BeginCombo("Map",game.MapNames.GetValueOrDefault(map,$"Map {map}")))
        {foreach(var m in game.MapNames)if(ImGui.Selectable(m.Value,m.Key==map))map=m.Key;ImGui.EndCombo();}
        ImGui.SetNextItemWidth(160);
        ImGui.InputInt("Maximum voyage hours",ref cap);cap=Math.Clamp(cap,1,168);
        ImGui.Checkbox("Only equipped / carried parts",ref owned);
        if(goal==3)
        {
            market.Update(game.HomeWorld,marketItems);
            ImGui.TextWrapped(market.Status);
            if(market.Current is {} cached)
                ImGui.TextWrapped($"Home world {game.HomeWorld}: {cached.Prices.Count}/{marketItems.Length} market prices. Cache fetched {cached.RetrievedAt.ToLocalTime():g} (underlying sales may be older).");
            if(ImGui.SmallButton("Refresh cached prices"))market.Update(game.HomeWorld,marketItems,true);
            var tax=options.MarketTaxPercent;var cost=options.MarketRunningCostPerHour;
            ImGui.SetNextItemWidth(160);
            if(ImGui.InputFloat("Sale tax (%)",ref tax)){options.MarketTaxPercent=Math.Clamp(tax,0,100);save();}
            ImGui.SetNextItemWidth(160);
            if(ImGui.InputFloat("Fuel / repair allowance (gil/hour)",ref cost)){options.MarketRunningCostPerHour=Math.Clamp(cost,0,1000000);save();}
            ImGui.TextWrapped("NQ cached average sale prices, less tax and your running-cost allowance. Uses NPC value when it is higher or a market price is missing. This does not guarantee sales; HQ premiums and unsampled drops are excluded.");
            if(vessel.Kind!=VesselKind.Submarine)ImGui.TextWrapped("Market estimates require submarine loot-rate samples; airships are not supported.");
        }
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
                bool useMarket=goal==3;
                if(useMarket && type!=VesselType.Submarine)throw new InvalidOperationException("Market profit estimates are available for submarines only.");
                if(useMarket && (market.Current==null || market.Current.Prices.Count==0))throw new InvalidOperationException("Wait for cached market prices, then search again.");
                bool farming=(goal==0||useMarket)&&type==VesselType.Submarine;
                var valuation=new Dictionary<uint,int>(prices);
                var allowance=useMarket?options.MarketRunningCostPerHour:0;
                if(useMarket)
                    foreach(var entry in market.Current!.Prices)
                        valuation[entry.Key]=Math.Max(prices.GetValueOrDefault(entry.Key), (int)(entry.Value*(1-options.MarketTaxPercent/100.0)));
                marketResults=useMarket;
                var searchGoal=goal==2?RouteGoal.ExpPerVoyage:RouteGoal.ExpPerHour;
                var durationCap=TimeSpan.FromHours(cap);selected=key;results.Clear();status="Calculating...";
                cancellation?.Dispose();cancellation=new();var token=cancellation.Token;
                task=Task.Run(()=>
                {
                    var output=new List<Result>();token.ThrowIfCancellationRequested();
                    var request=new RouteRequest(type,planningMap,build,unlocked,new HashSet<uint>(),searchGoal,durationCap,true,FuelAvailable:fuel,Prices:farming?valuation:null);
                    foreach(var route in RouteSearch.FindTop(tables,request,5,token))
                        output.Add(new(build,route.Sectors,route.Duration,route.Gil-allowance*route.Duration.TotalHours,route.Exp.Average,"Current build"));
                    token.ThrowIfCancellationRequested();
                    if(farming)
                    {
                        foreach(var recommendation in PartOptimizer.BestForGil(tables,(int)vessel.Rank,planningMap,unlocked,valuation,0,searchGoal,10,allowed,current))
                        {
                            token.ThrowIfCancellationRequested();
                            if(recommendation.Duration>durationCap||recommendation.Route.Sum(id=>tables.Sector(type,id).Fuel)>fuel)continue;
                            output.Add(new(recommendation.Build,recommendation.Route,recommendation.Duration,recommendation.Gil-allowance*recommendation.Duration.TotalHours,0,"Recommended farming build"));
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
        if(ImGui.CollapsingHeader("How estimates work"))
            ImGui.TextWrapped("Uses community loot samples and model formulas. NPC values are gross, before costs. Farming build searches shortlist the eight highest-value sampled sectors; recommendations are not a guaranteed global optimum. No items or parts are purchased.");
        if(selected!=key)return;
        var plan=options.Vessels.TryGetValue(key,out var p)?p:new VesselPlan();
        var swap=plan.AutoSwapBuild;
        ImGui.BeginDisabled(running);
        if(ImGui.Checkbox("Automatically install saved recommended build before redeployment",ref swap))
        {plan.AutoSwapBuild=swap;options.Vessels[key]=plan;save();}
        if(ImGui.IsItemHovered())ImGui.SetTooltip("Part swapping is off by default. Save a recommendation below, then enable swapping if desired. Components must be in carried inventory; no purchase or crafting is performed. Changes stop on identity/quantity/readiness errors.");
        if(plan.BuildParts.Count==4)ImGui.TextWrapped("Saved part rows: "+string.Join(" / ",plan.BuildParts));
        if(ImGui.BeginChild("recommendations",new System.Numerics.Vector2(0,Math.Clamp(ImGui.GetContentRegionAvail().Y-8,140,320)),true))
        for(int i=0;i<results.Count;i++)
        {
            var result=results[i];ImGui.PushID(i);
            ImGui.TextWrapped($"{result.Note}: {result.Build.Identifier} — {result.Duration.TotalHours:F1} h, estimated {(marketResults?"net market":"NPC")} {result.Gil:N0} gil / {result.Gil/Math.Max(.01,result.Duration.TotalHours):N0} per hour, EXP {result.Exp:N0}");
            ImGui.TextWrapped(string.Join(" → ",result.Route.Select(id=>tables.Sector(result.Build.Type,id).Name)));
            if(ImGui.CollapsingHeader("Build details"))ImGui.TextWrapped($"Surv {result.Build.Surveillance} | Retrieval {result.Build.Retrieval} | Speed {result.Build.Speed} | Range {result.Build.Range} | Favor {result.Build.Favor} | Capacity {result.Build.Cost}/{result.Build.Capacity}");
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
            ImGui.Separator();
            ImGui.PopID();
        }
        ImGui.EndChild();
        ImGui.EndDisabled();
    }
}

using Workshoppa.Voyages.Optimization.Model;
using Workshoppa.Voyages.Optimization.Calc;
using Workshoppa.Voyages.Optimization.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
internal static class OptimizerChecks
{
    private sealed class Dto{public SectorInfo[] Sectors=Array.Empty<SectorInfo>();public PartInfo[] Parts=Array.Empty<PartInfo>();public RankInfo[] Ranks=Array.Empty<RankInfo>();public MapInfo[] Maps=Array.Empty<MapInfo>();}
    internal static int Run()
    {
        int n=0;void Check(bool ok,string msg){if(!ok)throw new InvalidOperationException(msg);n++;}
        var dto=JsonConvert.DeserializeObject<Dto>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"gamedata.json")),new JsonSerializerSettings{Converters={new StringEnumConverter()}})!;
        var data=new PlannerData(dto.Sectors,dto.Parts,dto.Ranks,dto.Maps);
        var build=Build.From(data,VesselType.Submarine,50,3,4,1,2);
        Check(build.FitsCapacity&&build.PartsAllowed,"valid stock build");
        Check(build.Parts.Select((part,index)=>part.Slot==index).All(ok=>ok),"native slot order matches model");
        var unlocked=new HashSet<uint>{1,2,3,4,5};
        var request=new RouteRequest(VesselType.Submarine,1,build,unlocked,new HashSet<uint>(),RouteGoal.ExpPerHour,TimeSpan.FromHours(72),true,FuelAvailable:100);
        var routes=RouteSearch.FindTop(data,request,5);
        Check(routes.Count>0,"route search produces results");
        Check(routes.All(r=>r.Sectors.All(unlocked.Contains)&&r.Distance<=build.Range&&r.Fuel<=100&&r.Sectors.Length<=5&&r.Sectors.Distinct().Count()==r.Sectors.Length),"routes obey constraints");
        Check(RouteSearch.FindTop(data,request with{FuelAvailable=0},5).Count==0,"zero fuel rejected");
        Check(RouteSearch.FindTop(data,request with{MustInclude=new HashSet<uint>{999}},5).Count==0,"unknown required sector rejected");
        var price=LootTable.Items(VesselType.Submarine).ToDictionary(i=>i,_=>1000);
        Check(price.Count>0,"embedded loot model loaded");
        Check(ItemYield.GilPerVisit(VesselType.Airship,0,100,100,price)==0,"no invented airship gil rates");
        var farm=RouteSearch.FindTop(data,request with{Prices=price},5);
        Check(farm.Count>0&&farm.All(r=>r.Gil>=0),"NPC model route search");
        var parts=PartOptimizer.Best(data,VesselType.Submarine,50,1,routes[0].Sectors,RouteGoal.ExpPerHour,true,5);
        Check(parts.Count>0&&parts.All(p=>p.Build.FitsCapacity&&p.Build.PartsAllowed&&p.Distance<=p.Build.Range),"build recommendations respect rank capacity range");
        var onlyCurrent=build.Parts.Select(p=>p.Id).ToHashSet();
        var owned=PartOptimizer.Best(data,VesselType.Submarine,50,1,routes[0].Sectors,RouteGoal.ExpPerHour,true,5,onlyCurrent);
        Check(owned.All(p=>p.Build.Parts.All(part=>onlyCurrent.Contains(part.Id))),"owned part filter respected");
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        try{RouteSearch.FindTop(data,request,5,cancel.Token);}catch(OperationCanceledException){n++;}
        Console.WriteLine($"PASS: {n} optimiser checks against game-data fixture");return n;
    }
}

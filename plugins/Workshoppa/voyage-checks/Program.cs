using Workshoppa.Voyages;
using System.Text.Json;
int checks=0;
void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
Vessel Vessel(VesselKind kind,int slot=0,long time=0)=>new(kind,slot,$"{kind}-{slot}",time,50,new ushort[]{30000,30000,30000,30000},true);
const long utc=100000;
var idle=Vessel(VesselKind.Airship);var done=idle with{ReturnTime=utc-1};var away=idle with{ReturnTime=utc+1000};var plan=new VesselPlan();
Check(VoyagePolicy.Eligible(idle,plan,true,true,utc),"idle redeploys");
Check(!VoyagePolicy.Eligible(idle,plan,true,false,utc),"collect only leaves idle vessel alone");
Check(VoyagePolicy.Eligible(done,plan,true,false,utc),"completed collection eligible");
Check(!VoyagePolicy.Eligible(done,plan,false,true,utc),"disabled collection not overridden");
Check(!VoyagePolicy.Eligible(away,plan,true,true,utc),"travelling never selected");
Check(!VoyagePolicy.Eligible(idle,new(){Enabled=false},true,true,utc),"disabled vessel ignored");
Check(VoyagePolicy.TryRatio("1 / 2,000",out var need,out var have)&&need==1&&have==2000,"localized resource grouping");
Check(!VoyagePolicy.TryRatio("missing",out _,out _),"unknown fuel not accepted");
var destinations=new[]{new Destination(0,VesselKind.Airship,0,"Sector one",1),new Destination(1,VesselKind.Submarine,1,"Shoals",1),new Destination(2,VesselKind.Submarine,1,"Deep sea",10),new Destination(3,VesselKind.Submarine,2,"Other map",1)};
Check(VoyagePolicy.ValidateRoute(new(){UseCustomRoute=true,Points=new(){0}},VesselKind.Airship,destinations,1)==null,"airship zero row is valid");
Check(VoyagePolicy.ValidateRoute(new(){UseCustomRoute=true,Points=new(){1,2}},VesselKind.Submarine,destinations,50)==null,"valid same map");
Check(VoyagePolicy.ValidateRoute(new(){UseCustomRoute=true,Points=new(){1,3}},VesselKind.Submarine,destinations,50)!=null,"mixed map rejected");
Check(VoyagePolicy.ValidateRoute(new(){UseCustomRoute=true,Points=new(){2}},VesselKind.Submarine,destinations,1)!=null,"rank enforced");
Check(VoyagePolicy.ValidateRoute(new(){UseCustomRoute=true,Points=new(){1,1}},VesselKind.Submarine,destinations,50)!=null,"duplicates rejected");
Check(VoyagePolicy.ValidateRoute(new(){UseCustomRoute=true},VesselKind.Submarine,destinations,50)!=null,"empty custom route rejected");
Check(idle.Key(1)!=idle.Key(2)&&idle.Key(1)!=(idle with{Kind=VesselKind.Submarine}).Key(1),"workshop and type scope independent");
long now=0;
void Pump(VoyageController c,int count=100){for(int i=0;i<count;i++){now+=250;c.Update(now,utc);}}
(FakeGame Game,VoyageController Controller,List<string> Notifications) Make(VoyageOptions? opts=null)
{var g=new FakeGame(destinations);var n=new List<string>();return(g,new(g,opts??new(),()=>null,n.Add),n);}
var (g,c,n)=Make();g.Vessels.Add(done);g.Vessels.Add(Vessel(VesselKind.Submarine));g.Vessels.Add(Vessel(VesselKind.Airship,1,utc+1000));c.Start(now);Pump(c);
Check(!c.Running&&g.Collects==1&&g.Deploys==2&&n.Count==0,"collect and repeat both types");
Check(!g.SelectedSlots.Contains((VesselKind.Airship,1)),"away ship not touched");
Check(g.Repeats==2,"each previous voyage selected once");
var after=g.Actions;Pump(c);Check(g.Actions==after,"idle never repeats actions");
var collectOnly=Make(new(){Redeploy=false});collectOnly.Game.Vessels.Add(done);collectOnly.Controller.Start(now);Pump(collectOnly.Controller);
Check(collectOnly.Game.Collects==1&&collectOnly.Game.Deploys==0&&!collectOnly.Controller.Running,"collect-only cycle");
var disabled=Make(new(){Airships=false});disabled.Game.Vessels.Add(idle);disabled.Game.Vessels.Add(Vessel(VesselKind.Submarine));disabled.Controller.Start(now);Pump(disabled.Controller);
Check(disabled.Game.Deploys==1&&!disabled.Game.SelectedSlots.Any(s=>s.Item1==VesselKind.Airship),"type toggle");
var noRepair=Make();noRepair.Game.Vessels.Add(idle with{Conditions=new ushort[]{0,30000,30000,30000}});noRepair.Controller.Start(now);Pump(noRepair.Controller);
Check(noRepair.Game.Deploys==0&&noRepair.Notifications.Count==1,"broken part stops and notifies");
var repairs=Make(new(){Repair=true});repairs.Game.Vessels.Add(idle with{Conditions=new ushort[]{0,0,30000,30000}});repairs.Controller.Start(now);Pump(repairs.Controller);
Check(repairs.Game.Repairs==2&&repairs.Game.Deploys==1&&repairs.Notifications.Count==0,"repair broken parts before repeat");
var fuel=Make();fuel.Game.Vessels.Add(idle);fuel.Game.Fuel=false;fuel.Controller.Start(now);Pump(fuel.Controller);
Check(fuel.Game.Deploys==0&&fuel.Notifications.Count==1,"fuel failure never dispatches");
var missing=Make();missing.Game.Vessels.Add(idle);missing.Game.Previous=false;missing.Controller.Start(now);Pump(missing.Controller);
Check(missing.Game.Deploys==0&&missing.Notifications.Count==1,"missing previous route stops");
var delayed=Make();delayed.Game.Vessels.Add(done);delayed.Game.DelayCollection=true;delayed.Controller.Start(now);Pump(delayed.Controller,20);
Check(delayed.Game.Collects==1&&delayed.Game.Deploys==0&&delayed.Controller.Running,"collection not reissued while game pending");
delayed.Game.FinishCollection();Pump(delayed.Controller);Check(delayed.Game.Deploys==1,"pending collection can complete");
var owner=Make();owner.Game.Vessels.Add(idle);owner.Controller.Start(now);Pump(owner.Controller,4);owner.Game.Owner=false;Pump(owner.Controller);
Check(owner.Game.Deploys==0&&owner.Notifications.Count==1,"selected vessel change aborts");
var zone=Make();zone.Game.Vessels.Add(idle);zone.Controller.Start(now);zone.Game.House=2;Pump(zone.Controller);
Check(zone.Game.Actions==0&&zone.Notifications.Count==1,"workshop change aborts before callback");
var customOptions=new VoyageOptions();customOptions.Vessels[idle.Key(1)]=new(){UseCustomRoute=true,Points=new(){0}};
var custom=Make(customOptions);custom.Game.Vessels.Add(idle);custom.Controller.Start(now);Pump(custom.Controller);
Check(custom.Game.Points.SequenceEqual(new uint[]{0})&&custom.Game.Deploys==1&&custom.Game.Repeats==0&&custom.Notifications.Count==0,"custom airship route replaces previous log");
var subV=Vessel(VesselKind.Submarine);customOptions=new(){Airships=false};customOptions.Vessels[subV.Key(1)]=new(){UseCustomRoute=true,Points=new(){2,1}};
var sub=Make(customOptions);sub.Game.Vessels.Add(subV);sub.Controller.Start(now);Pump(sub.Controller);
Check(sub.Game.Points.SequenceEqual(new uint[]{2,1})&&sub.Game.Deploys==1&&sub.Notifications.Count==0,"ordered submarine points");
var autoOpts=new VoyageOptions{AutoRunOnPanelOpen=true};var auto=Make(autoOpts);auto.Game.Vessels.Add(idle);Pump(auto.Controller);
after=auto.Game.Actions;Pump(auto.Controller);Check(auto.Game.Actions==after&&auto.Game.Deploys==1,"automatic visit runs once");
var stop=Make();stop.Game.Vessels.Add(idle);stop.Controller.Start(now);Pump(stop.Controller,4);stop.Controller.Stop();after=stop.Game.Actions;Pump(stop.Controller);Check(stop.Game.Actions==after,"manual stop prevents pending actions");
var restored=JsonSerializer.Deserialize<VoyageOptions>(JsonSerializer.Serialize(customOptions))!;Check(restored.Vessels[subV.Key(1)].Points.SequenceEqual(new uint[]{2,1}),"saved route round trip");
var optionsTarget=new VoyageOptions();var sameOptions=optionsTarget;optionsTarget.CopyFrom(customOptions);
Check(ReferenceEquals(optionsTarget,sameOptions)&&optionsTarget.Vessels[subV.Key(1)].Points.SequenceEqual(new uint[]{2,1}),"import preserves live options object");
customOptions.Vessels[subV.Key(1)].Points.Clear();
Check(optionsTarget.Vessels[subV.Key(1)].Points.Count==2,"imported plans are independent copies");
var historyDir=Path.Combine(Path.GetTempPath(),"Workshoppa-history-check-"+Guid.NewGuid().ToString("N"));
var historyFile=Path.Combine(historyDir,"history.json");var errors=new List<string>();
var history=new VoyageHistory(historyFile,errors.Add);var alerts=new List<string>();
history.Observe(1,new[]{away},utc,_=>null);history.NotifyDue(utc,true,alerts.Add);
Check(alerts.Count==0,"no early return alert");
history.NotifyDue(utc+1001,false,alerts.Add);Check(alerts.Count==0,"notification toggle respected");
history.NotifyDue(utc+1001,true,alerts.Add);history.NotifyDue(utc+1002,true,alerts.Add);
Check(alerts.Count==1,"return notification once");
var loadedHistory=new VoyageHistory(historyFile,errors.Add);loadedHistory.NotifyDue(utc+1003,true,alerts.Add);
Check(alerts.Count==1,"notification dedup survives reload");
history.Observe(1,new[]{done},utc,_=>new(){new VoyageLoot(8143,"Clear Demimateria II",2,false,1000)});
Check(history.Records.Count==0,"visible results alone not counted as collected");
history.Observe(1,new[]{idle},utc,_=>null);history.Observe(1,new[]{idle},utc,_=>null);
Check(history.Records.Count==1&&history.Records[0].VendorValue==2000,"confirmed collection counted once with gross value");
var persisted=new VoyageHistory(historyFile,errors.Add);Check(persisted.Records.Count==1,"loot history persists");
history.Export(Path.Combine(historyDir,"export.csv"));Check(File.ReadAllText(Path.Combine(historyDir,"export.csv")).Contains("Clear Demimateria II"),"CSV loot export");
Check(errors.Count==0,"history persistence no errors");
checks+=OptimizerChecks.Run();
Console.WriteLine($"PASS: {checks} voyage policy/state-machine checks; native UI behavior remains untested.");

sealed class FakeGame(IReadOnlyList<Destination> destinations):IVoyageGame
{
    public bool Available{get;set;}=true;public ulong Character{get;set;}=1;public ulong House{get;set;}=1;
    public string Window="root";public VesselKind Kind;public int Slot;public bool Owner=true,Fuel=true,Previous=true,DelayCollection;
    public List<Vessel> Vessels=new();public List<(VesselKind,int)> SelectedSlots=new();public List<uint> Points=new();
    public int Actions,Collects,Deploys,Repeats,Repairs;
    public IReadOnlyList<Destination> Destinations=>destinations;public bool Root=>Window=="root";public bool AnyWindow=>Window!="";public bool Cutscene=>false;
    public bool Result=>Window=="result";public bool Detail=>Window=="detail";public bool Editor=>Window=="editor";public bool MapSelect=>Window=="map";public bool RepairWindow=>Window=="repair";public bool VesselMenu=>Window=="vessel";
    public uint SelectedMap{get;set;}=1;
    public IReadOnlyList<Vessel> Roster(VesselKind k)=>Vessels.Where(v=>v.Kind==k).ToArray();
    public bool Picker(VesselKind k)=>Window=="picker"&&Kind==k;
    public bool SelectedVesselMatches(Vessel v)=>Owner&&Kind==v.Kind&&Slot==v.Slot;
    private int CurrentIndex=>Vessels.FindIndex(v=>v.Kind==Kind&&v.Slot==Slot);
    public bool SelectKind(VesselKind k){Actions++;Kind=k;Window="picker";return true;}
    public bool SelectVessel(Vessel v){Actions++;Slot=v.Slot;SelectedSlots.Add((v.Kind,v.Slot));Window=v.ReturnTime>0?"result":"vessel";return true;}
    public bool Collect(){Actions++;Collects++;if(!DelayCollection)FinishCollection();return true;}
    public void FinishCollection(){Vessels[CurrentIndex]=Vessels[CurrentIndex] with{ReturnTime=0};Window="vessel";}
    public bool PreviousLog(){Actions++;Window="result";return true;}
    public bool Repeat(out string? error){error=Previous?null:"no previous route";if(!Previous)return false;Actions++;Repeats++;Window="detail";return true;}
    public bool Deploy(out string? error){error=Fuel?null:"insufficient fuel";if(!Fuel)return false;Actions++;Deploys++;Vessels[CurrentIndex]=Vessels[CurrentIndex] with{ReturnTime=101000};Window="picker";return true;}
    public bool QuitVessel(){Actions++;Window="picker";return true;}public bool QuitPicker(){Actions++;Window="root";return true;}
    public bool OpenRepair(VesselKind k){Actions++;Window="repair";return true;}
    public bool RequestRepair(Vessel v,int part,out string? error){error=null;Actions++;Window="yesno";return true;}
    public bool ConfirmRepair(Vessel v,int part,out string? error){error=null;Actions++;Repairs++;Vessels[CurrentIndex].Conditions[part]=30000;Window="repair";return true;}
    public bool CloseRepair(){Actions++;Window="vessel";return true;}
    public bool EnsureBuild(Vessel vessel,IReadOnlyList<uint> parts,out string? error){error=null;return true;}
    public void CancelBuild(){}
    public bool OpenRoute(){Actions++;Points.Clear();Window=Kind==VesselKind.Submarine?"map":"editor";return true;}
    public bool PickMap(uint map,out string? error){Actions++;SelectedMap=map;error=null;Window="editor";return true;}
    public int SelectedCount(VesselKind kind)=>Points.Count;
    public string RouteDistance=>$"{Points.Count*10}/100";
    public bool PickPoint(Destination point,out string? error){Actions++;Points.Add(point.Id);error=null;return true;}
    public bool ReviewRoute(out string? error){Actions++;error=null;Window="detail";return true;}
}

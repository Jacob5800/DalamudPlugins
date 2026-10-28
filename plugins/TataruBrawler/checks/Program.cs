using TataruBrawler;
int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
foreach (uint id in new uint[] {49529,49532,49702})
{
    var p = new EncounterPolicy();
    Check(!p.Update(1000, new[] {new BlockingCast(id, 501)}), "Do not block early");
    Check(p.Update(2000, new[] {new BlockingCast(id, 500)}), "Known ID triggers");
    Check(p.Paused(6499) && !p.Paused(6500), "Four seconds after resolution");
    Check(!p.Update(2100, new[] {new BlockingCast(id, 400)}), "No repeated start notification");
    p.Reset(); Check(!p.Paused(2100), "Reset clears hold");
}
var guard = new EncounterPolicy();
Check(!guard.Update(0,new[] {new BlockingCast(123,0),new BlockingCast(49529,float.NaN)}), "Unknown and invalid cast ignored");
var totals = new RewardTotals {Games=10,Mgp=1000,Mgc=20};
var s = new RewardSession(totals);
Check(!s.Observe(0,true,false,5000,100), "Entry no reward");
Check(s.Observe(100,true,true,5100,102), "Result counted");
Check(!s.Observe(200,true,true,5100,102), "Repeated result not counted");
Check(s.Observe(300,false,false,5150,103), "Delayed lobby update counted");
Check(s.Session.Games==1 && s.Session.Mgp==150 && s.Session.Mgc==3, "Session totals correct");
Check(totals.Games==11 && totals.Mgp==1150 && totals.Mgc==23, "Saved total accumulation");
Check(!s.Observe(15200,false,false,9999,999), "Ignore outside settlement window");
s.Observe(20000,true,false,100,10); s.Observe(20100,false,false,100,10);
Check(s.Session.Games==1, "Aborted visit ignored");
s.Observe(21000,true,false,100,10); s.Observe(21100,true,true,100,10);
Check(s.Session.Games==2 && s.Session.Mgp==150, "Capped or zero currency still counts completed game");
s.EndVisit(); s.Observe(22000,true,false,null,null); s.Observe(22100,true,true,300,30);
Check(s.Session.Games==3 && s.Session.Mgp==150, "Missing baseline cannot invent rewards");
s.EndVisit(); s.Observe(23000,true,false,500,50); s.Observe(23100,true,true,400,40);
Check(s.Session.Mgp==150 && s.Session.Mgc==3, "Currency decrease not negative reward");
Console.WriteLine($"{checks} checks passed.");

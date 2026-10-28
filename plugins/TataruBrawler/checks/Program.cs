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
Check(BackgroundKeyPolicy.Allowed('a') && BackgroundKeyPolicy.Allowed('.') && BackgroundKeyPolicy.Allowed('\r'), "Background supports English and Enter");
Check(!BackgroundKeyPolicy.Allowed('\u001b') && !BackgroundKeyPolicy.Allowed('\u0006') && !BackgroundKeyPolicy.Allowed('あ'), "Background rejects escape, IME and Japanese keys");
Check(BackgroundText.TryNext("he", 'l', "hello", out var next) && next=="hel", "Background prefix progression");
Check(!BackgroundText.TryNext("other", 'a', "hello", out _), "Unrelated field text rejected");
Check(!BackgroundText.TryNext("hell", '\r', "hello", out _), "Partial answer cannot submit");
Check(BackgroundText.TryNext("hello", '\r', "hello", out _), "Complete answer can submit");
var typing = new TypingSession((min,max)=>min);
string field = ""; bool submitted = false;
typing.Start(0);
for (long t=0;t<2000 && !submitted;t+=50)
    typing.Tick(t,true,"hello",true,20,20,key=>
    {
        if(!BackgroundText.TryNext(field,key,"hello",out var changed)) return false;
        if(key=='\r') submitted=true; else field=changed;
        return true;
    },false,false,field,0);
Check(submitted && field=="hello" && typing.Running, "Typing session completes background character/submit protocol");
var plain = TypingPlan.Create("don't-stop?!, now.",true)!;
Check(plain.Expected=="dontstop, now." && plain.Keys=="dontstop, now." && plain.Original=="don't-stop?!, now.", "Optional punctuation skipped, source and required punctuation preserved");
Check(TypingPlan.Create("don't-stop?!",false)!.Expected=="don't-stop?!", "Skipping disabled preserves original");
Check(TypingPlan.Create("あ!",true)!.Expected=="あ!", "Japanese punctuation unchanged");
var shortened = new TypingSession((min,max)=>min) {SkipOptionalPunctuation=true};
shortened.Start(0); field=""; submitted=false;
for(long t=0;t<2000 && !submitted;t+=50)
    shortened.Tick(t,true,"don't!",true,20,20,key=>
    {
        Check(shortened.CurrentWord=="don't!", "Visible prompt identity retained");
        if(!BackgroundText.TryNext(field,key,shortened.ExpectedAnswer,out var changed))return false;
        if(key=='\r')submitted=true;else field=changed;
        return true;
    },false,false,field,0);
Check(submitted && field=="dont", "Shortened background answer completes");
var warning = new MgcWarning();
Check(!warning.Update(89,100,true,90), "Below cap threshold silent");
Check(warning.Update(90,100,true,90), "Warn on reaching threshold");
Check(!warning.Update(91,100,true,90), "No repeated near-cap chat");
Check(!warning.Update(null,null,true,90) && !warning.Update(91,100,true,90), "Temporary missing data does not rewarn");
Check(warning.Update(100,100,true,90), "Warn at hard cap");
Check(!warning.Update(100,100,true,90), "No repeated cap chat");
warning.Update(10,100,true,90);
Check(warning.Update(90,100,true,90), "Warn again after spending and refilling");
Check(!warning.Update(100,100,false,90) && warning.Message==null, "Warning toggle disables messages");
var recovery = new AutomaticRecovery();
Check(!recovery.ShouldResume(0,true,true,false,"SendFailed",true,false,false), "Recovery waits for stable input");
Check(recovery.ShouldResume(2000,true,true,false,"SendFailed",true,false,false), "Transient failure resumes");
Check(!recovery.ShouldResume(3000,true,true,false,"SendFailed",true,false,false), "Recovery throttled");
foreach(var status in new[]{"Stopped","Completed","Unsupported","NoProgress","UserPaused"})
 Check(!recovery.ShouldResume(10000,true,true,false,status,true,false,false), "Never restart "+status);
Check(!recovery.ShouldResume(12000,false,true,false,"SendFailed",true,false,false), "Manual stop disables recovery");
Check(!recovery.ShouldResume(13000,true,true,false,"SendFailed",true,true,false), "Manual pause respected");
Check(!recovery.ShouldResume(14000,true,false,false,"SendFailed",true,false,false), "Outside duty not resumed");
Console.WriteLine($"{checks} checks passed.");

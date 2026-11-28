using System.Text.Json;
using RetainerPricer;

var checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
void Reject(Action action, string name) { try { action(); } catch (InvalidOperationException) { checks++; return; } throw new Exception(name); }
var c = new PluginConfig { AutoRunFullSequence = true, ExcludedItemIds = [42], BatchSaleQuantities = new() { [42] = 3 } };
c.UniversalisPendingItemIds.Add(99);
Check(SettingsProfiles.Initialize(c) && c.ActiveSettingsProfile == "Default", "existing setup becomes Default");
Check(!SettingsProfiles.Initialize(c), "initialization is idempotent");
SettingsProfiles.Create(c, "Gathering");
c.AutoRunFullSequence = false; c.ExcludedItemIds.Add(43); c.BatchSaleQuantities[42] = 8;
SettingsProfiles.Switch(c, "Default");
Check(c.AutoRunFullSequence && c.ExcludedItemIds.SequenceEqual(new uint[] {42}) && c.BatchSaleQuantities[42] == 3, "settings lists quantities isolated");
Check(c.UniversalisPendingItemIds.SequenceEqual(new uint[] {99}) && c.SettingsProfiles.Count == 2, "shared caches and library preserved");
SettingsProfiles.Switch(c, "Gathering");
Check(!c.AutoRunFullSequence && c.ExcludedItemIds.Contains(43) && c.BatchSaleQuantities[42] == 8, "switch saves outgoing edits");
var reloaded = JsonSerializer.Deserialize<PluginConfig>(JsonSerializer.Serialize(c))!;
SettingsProfiles.Initialize(reloaded); SettingsProfiles.Switch(reloaded, "Default");
Check(reloaded.ActiveSettingsProfile == "Default" && reloaded.ExcludedItemIds.Count == 1, "file round trip retains profiles");
var exported = SettingsBackup.Export(c)["Settings"]!.AsObject();
Check(!exported.ContainsKey("SettingsProfiles") && !exported.ContainsKey("ActiveSettingsProfile") && !exported.ContainsKey("UniversalisPendingItemIds"), "portable backups exclude library and cache");
SettingsBackup.Apply(reloaded, exported, false);
Check(reloaded.SettingsProfiles.Count == 2 && reloaded.ActiveSettingsProfile == "Default" && reloaded.ExcludedItemIds.Contains(43), "import updates current settings and preserves library");
SettingsProfiles.Rename(c, "Gathering", "Farm");
Check(c.ActiveSettingsProfile == "Farm" && !c.SettingsProfiles.ContainsKey("Gathering"), "active rename");
Reject(() => SettingsProfiles.Create(c," default "), "case-insensitive duplicate rejected");
Reject(() => SettingsProfiles.Create(c,"  "), "empty name rejected");
Reject(() => SettingsProfiles.Delete(c,"Farm"), "active delete rejected");
SettingsProfiles.Delete(c,"Default"); Check(c.SettingsProfiles.Count == 1,"inactive delete");
c.SettingsProfiles["Broken"] = "invalid json";
var before = c.ExcludedItemIds.ToArray();
try { SettingsProfiles.Switch(c,"Broken"); throw new Exception("invalid accepted"); } catch (JsonException) { checks++; }
Check(c.ActiveSettingsProfile == "Farm" && before.SequenceEqual(c.ExcludedItemIds),"invalid profile leaves settings intact");
Check(ShiftStopPolicy.ShouldStop(true,true,true,false,true), "standalone venture stop");
Check(ShiftStopPolicy.ShouldStop(true,true,true,true,false), "full sequence stop");
Check(!ShiftStopPolicy.ShouldStop(false,true,true,true,true), "toggle respected");
Check(!ShiftStopPolicy.ShouldStop(true,false,true,true,true), "requires shift");
Check(!ShiftStopPolicy.ShouldStop(true,true,false,true,true), "bell scope");
Check(!ShiftStopPolicy.ShouldStop(true,true,true,false,false), "idle ignored");
var teleportRetry = new TeleportRetry();
var tripClock = DateTimeOffset.UnixEpoch.AddHours(1);
Check(!teleportRetry.Waiting(tripClock) && !teleportRetry.Exhausted, "first teleport is immediate");
teleportRetry.Requested(tripClock);
Check(teleportRetry.Waiting(tripClock.AddSeconds(4)) && !teleportRetry.Waiting(tripClock.AddSeconds(5)), "teleport retried after quiet period");
teleportRetry.Busy(tripClock.AddSeconds(20));
Check(teleportRetry.Waiting(tripClock.AddSeconds(24)), "casting or loading restarts the wait");
teleportRetry.Requested(tripClock.AddSeconds(30)); teleportRetry.Requested(tripClock.AddSeconds(40));
Check(teleportRetry.Exhausted && teleportRetry.Waiting(tripClock.AddSeconds(41)), "last attempt still gets time to finish");
teleportRetry.Reset();
Check(!teleportRetry.Exhausted && !teleportRetry.Waiting(tripClock), "new trip resets attempts");
var rejected = new ListingRunRejections();
rejected.Record(42,false,new PriceProposal(1,0,1,"one gil",Rejection: PriceRejection.CannotUndercutOneGil));
Check(rejected.Contains(42,false) && !rejected.Contains(42,true), "one-gil rejection shared across retainers but quality separate");
rejected.Record(43,true,new PriceProposal(50,0,1,"minimum",Rejection: PriceRejection.BelowMinimum));
Check(rejected.Contains(43,true), "minimum-price rejection remembered");
rejected.Record(44,false,new PriceProposal(0,0,0,"temporary HTTP failure"));
Check(!rejected.Contains(44,false), "temporary failures not remembered");
rejected.Record(45,false,new PriceProposal(10,9,1,null));
Check(!rejected.Contains(45,false), "successful quote not rejected");
rejected.Clear();
Check(!rejected.Contains(42,false) && !rejected.Contains(43,true), "new run clears rejection memory");
var clock = DateTimeOffset.UtcNow;
PriceSnapshot Quote(uint price) => new(42,74,PriceSource.Local,clock,[new MarketListing(42,false,price,1,99,false)]);
var one = PriceCalculator.Calculate(Quote(1),42,74,false,new HashSet<ulong>(),1,clock,null);
Check(one.Rejection == PriceRejection.CannotUndercutOneGil, "real one-gil quote classified");
var low = PriceCalculator.Calculate(Quote(100),42,74,false,new HashSet<ulong>(),100,clock,null);
Check(low.Rejection == PriceRejection.BelowMinimum, "real below-minimum quote classified");
var match = PriceCalculator.Calculate(Quote(1),42,74,false,new HashSet<ulong>(),1,clock,null,strategy:PriceStrategy.MatchLowest);
Check(match.CanApply && match.Rejection == PriceRejection.None, "allowed matching one-gil not rejected");
Check(BelowVendorPolicy.RequiresReview(false,0,799,1000), "new listing warns below vendor example");
Check(BelowVendorPolicy.RequiresReview(false,0,800,1000), "matching below-vendor price warns");
Check(BelowVendorPolicy.RequiresReview(false,0,1000,1000), "equal vendor price nets less after tax");
Check(BelowVendorPolicy.RequiresReview(false,0,133,126), "Piety Materia VI example nets no more than NPC");
Check(!BelowVendorPolicy.RequiresReview(false,0,134,126), "first price netting more than NPC allowed");
Check(BelowVendorPolicy.NetAfterTax(999_999_999) == 949_999_999, "maximum price tax does not overflow");
Check(!BelowVendorPolicy.RequiresReview(false,0,1100,1000), "above vendor price allowed");
Check(!BelowVendorPolicy.RequiresReview(false,0,1,0), "no NPC sale value does not warn");
Check(BelowVendorPolicy.RequiresReview(true,1200,799,1000), "existing reduction still warns");
Check(!BelowVendorPolicy.RequiresReview(true,700,799,1000), "existing increases keep established behavior");
var pendingSlots = new[] { (Item: 42, Slot: 3), (Item: 43, Slot: 4), (Item: 42, Slot: 5) };
var refreshedSlots = new[] { (Item: 42, Slot: 0), (Item: 42, Slot: 1), (Item: 43, Slot: 2) };
Check(ListingRefresh.TryRemap(pendingSlots, refreshedSlots, x => x.Item, out var mapped) &&
    mapped.Select(x => x.Slot).SequenceEqual(new[] { 0, 2, 1 }), "withdrawal refresh maps changed slots and consumes duplicates once");
Check(!ListingRefresh.TryRemap(pendingSlots, refreshedSlots.Take(2).ToArray(), x => x.Item, out mapped) && mapped.Count == 0,
    "changed inventory rejects entire remap");
Check(ListingRefresh.TryRemap(Array.Empty<int>(), Array.Empty<int>(), x => x, out var empty) && empty.Count == 0, "last sold listing can finish traversal");
// Live World repricing must exclude every owned retainer and separate HQ/NQ.
var ownIds = new HashSet<ulong> { 21, 22 };
PriceSnapshot Live(params MarketListing[] listings) => new(42, 74, PriceSource.Local, clock, listings);
MarketListing Listing(uint price, ulong owner, bool hq = false) => new(42, hq, price, 1, owner, false);
PriceProposal LivePrice(PriceSnapshot snapshot, bool hq = false) =>
    PriceCalculator.Calculate(snapshot, 42, 74, hq, ownIds, 1, clock, null);
var localQuote = Live(Listing(10, 21), Listing(20, 22), Listing(100, 99), Listing(5, 98, true));
var localPrice = LivePrice(localQuote);
Check(localPrice.CanApply && localPrice.SuggestedPrice == 99 && localPrice.MatchingListings == 1,
    "live World pricing ignores both owned retainers and other-quality offers");
Check(LivePrice(localQuote, true).SuggestedPrice == 4, "live HQ pricing uses HQ offers");
Check(!LivePrice(Live(Listing(10, 21), Listing(20, 22))).CanApply, "own-only local market leaves price unchanged");
Check(!LivePrice(Live()).CanApply, "empty local market leaves price unchanged");
Check(!LivePrice(Live(Listing(100, 0))).CanApply, "unknown local owner blocks repricing");
Check(!LivePrice(localQuote with { IsComplete = false }).CanApply, "partial local response blocks repricing");
Check(!LivePrice(localQuote with { ItemId = 43 }).CanApply, "wrong-item local response blocks repricing");
Check(!LivePrice(localQuote with { WorldId = 75 }).CanApply, "wrong-world local response blocks repricing");
var vendorOnly = new PluginConfig { DontMarketItemIds = [0, 42, 42], RetainerSaleWhitelistItemIds = [42] };
vendorOnly.Normalize();
Check(vendorOnly.DontMarketItemIds.SequenceEqual(new uint[] {42}), "Don't market normalizes IDs");
Check(!vendorOnly.ExcludedItemIds.Contains(42) && !vendorOnly.DontVendorItemIds.Contains(42) &&
    vendorOnly.RetainerSaleWhitelistItemIds.Contains(42), "vendor-only configuration keeps market blocking separate from sale protections");
var vendorOnlyRestored = new PluginConfig();
SettingsBackup.Apply(vendorOnlyRestored, SettingsBackup.Export(vendorOnly)["Settings"]!.AsObject(), false);
Check(vendorOnlyRestored.DontMarketItemIds.Contains(42) && vendorOnlyRestored.RetainerSaleWhitelistItemIds.Contains(42),
    "Don't market and vendor whitelist survive settings restore");
var visibility = new PluginConfig();
Check(!visibility.ShowSniper && !visibility.ShowDesynthesisList && visibility.ShowDebugStatus,
    "visibility defaults hide Sniper/Desynthesis and show Debug status");
visibility.ShowSniper = visibility.ShowDesynthesisList = true;
visibility.ShowDebugStatus = false;
var restoredVisibility = new PluginConfig();
SettingsBackup.Apply(restoredVisibility, SettingsBackup.Export(visibility)["Settings"]!.AsObject(), false);
Check(restoredVisibility.ShowSniper && restoredVisibility.ShowDesynthesisList && !restoredVisibility.ShowDebugStatus,
    "visibility choices including false survive settings backup/restore");
Check(new PluginConfig().ShowDontMarket, "Don't market visible by default");
var retry = new MarketComparisonRetry();
Check(!retry.TrySchedule(1, clock) && retry.Attempts == 0, "unknown market errors are not retried");
Check(retry.TrySchedule(0x70000003, clock) && !retry.IsReady(clock.AddMilliseconds(499)), "market retry waits at least half a second");
Check(retry.IsReady(clock.AddMilliseconds(500)), "market retry ready after half a second");
Check(retry.TrySchedule(0x70000003, clock.AddSeconds(1)) && !retry.IsReady(clock.AddMilliseconds(1999)), "second rejection gets a longer cooldown");
Check(retry.IsReady(clock.AddMilliseconds(2000)), "second retry is ready after 1000 ms");
Check(retry.TrySchedule(0x70000003, clock.AddSeconds(2)) && retry.DelayMilliseconds == 2000, "third retry waits two seconds");
Check(!retry.TrySchedule(0x70000003, clock.AddSeconds(5)), "third retry is the last attempt");
retry.Reset();
Check(retry.Attempts == 0 && retry.IsReady(clock), "next listing gets a fresh bounded retry allowance");
Check(!new PluginConfig().ShowAutomationList, "Automation tab hidden by default");
var historyOnly = Live() with { Sales = [
    new MarketSale(false, 50, 74, "Home", clock.AddDays(-1)),
    new MarketSale(false, 70, 74, "Home", clock.AddDays(-2)),
    new MarketSale(false, 60, 74, "Home", clock.AddDays(-45)),
    new MarketSale(true, 500, 74, "Home", clock.AddDays(-1)),
    new MarketSale(false, 999, 74, "Home", clock.AddDays(-46))] };
PriceProposal HistoryPrice(PriceSnapshot quote, bool allow = true) =>
    PriceCalculator.Calculate(quote, 42, 74, false, ownIds, 1, clock, null, requireRecentSale: false, allowSalesFallback: allow);
var historyPrice = HistoryPrice(historyOnly);
Check(historyPrice.CanApply && historyPrice.UsedSalesFallback && historyPrice.SaleHistoryMedian == 60 && historyPrice.SuggestedPrice == 59,
    "empty local market uses matching-quality recent sale median with selected undercut rule");
foreach (var count in new[] { 0, 1, 2 })
{
    var limited = HistoryPrice(historyOnly with { Sales = historyOnly.Sales!.Take(count).ToArray() });
    Check(!limited.CanApply && limited.Rejection == PriceRejection.InsufficientHistory && limited.MatchingSales == count,
        $"{count} recent matching sales require user review");
}
Check(!HistoryPrice(historyOnly, false).CanApply, "history fallback requires caller permission");
Check(!HistoryPrice(historyOnly with { IsComplete = false }).CanApply, "failed or partial market response cannot use history");
Check(!HistoryPrice(historyOnly with { Sales = [] }).CanApply, "empty sale history leaves price unchanged");
Check(!HistoryPrice(historyOnly with { Sales = [new MarketSale(false,50,74,"Home",clock.AddDays(-46))] }).CanApply,
    "expired local sales cannot set price");
Check(!HistoryPrice(historyOnly with { Sales = [new MarketSale(true,50,74,"Home",clock.AddDays(-1))] }).CanApply,
    "HQ history cannot price NQ item");
var liveCompetitor = HistoryPrice(historyOnly with { Listings = [Listing(100,99)] });
Check(liveCompetitor.CanApply && !liveCompetitor.UsedSalesFallback && liveCompetitor.SuggestedPrice == 99,
    "live competitor price takes priority over history");
var suspiciousHistory = historyOnly with { Sales = new uint[] { 1, 95000, 100000, 105000, 1000000 }
    .Select(price => new MarketSale(false, price, 74, "Home", clock.AddDays(-1))).ToArray() };
Check(HistoryPrice(suspiciousHistory).Rejection == PriceRejection.UnusualHistoryPrice,
    "outlying history requires approval instead of automatic fallback");
foreach (var price in new[] { 24999u, 500001u })
{
    var result = HistoryPrice(suspiciousHistory with { Listings = [Listing(price,99)] });
    Check(result.CanApply && !result.UsedSalesFallback && result.SuggestedPrice == price - 1,
        $"eligible competitor at {price} bypasses sale-history review");
}
Check(PriceHistoryFallback.ShouldFetch(Live(),42,74,false,ownIds,clock), "complete empty live market starts history fallback");
Check(!PriceHistoryFallback.ShouldFetch(Live() with { Listings = [Listing(100,99)] },42,74,false,ownIds,clock), "competitors prevent remote fallback");
Check(!PriceHistoryFallback.ShouldFetch(Live() with { IsComplete = false },42,74,false,ownIds,clock), "incomplete live market cannot start history fallback");
var homeHistory = historyOnly with { Source = PriceSource.Universalis, Listings = [Listing(999999,99)] };
var combined = PriceHistoryFallback.Attach(Live(),homeHistory);
Check(combined.Source == PriceSource.Local && combined.Listings.Count == 0 && HistoryPrice(combined).SuggestedPrice == 59,
    "fallback uses remote history but never remote listings");
try { PriceHistoryFallback.Attach(Live(),homeHistory with { WorldId = 75 }); throw new Exception("Wrong world accepted"); }
catch (InvalidOperationException) { Check(true,"wrong-world history rejected"); }
try { PriceHistoryFallback.Attach(Live(),homeHistory with { DataCenterName = "Light" }); throw new Exception("DC history accepted"); }
catch (InvalidOperationException) { Check(true,"DC history cannot replace home-world history"); }
Check(PriceHistoryFallback.ShouldFetch(Live() with { Listings = [Listing(100,21)] },42,74,false,ownIds,clock),
    "own-retainer stock alone allows history fallback");
Check(PriceHistoryFallback.ShouldFetch(Live() with { Listings = [Listing(100,99) with { IsHq = true }] },42,74,false,ownIds,clock),
    "other quality does not block history fallback");
Check(!PriceHistoryFallback.ShouldFetch(Live() with { Listings = [Listing(100,0)] },42,74,false,ownIds,clock),
    "missing ownership cannot become empty-market fallback");
// Thinly listed items are compared with real sales before a price is applied.
{
    var sold = new uint[] { 1000, 1100, 1200 }.Select(price => new MarketSale(false, price, 74, "Home", clock.AddDays(-1))).ToArray();
    PriceProposal Thin(uint price, MarketSale[]? sales, int competitors = 1) => PriceCalculator.Calculate(
        historyOnly with { Sales = sales, Listings = Enumerable.Range(0, competitors).Select(i => Listing(price, (ulong)(200 + i))).ToList() },
        42, 74, false, ownIds, 1, clock, null, requireRecentSale: false, allowSalesFallback: true, checkThinMarket: true);
    var bait = Thin(10_000_033, sold);
    Check(!bait.CanApply && bait.Rejection == PriceRejection.FarAboveSales && bait.SuggestedPrice == 10_000_032 && bait.SaleHistoryMedian == 1100,
        "a lone bait listing is held for review");
    Check(Thin(2201, sold).CanApply, "exactly 200% of the median is allowed");
    Check(Thin(2202, sold).Rejection == PriceRejection.FarAboveSales, "just over 200% is held");
    Check(Thin(10_000_033, sold, 4).CanApply, "four competitors are not treated as a thin market");
    Check(Thin(100_001, []).Rejection == PriceRejection.FarAboveSales, "100,000 gil without sales is held");
    Check(Thin(100_000, []).CanApply, "99,999 gil without sales is allowed");
    Check(PriceHistoryFallback.ShouldFetch(Live() with { Listings = [Listing(100,99)] },42,74,false,ownIds,clock, checkThinMarket: true),
        "a thin market fetches sales history");
}
{
    var history = new GameSaleHistory();
    var sold = new[] { new MarketSale(false, 1100, 0, "Home", clock.AddDays(-1)) };
    history.Record(42, sold, clock);
    Check(history.TryGet(42, clock, out var seen) && seen.Count == 1, "game sale history for this request is used");
    Check(!history.TryGet(42, clock.AddSeconds(1), out _) && !history.TryGet(43, clock, out _), "older or other-item history is ignored");
    var attached = GameSaleHistory.Attach(Live(), sold);
    Check(attached.Source == PriceSource.Local && attached.MostRecentSaleAt == clock.AddDays(-1), "game history attaches to live listings");
}
{
    var declines = new ListingRunRejections();
    declines.Decline(42, false);
    declines.Clear();
    Check(declines.Declined(42, false) && !declines.Declined(42, true), "a declined item stays skipped between phases, per quality");
    declines.ForgetDeclined();
    Check(!declines.Declined(42, false), "a new run asks again");
}
{
    var fake = new FakeDesynthesis();
    var step = new DesynthesisStep(fake);
    var at = DateTimeOffset.UnixEpoch;
    step.Begin(at);
    for (var i = 0; i < 20 && step.IsRunning; i++) step.Update(at = at.AddSeconds(4));
    Check(step.Succeeded && string.Join(",", fake.Log) == "close,start,bell", "desynthesis leaves the bell, runs, then reopens it");
    fake = new FakeDesynthesis { Fail = true };
    step = new DesynthesisStep(fake);
    step.Begin(at);
    for (var i = 0; i < 20 && step.IsRunning; i++) step.Update(at = at.AddSeconds(4));
    Check(!step.Succeeded && !step.IsRunning && !fake.Log.Contains("bell"), "a failed desynthesis stops the sequence");
    Check(!new PluginConfig().AutoDesynthesis, "automatic desynthesis is off by default");
}
{
    var broken = new[] { new BrokenPart(0, 6), new BrokenPart(2, 6) };
    Check(SubmarineRepairPlan.Shortfall(broken, 12) == 0, "exactly enough repair materials is enough");
    Check(SubmarineRepairPlan.Shortfall(broken, 7) == 5, "the missing materials are counted");
    Check(SubmarineRepairPlan.Shortfall([], 0) == 0, "nothing broken needs nothing");
}
var runCache = new RunPriceCache();
var runKey = new PriceCacheKey(1,74,42,false,PriceSource.Local,"world");
runCache.Remember(runKey,historyOnly);
Check(runCache.TryGet(runKey,clock,null,out var cachedRunQuote) && ReferenceEquals(cachedRunQuote,historyOnly), "run cache reuses original completed quote");
Check(!runCache.TryGet(runKey with { IsHq = true },clock,null,out _), "run cache separates HQ from NQ");
Check(!runCache.TryGet(runKey with { ContentId = 2 },clock,null,out _), "run cache separates characters");
Check(!runCache.TryGet(runKey with { WorldId = 75 },clock,null,out _), "run cache separates worlds");
Check(!runCache.TryGet(runKey with { Source = PriceSource.Universalis, Scope = "dc:Light" },clock,null,out _), "run cache separates sources and scopes");
runCache.Remember(runKey,historyOnly with { IsComplete = false });
Check(runCache.TryGet(runKey,clock,null,out cachedRunQuote) && cachedRunQuote.IsComplete, "incomplete results cannot replace cached quote");
runCache.Remove(runKey);
Check(!runCache.TryGet(runKey,clock,null,out _), "explicit price recheck invalidates cached quote");
runCache.Remember(runKey,historyOnly);
Check(!runCache.TryGet(runKey,clock.AddMinutes(2),TimeSpan.FromMinutes(1),out _), "configured age limit invalidates run quote");
runCache.Remember(runKey,historyOnly);
runCache.Clear();
Check(!runCache.TryGet(runKey,clock,null,out _), "next automation session starts without prior quote");
var automationSummary = new AutomationRunSummary();
automationSummary.BeginPhase("New listings");
automationSummary.CacheHits = 3;
automationSummary.Listed = 2;
automationSummary.Skip("Price review declined", "Item A");
automationSummary.Skip("Price review declined", "Item B");
automationSummary.BeginPhase("Update existing listings");
automationSummary.Complete("Stopped", "Item dialog changed");
Check(automationSummary.Finished && automationSummary.Outcome == "Stopped" && automationSummary.Detail == "Item dialog changed",
    "stopped automation retains failure details");
Check(automationSummary.CacheHits == 3 && automationSummary.Listed == 2 && automationSummary.Phases.Count == 2,
    "automation summary retains counters and completed/active phases");
Check(automationSummary.SkipReasons["Price review declined"] == 2 && automationSummary.SkippedItems.Count == 2,
    "skip reasons are grouped with item details");
automationSummary.Complete("Completed", "late callback");
automationSummary.Skip("late skip");
Check(automationSummary.Outcome == "Stopped" && automationSummary.SkipReasons.Count == 1,
    "finished automation summary ignores repeated completion and late skips");
Check(new AutomationRunSummary().CacheHits == 0, "new automation summary starts with fresh counters");
var recoverySummary = new AutomationRunSummary();
var hqKey = runKey with { IsHq = true };
recoverySummary.SkipLookup(runKey, "Lookup failed", "Item A");
recoverySummary.SkipLookup(runKey, "Lookup failed", "Item A");
recoverySummary.SkipLookup(hqKey, "Lookup failed", "Item A");
recoverySummary.Skip("Price increase disabled", "Item A");
recoverySummary.ResolveLookup(runKey);
Check(recoverySummary.SkipReasons["Lookup failed"] == 1 && recoverySummary.SkippedItems.Count == 2,
    "later success clears matching lookup failures and preserves other quality and policy skips");
recoverySummary.ResolveLookup(hqKey);
Check(!recoverySummary.SkipReasons.ContainsKey("Lookup failed") && recoverySummary.SkippedItems.Count == 1,
    "last recovered lookup removes its grouped reason");
// Multi-character run: order of steps, against a scripted host.
{
    MultiCharacter Pick(ulong id, bool included = true) => new() { ContentId = id, Name = $"C{id}", World = "Zodiark", Included = included };
    DateTimeOffset at = DateTimeOffset.UnixEpoch;
    void Step(MultiCharacterRun run, int seconds = 6) { at = at.AddSeconds(seconds); run.Update(at); }

    var host = new FakeHost { ContentId = 2 };
    var run = new MultiCharacterRun(host);
    run.Start([Pick(1), Pick(2), Pick(3, false)], at);
    for (var i = 0; i < 40 && run.IsRunning; i++) Step(run);
    Check(!run.IsRunning && string.Join(",", host.Sequences) == "2,1", "current character runs first, unticked one is left out");
    Check(host.Switches == 1 && run.Status.Contains("2 of 2"), "only one relog is needed and both complete");

    host = new FakeHost { ContentId = 1, NoBell = 1 };
    run = new MultiCharacterRun(host);
    run.Start([Pick(1), Pick(2)], at);
    for (var i = 0; i < 40 && run.IsRunning; i++) Step(run);
    Check(string.Join(",", host.Sequences) == "2" && run.Status.Contains("no summoning bell"), "a character without a bell is skipped");

    host = new FakeHost { ContentId = 1, NoBell = 1, TravelEnabled = true };
    run = new MultiCharacterRun(host);
    run.Start([Pick(1)], at);
    for (var i = 0; i < 40 && run.IsRunning; i++) Step(run);
    Check(host.Trips == 1 && string.Join(",", host.Sequences) == "1", "travel reaches a bell and the sequence runs");

    host = new FakeHost { ContentId = 1, NoBell = 1, TravelEnabled = true, TravelOutcome = "vnavmesh is not installed or enabled" };
    run = new MultiCharacterRun(host);
    run.Start([Pick(1), Pick(2)], at);
    for (var i = 0; i < 40 && run.IsRunning; i++) Step(run);
    Check(string.Join(",", host.Sequences) == "2" && run.Status.Contains("vnavmesh"), "failed travel skips only that character");

    host = new FakeHost { ContentId = 1, NoBell = 1, TravelEnabled = true, TravelOutcome = null };
    run = new MultiCharacterRun(host);
    run.Start([Pick(1)], at);
    for (var i = 0; i < 40 && run.IsRunning; i++) Step(run, 60);
    Check(!run.IsRunning && run.Status.Contains("took too long"), "travel that never arrives is skipped");

    host = new FakeHost { ContentId = 1, FailSequence = true };
    run = new MultiCharacterRun(host);
    run.Start([Pick(1), Pick(2)], at);
    for (var i = 0; i < 40 && run.IsRunning; i++) Step(run);
    Check(!run.IsRunning && host.Switches == 0 && run.Status.Contains("Stopped on C1"), "a failed sequence ends the run before switching");

    host = new FakeHost { ContentId = 1, LoginAs = 9 };
    run = new MultiCharacterRun(host);
    run.Start([Pick(2)], at);
    for (var i = 0; i < 40 && run.IsRunning; i++) Step(run);
    Check(!run.IsRunning && host.Sequences.Count == 0 && run.Status.Contains("different character"), "an unexpected character stops the run");

    host = new FakeHost { ContentId = 1, LoginAs = 0 };
    run = new MultiCharacterRun(host);
    run.Start([Pick(2)], at);
    for (var i = 0; i < 40 && run.IsRunning; i++) Step(run, 30);
    Check(!run.IsRunning && run.Status.Contains("without logging in"), "a login that never arrives stops the run");

    host = new FakeHost { Missing = true };
    run = new MultiCharacterRun(host);
    run.Start([Pick(1)], at);
    Check(!run.IsRunning && run.Status.Contains("Lifestream"), "missing Lifestream blocks the start");

    var config = new PluginConfig { MultiCharacterEnabled = true, MultiCharacters = [Pick(1), Pick(1), Pick(0)] };
    config.Normalize();
    Check(config.MultiCharacters.Count == 1, "invalid and duplicate characters are dropped");
    Check(!SettingsBackup.Export(config)["Settings"]!.AsObject().ContainsKey("MultiCharacters"), "characters stay out of backups and profiles");
    Check(!new PluginConfig().MultiCharacterEnabled, "multi-character runs are off by default");
}
Console.WriteLine($"PASS: {checks} profile/persistence/Shift/listing checks");

// Regression: live ordinary Silver Piece listings must undercut across stack sizes;
// only set-only offers (mapped to OnMannequin by the native reader) are excluded.
{
    var now = DateTimeOffset.UtcNow;
    var rows = new MarketListing[] {
        new(5825, false, 1, 1, 90, true),
        new(5825, false, 500, 94, 91),
        new(5825, false, 500, 7, 92),
        new(5825, false, 895, 7, 93),
    };
    var snapshot = new PriceSnapshot(5825, 402, PriceSource.Local, now, rows);
    var proposal = PriceCalculator.Calculate(snapshot, 5825, 402, false, new HashSet<ulong>(), 1, now, null);
    Check(proposal.CanApply && proposal.LowestPrice == 500 && proposal.SuggestedPrice == 499 && proposal.MatchingListings == 3,
        "ordinary live listings undercut regardless of stack size while set-only offers remain excluded");
    Console.WriteLine("Silver Piece pricing regression passed.");
}

sealed class FakeHost : RetainerPricer.IMultiCharacterHost
{
    public ulong ContentId { get; set; }
    public ulong? LoginAs;
    public ulong NoBell;
    public bool FailSequence, Missing;
    public int Switches;
    public List<ulong> Sequences = [];
    public bool CharacterReady => ContentId != 0;
    public bool LifestreamBusy => Missing ? throw new InvalidOperationException() : false;
    public int ChangeCharacter(string name, string world) { Switches++; ContentId = LoginAs ?? ulong.Parse(name[1..]); return 0; }
    public string? BlockedReason => null;
    public bool RetainerPickerOpen { get; private set; }
    public bool BellInteraction => RetainerPickerOpen;
    public bool TryUseBell() { if (ContentId == NoBell) return false; RetainerPickerOpen = true; return true; }
    public void ClosePicker() => RetainerPickerOpen = false;
    public bool TravelEnabled { get; set; }
    public string? TravelOutcome = "";
    public int Trips;
    public string? Travel(DateTimeOffset now) { Trips++; if (TravelOutcome == "") { NoBell = 0; RetainerPickerOpen = true; } return TravelOutcome; }
    public void StopTravel() { }
    public string TravelStatus => "walking";
    public void StartSequence() { Sequences.Add(ContentId); if (!FailSequence) CompletedSequences++; }
    public bool SequenceRunning => false;
    public int CompletedSequences { get; private set; }
    public string SequenceStatus => "failed";
    public void CancelSequence(string reason) { }
}

sealed class FakeDesynthesis : RetainerPricer.IDesynthesisHost
{
    public bool Fail;
    public List<string> Log = [];
    public bool HasWork => true;
    public bool PickerOpen { get; private set; } = true;
    public bool AtBell => PickerOpen;
    public void ClosePicker() { Log.Add("close"); PickerOpen = false; }
    public bool TryUseBell() { Log.Add("bell"); PickerOpen = true; return true; }
    public void Start() { Log.Add("start"); Succeeded = !Fail; }
    public bool Running => false;
    public bool Succeeded { get; private set; }
    public string Status => "failed";
    public void Stop(string reason) { }
}

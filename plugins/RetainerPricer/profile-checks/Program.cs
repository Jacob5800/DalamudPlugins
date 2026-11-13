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
Check(!BelowVendorPolicy.RequiresReview(false,0,1000,1000), "equal vendor price allowed");
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
Check(retry.TrySchedule(0x70000003, clock.AddSeconds(1)) && !retry.TrySchedule(0x70000003, clock.AddSeconds(2)), "market retries capped at two");
retry.Reset();
Check(retry.Attempts == 0 && retry.IsReady(clock), "next listing gets a fresh bounded retry allowance");
Console.WriteLine($"PASS: {checks} profile/persistence/Shift/listing checks");

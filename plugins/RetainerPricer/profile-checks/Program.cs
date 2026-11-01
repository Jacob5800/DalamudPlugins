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
Console.WriteLine($"PASS: {checks} profile/persistence/Shift checks");

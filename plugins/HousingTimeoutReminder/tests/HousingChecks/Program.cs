using System.Reflection;
using NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Configuration;
using NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Handler;

var decode = typeof(HousingManager).GetMethod("TryDecodeOwnedHouse", BindingFlags.Static | BindingFlags.NonPublic)!;
var checks = 0;
void Check(bool condition, string message) {
    checks++;
    if (!condition) throw new Exception(message);
}
ulong Pack(byte unit, int wardIndex, int room, ushort territory, ushort world = 402) =>
    unit | ((ulong)(wardIndex | room << 6) << 16) | ((ulong)territory << 32) | ((ulong)world << 48);
bool Decode(ulong raw, HousingType type, out object?[] args) {
    args = [raw, 402u, type, null, null, null, null];
    return (bool)decode.Invoke(null, args)!;
}
foreach (var territory in new ushort[] {339, 340, 341, 641, 979}) {
    Check(Decode(Pack(0, 0, 0, territory), HousingType.PrivateEstate, out var first), "First plot decode failed");
    Check((sbyte)first[4]! == 1 && (sbyte)first[5]! == 1, "First plot/ward must be one-based");
    Check(Decode(Pack(59, 31, 0, territory), HousingType.FreeCompanyEstate, out var last), "Last plot decode failed");
    Check((sbyte)last[4]! == 32 && (sbyte)last[5]! == 60, "Last plot/ward incorrect");
}
Check(!Decode(Pack(128, 0, 90, 339), HousingType.PrivateEstate, out _), "Apartment accepted as a private estate");
Check(!Decode(Pack(129, 31, 1, 979), HousingType.FreeCompanyEstate, out _), "Apartment accepted as an FC estate");
foreach (var invalid in new[] {0UL, ulong.MaxValue, Pack(60, 0, 0, 339), Pack(0, 32, 0, 339), Pack(0, 0, 1, 339), Pack(0, 0, 0, 9999), Pack(0, 0, 0, 339, 403)})
    Check(!Decode(invalid, HousingType.PrivateEstate, out _), "Invalid or foreign-world house accepted");
Check(!Decode(Pack(128, 0, 0, 339), HousingType.PrivateEstate, out _), "Apartment lobby accepted as an estate");
Check(!Decode(Pack(0, 0, 0, 339), HousingType.Unknown, out _), "Unknown estate type accepted");
Check(!new HousingPlot {District = District.Mist, Ward = 1, Plot = 61}.IsValid, "Invalid manual plot accepted");
Check(new PlayerID("Alt", "Character", 402).Equals(new PlayerID("Alt", "Character", 402)), "Character identity equality failed");
Check(!new PlayerID("Alt", "Character", 402).Equals(new PlayerID("Alt", "Character", 403)), "Characters on different worlds merged");
var visit = typeof(HousingTimer).GetMethod("CanRecordVisit", BindingFlags.Static | BindingFlags.NonPublic)!;
var owner = new PlayerID("Alt", "Character", 402);
bool CanVisit(PlayerID? active, bool? inside, bool apartment, short room, uint world = 402) => (bool)visit.Invoke(null, [active, owner, inside, apartment, room, world, 402u])!;
Check(CanVisit(new PlayerID("Alt", "Character", 402), true, false, 0), "Estate interior visit rejected");
Check(!CanVisit(owner, false, false, 0), "Standing in the yard recorded a visit");
Check(!CanVisit(owner, null, false, 0), "Unknown interior state recorded a visit");
Check(!CanVisit(owner, true, false, 1), "FC private chamber recorded an estate visit");
Check(!CanVisit(owner, true, true, 1), "Apartment recorded an estate visit");
Check(!CanVisit(new PlayerID("Main", "Character", 402), true, false, 0), "Main character updated an alt's personal timer");
Check(!CanVisit(null, true, false, 0), "Logged-out character recorded a visit");
Check(!CanVisit(owner, true, false, 0, 403), "A same-address house on another world recorded a visit");
var fcMatch = typeof(HousingTimer).GetMethod("IsSameFcEstate", BindingFlags.Static | BindingFlags.NonPublic)!;
var fcHouse = new HousingPlot {District = District.Mist, Ward = 1, Plot = 10};
bool SameFc(PlayerID? other, HousingPlot house) => (bool)fcMatch.Invoke(null, [owner, other, fcHouse, house])!;
Check(SameFc(new PlayerID("Main", "Character", 402), new HousingPlot {District = District.Mist, Ward = 1, Plot = 10}), "Same FC estate across alts did not match");
Check(!SameFc(new PlayerID("Main", "Character", 403), fcHouse), "FC estate on another world matched");
Check(!SameFc(owner, new HousingPlot {District = District.Mist, Ward = 1, Plot = 11}), "Different FC plot matched");
Check(!SameFc(owner, new HousingPlot {District = District.Mist, Ward = 2, Plot = 10}), "Different FC ward matched");
Check(!SameFc(null, fcHouse), "Unknown FC owner matched");
var accessType = typeof(HousingTimer).Assembly.GetType("NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Handler.FreeCompanyAccess")!;
var accessMethod = accessType.GetMethod("MatchRankAccess", BindingFlags.Static | BindingFlags.NonPublic)!;
bool? RankAccess(string[] names, uint[] flags, string[] fields) => (bool?)accessMethod.Invoke(null, [names, flags, fields, null]);
Check(RankAccess(["Peanut", "Commander"], [1u, 0u], ["Peanut"]) == true, "Custom low-rank name with estate permission was denied");
Check(RankAccess(["Leader", "Officer"], [1u, 0u], ["Officer"]) == false, "Officer rank without estate permission was allowed");
Check(RankAccess(["Gardener"], [2u], ["Gardener"]) == false, "Estate-renaming permission mistaken for estate access");
Check(RankAccess(["Gardener"], [3u], ["Gardener"]) == true, "Combined estate permissions lost access bit");
Check(RankAccess(["Leader"], [1u], []) is null, "Missing member rank was treated as a permission decision");
Check(RankAccess(["Leader", "Visitor"], [1u, 0u], ["Leader", "Visitor"]) is null, "Ambiguous member row assigned a rank");
Check(RankAccess([""], [1u], [""]) is null, "Unloaded rank label accepted");
Check(RankAccess(["Member"], [], ["Member"]) is null, "Missing permission table accepted");
Console.WriteLine($"Passed {checks} housing-address, visit, identity, and FC-permission checks.");
var showFc = accessType.GetMethod("ShouldShowReminder", BindingFlags.Static | BindingFlags.NonPublic)!;
bool ShowsFc(bool enabled, bool? access, bool late) => (bool)showFc.Invoke(null, [enabled, access, late])!;
Check(ShowsFc(true, true, true), "Enabled and allowed FC reminder was hidden");
Check(ShowsFc(true, null, true), "Unknown permission silently hid an FC reminder");
Check(!ShowsFc(true, false, true), "Denied FC access produced a reminder");
Check(!ShowsFc(false, true, true), "Global FC opt-out did not hide a reminder");
Check(!ShowsFc(false, null, true), "Unknown FC permission bypassed global opt-out");
Check(!ShowsFc(true, true, false), "Up-to-date FC estate produced a reminder");
Console.WriteLine($"Passed {checks} total checks, including global FC reminder settings.");

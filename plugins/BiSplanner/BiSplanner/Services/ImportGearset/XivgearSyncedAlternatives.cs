using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BiSplanner.Services.ImportGearset;

internal sealed class XivgearSyncedAlternatives
{
    private static readonly Dictionary<int, string> StatColumns = new()
    {
        [1] = "strength", [2] = "dexterity", [3] = "vitality", [4] = "intelligence",
        [5] = "mind", [6] = "piety", [19] = "tenacity", [21] = "defense",
        [22] = "directHitRate", [24] = "magicDefense", [27] = "criticalHit",
        [44] = "determination", [45] = "skillSpeed", [46] = "spellSpeed",
        [12] = "physicalDamage", [13] = "magicalDamage",
    };

    private readonly JsonElement[] items;
    private readonly Dictionary<int, JsonElement> parameters;
    private readonly JsonElement caps;
    private readonly int sync;

    private XivgearSyncedAlternatives(JsonElement[] items, Dictionary<int, JsonElement> parameters, JsonElement caps, int sync)
    {
        this.items = items;
        this.parameters = parameters;
        this.caps = caps;
        this.sync = sync;
    }

    public static async Task<XivgearSyncedAlternatives> Load(HttpClient client, string job, int sync)
    {
        async Task<JsonElement[]> Read(string path)
        {
            using var document = JsonDocument.Parse(await client.GetStringAsync("https://data.xivgear.app/" + path));
            return document.RootElement.EnumerateObject().First().Value.EnumerateArray().Select(row => row.Clone()).ToArray();
        }

        var jobs = Read("Items?job=" + Uri.EscapeDataString(job));
        var parameters = Read("BaseParams");
        var levels = Read("ItemLevel");
        await Task.WhenAll(jobs, parameters, levels);
        return new(await jobs, (await parameters).ToDictionary(row => Number(row, "rowId")),
            (await levels).First(row => Number(row, "rowId") == sync), sync);
    }

    public uint[] Find(uint id, string slot, int level, HashSet<uint> plannedRings)
    {
        var baseItem = items.FirstOrDefault(item => Number(item, "rowId") == id);
        if (baseItem.ValueKind == JsonValueKind.Undefined || Number(baseItem, "ilvl") <= sync)
            return [];

        var slotColumn = slot switch
        {
            "Head" => "headPercent", "Body" => "chestPercent", "Hand" => "handsPercent",
            "Legs" => "legsPercent", "Feet" => "feetPercent", "Ears" => "earringPercent",
            "Neck" => "necklacePercent", "Wrist" => "braceletPercent",
            "RingLeft" or "RingRight" => "ringPercent", "OffHand" => "offHandPercent",
            "Weapon" => Number(baseItem.GetProperty("equipSlotCategory"), "offHand") < 0
                ? "twoHandWeaponPercent" : "oneHandWeaponPercent",
            _ => null,
        };
        if (slotColumn is null || !TryStats(baseItem, false, slotColumn, out var required))
            return [];

        var category = Number(baseItem.GetProperty("equipSlotCategory"), "rowId");
        var results = new List<uint>();
        foreach (var item in items)
        {
            var candidateId = (uint)Number(item, "rowId");
            if (candidateId == id || Number(item, "equipLevel") > 100 ||
                Number(item.GetProperty("equipSlotCategory"), "rowId") != category ||
                (Number(item, "ilvl") <= sync && Number(item, "equipLevel") > level) ||
                (slotColumn == "ringPercent" && Flag(item, "unique") && plannedRings.Contains(candidateId)))
                continue;

            if (TryStats(item, false, slotColumn, out var hq) && Matches(hq, required))
                results.Add(candidateId + (Flag(item, "canBeHq") ? 1000000u : 0u));
            if (Flag(item, "canBeHq") && TryStats(item, true, slotColumn, out var nq) && Matches(nq, required))
                results.Add(candidateId);
        }
        return results.Distinct().Order().ToArray();
    }

    private bool TryStats(JsonElement item, bool nq, string slot, out Dictionary<int, int> stats)
    {
        stats = new();
        if ((item.TryGetProperty("specialStatType", out var special) && special.ValueKind == JsonValueKind.String)
            || !item.TryGetProperty(nq ? "baseParamMap" : "baseParamMapHQ", out var raw)
            || raw.ValueKind != JsonValueKind.Object)
            return false;
        foreach (var stat in raw.EnumerateObject())
        {
            if (!int.TryParse(stat.Name, out var id) || !StatColumns.ContainsKey(id))
                return false;
            stats[id] = stat.Value.GetInt32();
        }

        stats[12] = Number(item, nq ? "damagePhys" : "damagePhysHQ");
        stats[13] = Number(item, nq ? "damageMag" : "damageMagHQ");
        stats[21] = Number(item, nq ? "defensePhys" : "defensePhysHQ");
        stats[24] = Number(item, nq ? "defenseMag" : "defenseMagHQ");
        stats[14] = Number(item, "delayMs");
        if (Number(item, "ilvl") <= sync)
            return true;

        foreach (var id in stats.Keys.ToArray())
        {
            if (!StatColumns.TryGetValue(id, out var column) || !parameters.TryGetValue(id, out var param))
                continue;
            var modifier = Number(caps, column);
            if (modifier <= 0)
                continue;
            var index = Number(item, "meldParamIndex", -1);
            var melds = param.GetProperty("meldParam");
            var meld = index >= 0 && index < melds.GetArrayLength() ? melds[index].GetInt32() : 100;
            var cap = (modifier * meld / 100 * Number(param, slot) / 100 + 5) / 10;
            stats[id] = Math.Min(stats[id], cap);
        }
        return true;
    }

    private static bool Matches(Dictionary<int, int> candidate, Dictionary<int, int> required) =>
        required.All(stat => stat.Key is 45 or 46 or 14
            ? candidate.GetValueOrDefault(stat.Key) == stat.Value
            : candidate.GetValueOrDefault(stat.Key) >= stat.Value)
        && candidate.GetValueOrDefault(45) == required.GetValueOrDefault(45)
        && candidate.GetValueOrDefault(46) == required.GetValueOrDefault(46);

    private static int Number(JsonElement row, string key, int fallback = 0) =>
        row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : fallback;

    private static bool Flag(JsonElement row, string key) =>
        row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
}

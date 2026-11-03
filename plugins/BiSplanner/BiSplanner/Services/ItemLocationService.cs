using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace BiSplanner.Services;

// Snapshots are character-scoped. Inaccessible remote containers are never treated as live data.
public sealed class ItemLocationService(IPlayerState player, IGameGui gameGui, IFramework framework,
    IDalamudPluginInterface pluginInterface, ITypedLogger<ItemLocationService> logger) : IHostedService, IDisposable
{
    private bool stopped = true;
    public Task StartAsync(CancellationToken token) { stopped = false; framework.Update += OnUpdate; return Task.CompletedTask; }
    public Task StopAsync(CancellationToken token) { Dispose(); return Task.CompletedTask; }
    public void Dispose() { if (stopped) return; stopped = true; framework.Update -= OnUpdate; }
    private void OnUpdate(IFramework _) { if (!stopped) Refresh(); }
    public sealed record Location(uint ItemId, int Quantity, string Container, int Slot);
    public sealed record Snapshot(DateTime UpdatedUtc, List<Location> Items);
    private Dictionary<string, Snapshot> remote = [];
    private List<Location> carried = [];
    private HashSet<string> visibleSnapshots = [];
    private ulong character;
    private DateTime nextScan;
    private string CachePath => Path.Combine(pluginInterface.GetPluginConfigDirectory(), $"item-locations-{character:X16}.json");

    public IReadOnlyList<string> GetLocations(uint itemId)
    {
        if (!stopped) Refresh();
        var result = carried.Where(i => i.ItemId == itemId)
            .Select(i => $"{i.Container}, storage slot {i.Slot} (x{i.Quantity})").ToList();
        foreach (var snapshot in remote.Values)
            result.AddRange(snapshot.Items.Where(i => i.ItemId == itemId)
                .Select(i => $"{i.Container}, storage slot {i.Slot} (x{i.Quantity}; last seen {snapshot.UpdatedUtc.ToLocalTime():g})"));
        return result;
    }

    private unsafe void Refresh()
    {
        if (!player.IsLoaded || player.ContentId == 0) { carried = []; remote = []; character = 0; visibleSnapshots.Clear(); return; }
        if (character != player.ContentId)
        {
            character = player.ContentId; carried = []; remote = []; visibleSnapshots.Clear(); nextScan = default;
            try { if (File.Exists(CachePath)) remote = JsonSerializer.Deserialize<Dictionary<string, Snapshot>>(File.ReadAllText(CachePath)) ?? []; }
            catch (Exception ex) { logger.Error(ex, "Could not load item location snapshots"); }
        }
        if (DateTime.UtcNow < nextScan) return;
        nextScan = DateTime.UtcNow.AddSeconds(2);
        var manager = InventoryManager.Instance();
        if (manager == null) return;
        List<Location>? Read(IEnumerable<InventoryType> types, string? prefix = null, bool requireAll = true)
        {
            var found = new List<Location>();
            foreach (var type in types)
            {
                var bag = manager->GetInventoryContainer(type);
                if (bag == null || !bag->IsLoaded)
                {
                    if (requireAll) return null;
                    continue;
                }
                for (var slot = 0; slot < bag->Size; slot++)
                {
                    var item = bag->GetInventorySlot(slot);
                    if (item == null || item->ItemId == 0) continue;
                    var rawId = item->ItemId + ((item->Flags & InventoryItem.ItemFlags.HighQuality) != 0 ? 1_000_000u : 0);
                    var label = prefix == null
                        ? type == InventoryType.EquippedItems ? "Equipped"
                            : (uint)type <= 3 ? $"Inventory bag {(uint)type + 1}"
                            : type.ToString().Replace("Armory", "Armoury chest: ")
                        : prefix + " / " + type;
                    found.Add(new Location(rawId, (int)item->Quantity, label, slot + 1));
                }
            }
            return found;
        }
        var all = Enum.GetValues<InventoryType>();
        var liveTypes = all.Where(t => (uint)t <= 3 || t == InventoryType.EquippedItems || t.ToString().StartsWith("Armory"));
        carried = Read(liveTypes, requireAll: false) ?? [];
        var changed = false;
        var visibleNow = new HashSet<string>();
        void Save(string key, List<Location>? items)
        {
            if (items == null) return;
            visibleNow.Add(key);
            if (visibleSnapshots.Contains(key) && remote.TryGetValue(key, out var old) && old.Items.SequenceEqual(items)) return;
            remote[key] = new Snapshot(DateTime.UtcNow, items); changed = true;
        }
        // Only refresh saddlebag caches while their UI is open; otherwise memory can be stale.
        if (gameGui.GetAddonByName("InventoryBuddy").IsVisible)
        {
            Save("saddlebag", Read(all.Where(t => t.ToString().StartsWith("SaddleBag")), "Saddlebag"));
            Save("premium-saddlebag", Read(all.Where(t => t.ToString().StartsWith("PremiumSaddleBag")), "Premium saddlebag"));
        }
        if (gameGui.GetAddonByName("InventoryRetainer").IsVisible || gameGui.GetAddonByName("InventoryRetainerLarge").IsVisible)
        {
            var retainers = RetainerManager.Instance();
            var order = ItemOrderModule.Instance();
            var active = retainers == null || !retainers->IsReady ? null : retainers->GetActiveRetainer();
            if (active != null && active->RetainerId != 0 && order != null && order->ActiveRetainerId == active->RetainerId)
                Save($"retainer-{active->RetainerId:X16}", Read(all.Where(t => t.ToString().StartsWith("RetainerPage")), $"Retainer {active->NameString}"));
        }
        visibleSnapshots = visibleNow;
        if (changed)
            try { Directory.CreateDirectory(pluginInterface.GetPluginConfigDirectory()); File.WriteAllText(CachePath, JsonSerializer.Serialize(remote)); }
            catch (Exception ex) { logger.Error(ex, "Could not save item location snapshots"); }
    }
}

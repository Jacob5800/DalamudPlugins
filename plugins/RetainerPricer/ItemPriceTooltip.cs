using Dalamud.Game.Text.SeStringHandling;
using Lumina.Excel.Sheets;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace RetainerPricer;

internal sealed record TooltipQuality(decimal? Price, DateTimeOffset? Uploaded, decimal? LastPrice, DateTimeOffset? Sold, decimal? SalesPerDay);
internal sealed record TooltipMarketData(TooltipQuality Nq, TooltipQuality Hq);

internal sealed class ItemPriceTooltip : IDisposable
{
    private const uint PriceNodeId = 580001;
    private readonly IGameGui gameGui;
    private readonly NativeMarketBridge bridge;
    private readonly UniversalisClient client;
    private readonly PluginConfig config;
    private readonly IAddonLifecycle lifecycle;
    private readonly HashSet<uint> marketable;
    private readonly HashSet<uint> hqCapable;
    private readonly Dictionary<uint, DateTimeOffset> lastAttempt = [];
    private readonly CancellationTokenSource lifetime = new();
    private readonly Dictionary<uint, (TooltipMarketData Data, DateTimeOffset At)> cache = [];
    private readonly HashSet<uint> queued = [];
    private readonly Dictionary<uint, DateTimeOffset> retryAt = [];
    private Task<IReadOnlyDictionary<uint, TooltipMarketData>>? request;
    private uint[] activeIds = [];
    private uint activeWorld;
    private uint worldId;
    private DateTimeOffset nextBatch;
    private DateTimeOffset nextPrefetch;
    private string? rendered;
    private int addedHeight;
    private nint attachedAddon;
    private int shiftedY;
    private readonly ushort orangeColor;
    private readonly ushort grayColor;
    private readonly ushort yellowColor;
    private readonly ushort redColor;

    public ItemPriceTooltip(IGameGui gameGui, NativeMarketBridge bridge, UniversalisClient client, PluginConfig config, IAddonLifecycle lifecycle, HashSet<uint> marketable, IDataManager data)
    {
        (this.gameGui, this.bridge, this.client, this.config, this.lifecycle, this.marketable) = (gameGui, bridge, client, config, lifecycle, marketable);
        hqCapable = data.GetExcelSheet<Item>().Where(row => row.CanBeHq).Select(row => row.RowId).ToHashSet();
        ushort Closest(int red, int green, int blue) => (ushort)data.GetExcelSheet<UIColor>()
            .Where(row => row.RowId > 0 && row.RowId <= ushort.MaxValue && (row.Dark & 255) != 0)
            .MinBy(row => Math.Pow((row.Dark >> 24) - red, 2) + Math.Pow(((row.Dark >> 16) & 255) - green, 2) + Math.Pow(((row.Dark >> 8) & 255) - blue, 2)).RowId;
        orangeColor = Closest(255, 165, 45);
        grayColor = Closest(175, 175, 175);
        yellowColor = Closest(255, 220, 50);
        redColor = Closest(255, 65, 65);
        lifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "ItemDetail", BeforeUpdate);
        lifecycle.RegisterListener(AddonEvent.PreFinalize, "ItemDetail", BeforeFinalize);
    }

    private unsafe void BeforeUpdate(AddonEvent _, AddonArgs args) => Restore((AtkUnitBase*)args.Addon.Address);
    private unsafe void BeforeFinalize(AddonEvent _, AddonArgs args) { Restore((AtkUnitBase*)args.Addon.Address); RemoveNode((AtkUnitBase*)args.Addon.Address); }
    private bool Fresh(uint id) => cache.TryGetValue(id, out var entry) && DateTimeOffset.UtcNow - entry.At < TimeSpan.FromMinutes(90);
    private void Queue(uint id, bool refreshMissing = false)
    {
        var missing = false;
        if (refreshMissing && cache.TryGetValue(id, out var entry))
        {
            bool Incomplete(TooltipQuality quality) => quality.Price is null or <= 0 || quality.Uploaded is null ||
                quality.LastPrice is null or <= 0 || quality.Sold is null || quality.SalesPerDay is null;
            missing = Incomplete(entry.Data.Nq) || (hqCapable.Contains(id) && Incomplete(entry.Data.Hq));
            if (lastAttempt.TryGetValue(id, out var attempted) && DateTimeOffset.UtcNow - attempted < TimeSpan.FromMinutes(1)) missing = false;
        }
        if (marketable.Contains(id) && (!Fresh(id) || missing) && !activeIds.Contains(id) &&
            (!retryAt.TryGetValue(id, out var at) || DateTimeOffset.UtcNow >= at)) queued.Add(id);
    }
    private unsafe void Prefetch()
    {
        var manager = InventoryManager.Instance();
        if (manager == null) return;
        foreach (var type in new[] { InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4, InventoryType.SaddleBag1, InventoryType.SaddleBag2, InventoryType.PremiumSaddleBag1, InventoryType.PremiumSaddleBag2, InventoryType.RetainerPage1, InventoryType.RetainerPage2, InventoryType.RetainerPage3, InventoryType.RetainerPage4, InventoryType.RetainerPage5, InventoryType.RetainerPage6, InventoryType.RetainerPage7 })
        {
            var container = manager->GetInventoryContainer(type);
            if (container == null || !container->IsLoaded) continue;
            for (var i = 0; i < container->Size; i++) Queue(container->Items[i].GetBaseItemId());
        }
    }
    public unsafe void Draw()
    {
        var addon = (AtkUnitBase*)gameGui.GetAddonByName("ItemDetail").Address;
        if (request is { IsCompleted: true })
        {
            var now = DateTimeOffset.UtcNow;
            if (request.IsCompletedSuccessfully && activeWorld == worldId)
                foreach (var row in request.Result) cache[row.Key] = (row.Value, now);
            else _ = request.Exception;
            if (activeWorld == worldId)
                foreach (var id in activeIds) if (!Fresh(id)) retryAt[id] = now.AddSeconds(30);
            request = null;
            activeIds = [];
        }
        if (!config.ShowItemTooltipPrices) { Restore(addon); return; }
        var world = bridge.GetHomeWorld();
        if (world is null) { Restore(addon); return; }
        if (worldId != world.WorldId) { worldId = world.WorldId; cache.Clear(); queued.Clear(); retryAt.Clear(); lastAttempt.Clear(); nextPrefetch = DateTimeOffset.MinValue; }
        var nowAt = DateTimeOffset.UtcNow;
        if (nowAt >= nextPrefetch) { Prefetch(); nextPrefetch = nowAt.AddSeconds(5); }
        var raw = gameGui.HoveredItem;
        var hovered = raw is > 0 and < 500_000 || raw is >= 1_000_000 and < 1_500_000 ? (uint)(raw % 500_000) : 0;
        if (hovered != 0) Queue(hovered, refreshMissing: true);
        if (request is null && queued.Count > 0 && nowAt >= nextBatch)
        {
            activeIds = queued.OrderByDescending(id => id == hovered).Take(50).ToArray();
            foreach (var id in activeIds) { queued.Remove(id); lastAttempt[id] = nowAt; }
            activeWorld = worldId;
            request = client.FetchTooltipBatchAsync(worldId, activeIds, lifetime.Token);
            nextBatch = nowAt.AddMilliseconds(200);
        }
        if (addon == null || !addon->IsVisible || !marketable.Contains(hovered)) { Restore(addon); return; }
        var text = new SeStringBuilder();
        void Amount(decimal? value, bool gil)
        {
            if (value is null || (gil && value <= 0)) { text.AddText("—"); return; }
            text.AddUiForeground(orangeColor).AddText(value.Value.ToString(gil ? "N0" : "0.##") + (gil ? "\uE049" : "")).AddUiForegroundOff();
        }
        void Age(DateTimeOffset? at)
        {
            var age = at is null ? TimeSpan.Zero : nowAt - at.Value;
            var days = Math.Max(0, (int)age.TotalDays);
            var color = at is null || days < 7 ? grayColor : days > 30 ? redColor : days >= 8 ? orangeColor : yellowColor;
            var label = at is null ? "age unknown" : days >= 1 ? $"{days}d ago" : age.TotalHours >= 1 ? $"{(int)age.TotalHours}h ago" : $"{Math.Max(0, (int)age.TotalMinutes)}m ago";
            text.AddUiForeground(color).AddText($" ({label})").AddUiForegroundOff();
        }
        void Price(decimal? value, DateTimeOffset? at) { Amount(value, true); if (value is > 0) Age(at); }
        text.AddText($"Home prices ({world.Name}):\n");
        if (cache.TryGetValue(hovered, out var result))
        {
            var (nq, hq) = (result.Data.Nq, result.Data.Hq);
            text.AddText("NQ "); Price(nq.Price, nq.Uploaded); text.AddText(" / HQ "); Price(hq.Price, hq.Uploaded);
            text.AddText($"\nMost recent purchase · Home ({world.Name}):\nNQ ");
            Price(nq.LastPrice, nq.Sold); text.AddText(" / HQ "); Price(hq.LastPrice, hq.Sold);
            text.AddText("\nSales/day (4-day cache): NQ "); Amount(nq.SalesPerDay, false);
            text.AddText(" / HQ "); Amount(hq.SalesPerDay, false);
        }
        else text.AddText(retryAt.ContainsKey(hovered) ? "Unavailable; retrying shortly." : "Loading home prices...");
        Attach(addon, text.Build().Encode());
        if (cache.Count > 2000) foreach (var id in cache.OrderBy(x => x.Value.At).Take(cache.Count - 2000).Select(x => x.Key).ToArray()) cache.Remove(id);
    }

    private static unsafe AtkTextNode* Find(AtkUnitBase* addon)
    {
        if (addon == null) return null;
        for (var i = 0; i < addon->UldManager.NodeListCount; i++)
            if (addon->UldManager.NodeList[i] != null && addon->UldManager.NodeList[i]->NodeId == PriceNodeId) return (AtkTextNode*)addon->UldManager.NodeList[i];
        return null;
    }
    // Native tooltip insertion follows Price Insight's MIT-licensed node layout approach.
    // Attribution and license: third-party/PriceInsight-LICENSE.txt.
    private unsafe void Attach(AtkUnitBase* addon, byte[] text)
    {
        var encoded = Convert.ToBase64String(text);
        if (attachedAddon == (nint)addon && rendered == encoded && addedHeight > 0) return;
        Restore(addon);
        var anchor = addon->GetNodeById(2);
        var style = addon->GetTextNodeById(44);
        if (anchor == null || style == null || addon->WindowNode == null || addon->WindowNode->Component == null) return;
        var node = Find(addon);
        if (node == null)
        {
            node = IMemorySpace.GetUISpace()->Create<AtkTextNode>();
            node->NodeId = PriceNodeId;
            node->Type = NodeType.Text;
            node->NodeFlags = NodeFlags.AnchorLeft | NodeFlags.AnchorTop;
            node->Color = style->Color;
            node->TextColor = style->TextColor;
            node->EdgeColor = style->EdgeColor;
            node->FontSize = 12;
            node->LineSpacing = 18;
            node->TextFlags = style->TextFlags | TextFlags.MultiLine | TextFlags.AutoAdjustNodeSize;
            node->SetXFloat(16);
            node->ParentNode = anchor->ParentNode;
            node->PrevSiblingNode = anchor->PrevSiblingNode;
            node->NextSiblingNode = anchor;
            if (anchor->PrevSiblingNode != null) anchor->PrevSiblingNode->NextSiblingNode = (AtkResNode*)node;
            anchor->PrevSiblingNode = (AtkResNode*)node;
            addon->UldManager.UpdateDrawNodeList();
        }
        node->SetText(text);
        node->ResizeNodeForCurrentText();
        node->SetYFloat(addon->WindowNode->Height - 8);
        node->ToggleVisibility(true);
        addedHeight = node->Height + 4;
        Resize(addon, addedHeight);
        anchor->SetYFloat(anchor->Y + addedHeight);
        var viewport = Dalamud.Bindings.ImGui.ImGui.GetMainViewport();
        var overflow = addon->Y + addon->GetScaledHeight(true) + 12 - (viewport.WorkPos.Y + viewport.WorkSize.Y);
        if (overflow > 0)
        {
            var targetY = (short)Math.Max(viewport.WorkPos.Y, addon->Y - overflow);
            shiftedY = addon->Y - targetY;
            addon->SetPosition(addon->X, targetY);
        }
        rendered = encoded;
        attachedAddon = (nint)addon;
    }
    private static unsafe void Resize(AtkUnitBase* addon, int delta)
    {
        if (addon->WindowNode == null) return;
        var height = (ushort)Math.Clamp(addon->WindowNode->Height + delta, 1, ushort.MaxValue);
        addon->WindowNode->SetHeight(height);
        addon->RootNode->SetHeight(height);
        var root = addon->WindowNode->Component->UldManager.RootNode;
        if (root != null) { root->SetHeight(height); if (root->PrevSiblingNode != null) root->PrevSiblingNode->SetHeight(height); }
    }
    private unsafe void Restore(AtkUnitBase* addon)
    {
        if (addon == null) { rendered = null; addedHeight = 0; attachedAddon = 0; return; }
        var node = Find(addon);
        if (node == null || addedHeight == 0 || attachedAddon != (nint)addon) return;
        node->ToggleVisibility(false);
        Resize(addon, -addedHeight);
        var anchor = addon->GetNodeById(2);
        if (anchor != null) anchor->SetYFloat(anchor->Y - addedHeight);
        if (shiftedY != 0) { addon->SetPosition(addon->X, (short)(addon->Y + shiftedY)); shiftedY = 0; }
        addedHeight = 0; rendered = null;
    }
    private static unsafe void RemoveNode(AtkUnitBase* addon)
    {
        var node = Find(addon);
        if (node == null) return;
        if (node->ParentNode != null && node->ParentNode->ChildNode == (AtkResNode*)node) node->ParentNode->ChildNode = node->PrevSiblingNode;
        if (node->PrevSiblingNode != null) node->PrevSiblingNode->NextSiblingNode = node->NextSiblingNode;
        if (node->NextSiblingNode != null) node->NextSiblingNode->PrevSiblingNode = node->PrevSiblingNode;
        addon->UldManager.UpdateDrawNodeList();
        node->Destroy(true);
    }
    public unsafe void Dispose()
    {
        lifecycle.UnregisterListener(BeforeUpdate);
        lifecycle.UnregisterListener(BeforeFinalize);
        var addon = (AtkUnitBase*)gameGui.GetAddonByName("ItemDetail").Address;
        Restore(addon); RemoveNode(addon);
        lifetime.Cancel(); lifetime.Dispose();
    }
}

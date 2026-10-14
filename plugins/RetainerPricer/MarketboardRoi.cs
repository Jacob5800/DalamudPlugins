using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using System.Numerics;

namespace RetainerPricer;

internal sealed class MarketboardRoi(IGameGui gameGui, NativeMarketBridge bridge, UniversalisClient client, PluginConfig config) : IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private Task<PriceSnapshot>? request;
    private (uint World, uint Item) requested;
    private PriceSnapshot? snapshot;
    private DateTimeOffset nextRequest;
    private string status = "Loading home prices...";
    public string Diagnostics { get; private set; } = "Open marketboard search results to inspect ROI.";

    public unsafe void Draw()
    {
        if (request is { IsCompleted: true })
        {
            if (request.IsCompletedSuccessfully) { snapshot = request.Result; status = ""; }
            else { _ = request.Exception; snapshot = null; status = "Home price unavailable"; }
            request = null;
        }
        if (!config.ShowMarketboardRoi) { Diagnostics = "Marketboard ROI disabled."; return; }
        var world = bridge.GetHomeWorld();
        var addon = (AddonItemSearchResult*)gameGui.GetAddonByName("ItemSearchResult").Address;
        var agent = AgentItemSearch.Instance();
        var proxy = InfoProxyItemSearch.Instance();
        if (world is null || addon == null || !addon->IsVisible || addon->Results == null || agent == null || proxy == null)
        { Diagnostics = $"ROI: home={world is not null}; result window={addon != null && addon->IsVisible}; list={addon != null && addon->Results != null}; agent={agent != null}; proxy={proxy != null}; loading={proxy != null && proxy->WaitingForListings}"; return; }
        // WaitingForListings can remain true after the visible marketboard response is complete.
        // Validate the rendered list and each matching offer instead of treating it as a blocker.
        var raw = agent->ResultItemId;
        var id = raw >= 1_000_000 ? raw - 1_000_000 : raw;
        if (id == 0 || proxy->SearchItemId != id) { Diagnostics = $"ROI: result item={id}; proxy item={proxy->SearchItemId}; waiting for matching result data."; return; }
        var key = (world.WorldId, id);
        var now = DateTimeOffset.UtcNow;
        if (request is null && now >= nextRequest && (requested != key || snapshot is null || now - snapshot.RetrievedAt > TimeSpan.FromMinutes(5)))
        {
            requested = key;
            snapshot = null;
            status = "Loading home prices...";
            request = client.FetchAsync(world.WorldId, id, lifetime.Token, cacheMinutes: 5);
            nextRequest = now.AddSeconds(5);
        }
        var matching = requested == key && snapshot?.ItemId == id && snapshot.WorldId == world.WorldId;
        var list = addon->Results;
        var rowCount = Math.Min(Math.Min(list->ListLength, proxy->ListingCount), 100);
        var visible = 0;
        var shown = 0;
        for (var index = 0; index < rowCount; index++)
        {
            if (!list->IsItemVisible(index)) continue;
            var renderer = list->GetItemRenderer(index);
            if (renderer == null || renderer->OwnerNode == null) continue;
            visible++;
            var offer = proxy->Listings[index];
            if (offer.ItemId != id || offer.UnitPrice == 0 || offer.IsMannequin) continue;
            var node = renderer->OwnerNode;
            var top = node->ScreenY;
            if (top < addon->Y || top >= addon->Y + addon->GetScaledHeight(true) - 25 * addon->Scale) continue;
            var home = matching ? snapshot!.Listings.Where(x => x.WorldId == world.WorldId && x.IsHq == offer.IsHqItem && !x.OnMannequin && x.PricePerUnit > 0).OrderBy(x => x.PricePerUnit).FirstOrDefault() : null;
            var label = "ROI: —";
            var color = new Vector4(0.65f, 0.65f, 0.65f, 1);
            decimal? roi = null;
            if (home is not null)
            {
                var cost = offer.UnitPrice * (1m + (decimal)config.SniperPurchaseFeePercent / 100m);
                var resale = Math.Max(1m, home.PricePerUnit - 1m);
                roi = (resale * (1m - (decimal)config.SniperResaleTaxPercent / 100m) - cost) / cost * 100m;
                label = $"ROI: {roi:0.0}%";
                color = roi < 0 ? new Vector4(1, 0.35f, 0.35f, 1) : roi >= 100 ? new Vector4(0.2f, 1, 0.3f, 1) : roi >= 25 ? new Vector4(0.6f, 0.9f, 0.6f, 1) : Vector4.One;
            }
            // Use each live renderer's screen position so virtualized rows follow scrolling.
            var labelWidth = Math.Max(ImGui.CalcTextSize(label).X, ImGui.CalcTextSize("ROI: -100.0%").X) + 8;
            var roiX = addon->X + addon->GetScaledWidth(true) - 28 * addon->Scale - labelWidth;
            ImGui.SetNextWindowPos(new Vector2(roiX, top));
            ImGui.SetNextWindowBgAlpha(0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(4, 1));
            var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBackground;
            if (ImGui.Begin($"ROI##RPMarketRow{index}", flags))
            {
                ImGui.TextColored(color, label);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(home is null ? (matching ? "No matching HQ/NQ home listing." : status) : $"Estimated resale on {world.Name}: {Math.Max(1m, home.PricePerUnit - 1m):N0} gil each. Same HQ/NQ, cheapest home listing minus 1 gil; {config.SniperPurchaseFeePercent:0.##}% purchase fee and {config.SniperResaleTaxPercent:0.##}% resale tax. Materia is not matched. This is an estimate, not guaranteed profit.");
            }
            ImGui.End();
            ImGui.PopStyleVar();
            shown++;
        }
        Diagnostics = $"ROI: item={id}; list rows={list->ListLength}; listings={proxy->ListingCount}; visible={visible}; labels={shown}; native loading={proxy->WaitingForListings}; home prices={(matching ? "ready" : status)}";
    }
    public void Dispose() { lifetime.Cancel(); lifetime.Dispose(); }
}

using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Numerics;

namespace RetainerPricer;

internal sealed class ItemPriceTooltip(IGameGui gameGui, NativeMarketBridge bridge, UniversalisClient client, PluginConfig config) : IDisposable
{
    private readonly CancellationTokenSource lifetime = new();
    private readonly Dictionary<(uint, uint), (PriceSnapshot Snapshot, decimal? Nq, decimal? Hq, DateTimeOffset At)> cache = [];
    private Task<(PriceSnapshot Snapshot, decimal? Nq, decimal? Hq)>? request;
    private (uint World, uint Item) requestKey;
    private (uint World, uint Item) hovered;
    private DateTimeOffset hoverSince;
    private DateTimeOffset nextRequest;
    private string? error;

    private async Task<(PriceSnapshot, decimal?, decimal?)> Fetch(uint world, uint item)
    {
        var snapshot = await client.FetchAsync(world, item, lifetime.Token, cacheMinutes: 5).ConfigureAwait(false);
        try
        {
            var velocity = await client.FetchTooltipVelocityAsync(world, item, lifetime.Token).ConfigureAwait(false);
            return (snapshot, velocity.Nq, velocity.Hq);
        }
        catch (Exception) when (!lifetime.IsCancellationRequested) { return (snapshot, null, null); }
    }

    public unsafe void Draw()
    {
        if (request is { IsCompleted: true })
        {
            if (request.IsCompletedSuccessfully)
            {
                if (cache.Count >= 100) cache.Remove(cache.MinBy(x => x.Value.At).Key);
                var result = request.Result;
                cache[requestKey] = (result.Snapshot, result.Nq, result.Hq, DateTimeOffset.UtcNow);
            }
            else { _ = request.Exception; if (requestKey == hovered) error = "Prices unavailable; retrying shortly."; }
            request = null;
        }
        if (!config.ShowItemTooltipPrices) return;
        var world = bridge.GetHomeWorld();
        var raw = gameGui.HoveredItem;
        if (world is null || raw == 0 || raw >= 2_000_000) return;
        var id = (uint)(raw >= 1_000_000 ? raw - 1_000_000 : raw);
        var addon = (AtkUnitBase*)gameGui.GetAddonByName("ItemDetail").Address;
        if (addon == null || !addon->IsVisible) return;
        var key = (world.WorldId, id);
        var now = DateTimeOffset.UtcNow;
        if (hovered != key) { hovered = key; hoverSince = now; error = null; }
        var available = cache.TryGetValue(key, out var cached);
        if ((!available || now - cached.At > TimeSpan.FromMinutes(5)) && request is null && now >= nextRequest && now - hoverSince >= TimeSpan.FromMilliseconds(500))
        {
            requestKey = key;
            request = Fetch(world.WorldId, id);
            nextRequest = now.AddSeconds(5);
        }
        var viewport = ImGui.GetMainViewport();
        var pos = new Vector2(addon->X, addon->Y + addon->GetScaledHeight(true));
        pos.X = Math.Clamp(pos.X, viewport.WorkPos.X, Math.Max(viewport.WorkPos.X, viewport.WorkPos.X + viewport.WorkSize.X - 420));
        if (pos.Y + 155 > viewport.WorkPos.Y + viewport.WorkSize.Y) pos.Y = Math.Max(viewport.WorkPos.Y, addon->Y - 155);
        ImGui.SetNextWindowPos(pos);
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoFocusOnAppearing;
        if (ImGui.Begin("Home prices##RetainerPricerTooltip", flags))
        {
            ImGui.TextUnformatted($"Home prices ({world.Name}):");
            if (!available) ImGui.TextDisabled(error ?? "Loading home prices...");
            else
            {
                string Age(DateTimeOffset at) { var age = now - at; return age.TotalDays >= 1 ? $"{(int)age.TotalDays}d ago" : age.TotalHours >= 1 ? $"{(int)age.TotalHours}h ago" : $"{Math.Max(0, (int)age.TotalMinutes)}m ago"; }
                string Listing(bool hq)
                {
                    var row = cached.Snapshot.Listings.Where(x => x.IsHq == hq && !x.OnMannequin && x.WorldId == world.WorldId).OrderBy(x => x.PricePerUnit).FirstOrDefault();
                    return row is null ? "—" : $"{row.PricePerUnit:N0} ({Age(row.ReviewedAt ?? cached.Snapshot.ObservedAt)})";
                }
                string Sale(bool hq)
                {
                    var row = (cached.Snapshot.Sales ?? []).Where(x => x.IsHq == hq && x.WorldId == world.WorldId).OrderByDescending(x => x.SoldAt).FirstOrDefault();
                    return row is null ? "—" : $"{row.PricePerUnit:N0} ({Age(row.SoldAt)})";
                }
                ImGui.TextUnformatted($"NQ {Listing(false)} / HQ {Listing(true)}");
                ImGui.TextUnformatted($"Most recent purchase · Home ({world.Name}):");
                ImGui.TextUnformatted($"NQ {Sale(false)} / HQ {Sale(true)}");
                ImGui.TextUnformatted($"Sales/day (4-day cache): NQ {cached.Nq?.ToString("0.##") ?? "—"} / HQ {cached.Hq?.ToString("0.##") ?? "—"}");
            }
        }
        ImGui.End();
    }

    public void Dispose() { lifetime.Cancel(); lifetime.Dispose(); }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Workshoppa.Voyages;

// Uses the same cached aggregate reference as RetainerPricer's initial Sniper scan.
internal sealed class VoyageMarketCache : IDisposable
{
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly CancellationTokenSource lifetime = new();
    private readonly string directory;
    private Task<Snapshot>? pending;
    private uint world;
    private DateTimeOffset retryAfter;
    internal sealed record Snapshot(DateTimeOffset RetrievedAt, Dictionary<uint, int> Prices);
    internal Snapshot? Current { get; private set; }
    internal string Status { get; private set; } = "Prices have not been loaded.";
    internal VoyageMarketCache(string directory)
    {
        this.directory = directory;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Workshoppa/9.3 (voyage-estimates)");
    }
    internal void Update(uint homeWorld, uint[] items, bool refresh = false)
    {
        if (homeWorld == 0) { Status = "Log in to load home-world prices."; return; }
        if (pending is { IsCompleted: true })
        {
            try { Current = pending.GetAwaiter().GetResult(); Status = "Cached NQ average sale prices loaded."; }
            catch (Exception ex) { Status = "Price refresh failed: " + ex.GetBaseException().Message; retryAfter = DateTimeOffset.UtcNow.AddMinutes(1); }
            pending = null;
        }
        if (pending != null) return;
        if (world != homeWorld)
        {
            world = homeWorld; Current = null; retryAfter = default;
            try { Current = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(Path.Combine(directory, $"voyage-prices-{world}.json"))); }
            catch (Exception) { /* Missing/corrupt cache is replaced by a successful fetch. */ }
        }
        if (!refresh && (DateTimeOffset.UtcNow < retryAfter || Current is { } cached && DateTimeOffset.UtcNow - cached.RetrievedAt < TimeSpan.FromHours(1))) return;
        Status = "Refreshing cached price aggregates in the background…";
        var requestedWorld = world;
        pending = Fetch(requestedWorld, items, lifetime.Token);
    }
    private async Task<Snapshot> Fetch(uint requestedWorld, uint[] items, CancellationToken token)
    {
        var prices = new Dictionary<uint, int>();
        foreach (var batch in items.Chunk(100))
        {
            using var response = await http.GetAsync($"https://universalis.app/api/v2/aggregated/{requestedWorld}/{string.Join(',', batch)}", token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));
            foreach (var row in doc.RootElement.GetProperty("results").EnumerateArray())
            {
                if (!row.TryGetProperty("itemId", out var id) || !id.TryGetUInt32(out var item) || !batch.Contains(item)) continue;
                if (row.TryGetProperty("nq", out var nq) && nq.TryGetProperty("averageSalePrice", out var average) &&
                    average.TryGetProperty("world", out var market) && market.TryGetProperty("price", out var price) &&
                    price.TryGetDouble(out var value) && double.IsFinite(value) && value > 0)
                    prices[item] = (int)Math.Clamp(value, 1, int.MaxValue);
            }
        }
        var snapshot = new Snapshot(DateTimeOffset.UtcNow, prices);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"voyage-prices-{requestedWorld}.json");
        await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(snapshot), token).ConfigureAwait(false);
        File.Move(path + ".tmp", path, true);
        return snapshot;
    }
    public void Dispose() { lifetime.Cancel(); http.Dispose(); lifetime.Dispose(); }
}

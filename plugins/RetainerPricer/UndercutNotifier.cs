namespace RetainerPricer;

internal sealed record UndercutRow(ulong RetainerId, string Retainer, RetainerListingCacheEntry Listing,
    uint LowestPrice, DateTimeOffset SnapshotAt, DateTimeOffset MarketAt);

internal sealed class UndercutNotifier : IDisposable
{
    private readonly UniversalisClient client;
    private readonly PluginConfig config;
    private readonly CancellationTokenSource lifetime = new();
    private Task<(List<UndercutRow> Rows, string Status)>? pending;
    private HashSet<(ulong, uint, bool, uint)> alerted = [];
    private DateTimeOffset nextCheck;
    private uint currentWorld;
    private uint pendingWorld;
    private CancellationTokenSource? scanCancellation;
    public IReadOnlyList<UndercutRow> Rows { get; private set; } = [];
    public string Status { get; private set; } = "Refresh all retainer listings, then start watching.";
    public bool Running { get; private set; }
    public bool Checking => pending is not null;

    public UndercutNotifier(UniversalisClient client, PluginConfig config) => (this.client, this.config) = (client, config);
    public void Start() { Running = true; nextCheck = DateTimeOffset.MinValue; }
    public void Stop() { Running = false; scanCancellation?.Cancel(); Status = "Watching stopped."; }
    public void CheckNow() { Running = true; nextCheck = DateTimeOffset.MinValue; }

    // Called on the framework thread; only copied data is read by the async request worker.
    public void Update(MarketWorld? world, bool paused, Action<UndercutRow> notify)
    {
        if (world is null || paused)
        {
            scanCancellation?.Cancel();
            nextCheck = DateTimeOffset.MinValue;
            return;
        }
        if (currentWorld != world.WorldId)
        {
            currentWorld = world.WorldId;
            Rows = [];
            alerted.Clear();
            nextCheck = DateTimeOffset.MinValue;
        }
        if (pending is { IsCompleted: true })
        {
            var result = pending.GetAwaiter().GetResult();
            pending = null;
            var canceled = scanCancellation?.IsCancellationRequested == true;
            scanCancellation?.Dispose();
            scanCancellation = null;
            if (Running && pendingWorld == currentWorld && !canceled)
            {
                Rows = result.Rows;
                Status = result.Status;
                var active = Rows.Select(Key).ToHashSet();
                foreach (var row in Rows)
                    if (!alerted.Contains(Key(row)) && config.UndercutEchoMessages) notify(row);
                alerted.UnionWith(active);
            }
        }
        if (!Running || pending is not null || DateTimeOffset.UtcNow < nextCheck) return;
        var snapshots = config.UniversalisRetainerListings.Values
            .Where(value => value.Listings.Count > 0 && value.WorldId == world.WorldId)
            .Select(value => new RetainerListingCache
            {
                RetainerId = value.RetainerId, RetainerName = value.RetainerName, WorldId = value.WorldId,
                RefreshedAt = value.RefreshedAt, Listings = value.Listings.ToList()
            }).ToArray();
        if (snapshots.Length == 0) { Status = "No saved listings. Refresh all retainer listings first."; nextCheck = DateTimeOffset.UtcNow.AddSeconds(60); return; }
        nextCheck = DateTimeOffset.UtcNow.AddSeconds(60);
        Status = "Checking home-world prices with Universalis…";
        pendingWorld = world.WorldId;
        scanCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        pending = Scan(world.WorldId, snapshots, config.UniversalisRetainerListings.Keys.ToHashSet(), scanCancellation.Token);
    }

    private static (ulong, uint, bool, uint) Key(UndercutRow row) =>
        (row.RetainerId, row.Listing.ItemId, row.Listing.IsHq, row.Listing.Price);

    private async Task<(List<UndercutRow>, string)> Scan(uint world, RetainerListingCache[] snapshots, HashSet<ulong> own, CancellationToken token)
    {
        var rows = new List<UndercutRow>();
        var failed = 0;
        foreach (var id in snapshots.SelectMany(value => value.Listings).Select(value => value.ItemId).Distinct())
        {
            if (token.IsCancellationRequested) break;
            try
            {
                var prices = await client.FetchAsync(world, id, token, cacheMinutes: 0).ConfigureAwait(false);
                if (!prices.IsComplete) { failed++; continue; }
                foreach (var retainer in snapshots)
                    foreach (var listing in retainer.Listings.Where(value => value.ItemId == id && value.Price > 0))
                    {
                        var lowest = prices.Listings.Where(value => value.IsHq == listing.IsHq &&
                            value.PricePerUnit > 0 && value.RetainerId != 0 && !value.OnMannequin && !own.Contains(value.RetainerId))
                            .Select(value => value.PricePerUnit).DefaultIfEmpty(uint.MaxValue).Min();
                        if (lowest < listing.Price)
                        {
                            var row = new UndercutRow(retainer.RetainerId, retainer.RetainerName, listing, lowest,
                                retainer.RefreshedAt, prices.ObservedAt);
                            if (!rows.Any(existing => Key(existing) == Key(row))) rows.Add(row);
                        }
                    }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception) { failed++; }
        }
        return (rows, $"Last check {DateTimeOffset.Now:HH:mm:ss} · {rows.Count} undercut listing(s) · {failed} item check(s) unavailable. Checks repeat every 60 seconds.");
    }

    public void Dispose() { lifetime.Cancel(); scanCancellation?.Dispose(); lifetime.Dispose(); }
}

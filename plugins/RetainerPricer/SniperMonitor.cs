using System.Diagnostics;

namespace RetainerPricer;

internal sealed class SniperMonitor : IDisposable
{
    private const int MaximumDeals = 5000;
    private const int MaximumPendingLiveListings = 50_000;

    private sealed class RunState
    {
        public Dictionary<(uint ItemId, bool IsHq), SniperQualityBaseline> Baselines { get; } = [];
        public Dictionary<(uint ItemId, bool IsHq), SniperQualityBaseline> AvailableHistory { get; } = [];
        public HashSet<uint> CompletedHistoryItemIds { get; } = [];
        public Dictionary<string, UniversalisListing> PendingLiveListings { get; } = new(StringComparer.Ordinal);
        public HashSet<uint> WatchedItemIds { get; init; } = [];
        public HashSet<uint> FailedItemIds { get; } = [];
        public Dictionary<(uint ItemId, bool IsHq), decimal> CachedPrices { get; } = [];
        public HashSet<uint> CachedItems { get; } = [];
        public bool DiscoveryComplete { get; set; }
        public long LiveListingsSeen { get; set; }
        public DateTimeOffset? LastLiveListingAt { get; set; }
        public int CacheFailedBatches { get; set; }
        public HashSet<uint> ExistingOneGilItems { get; } = [];
        public Dictionary<uint, DateTimeOffset> ExistingOneGilNext { get; } = [];


        public HashSet<uint> CandidateIds { get; } = [];
        public HashSet<uint> OneGilCandidateIds { get; } = [];
        public bool PrioritizeOneGilHistory { get; init; }
        public Dictionary<uint, DateTimeOffset> NextCandidateCheck { get; } = [];
        public Dictionary<uint, int> CandidateAttempts { get; } = [];

        public HistoryScanSummary? Summary { get; set; }
        public int DroppedPendingListings { get; set; }
    }

    private sealed record HistoryScanSummary(int EligibleItems, IReadOnlyList<string> FailedBatches,
        string? FatalError = null, int FailedItems = 0);

    private readonly object gate = new();
    private readonly SemaphoreSlim historyVerificationGate = new(1, 1);
    private bool verifyingCandidate;
    private string verifyingItemName = "";
    private RunState? activeState;
    public IReadOnlyList<string> RequestDebug => universalis.SniperRequestDebug;
    public IReadOnlyList<string> CandidateDebug { get { lock (gate) return activeState is null ? [] : activeState.CandidateIds.Concat(activeState.FailedItemIds).Concat(activeState.ExistingOneGilItems).Distinct().Select(id => $"{itemNames.GetValueOrDefault(id, $"Item {id}")} (#{id}) · failed passes {activeState.CandidateAttempts.GetValueOrDefault(id)} · retry {(activeState.NextCandidateCheck.TryGetValue(id, out var next) ? next.ToLocalTime().ToString("HH:mm:ss") : "ready")}").ToArray(); } }

    public bool IsVerifyingCandidate { get { lock (gate) return verifyingCandidate; } }
    public IReadOnlyList<string> PendingVerificationItems { get { lock (gate) return activeState is null ? [] : activeState.CandidateIds.Concat(activeState.FailedItemIds).Concat(activeState.ExistingOneGilItems).Distinct().Select(id => itemNames.GetValueOrDefault(id, $"Item {id}")).ToArray(); } }

    private readonly UniversalisClient universalis;
    private readonly PluginConfig config;
    private readonly IReadOnlyList<ItemChoice> marketableItems;
    private readonly IReadOnlyDictionary<uint, string> itemNames;
    private readonly IReadOnlyDictionary<uint, string> worldNames;
    private readonly List<SniperDeal> deals = [];
    private CancellationTokenSource? cancellation;
    private string status = "Sniper is stopped.";
    private bool isListening;
    private bool worldOnlyRun;
    private int activeMarketScope;
    public int? WatchingMarketScope { get { lock (gate) return cancellation is not null && !cancellation.IsCancellationRequested ? activeMarketScope : null; } }
    private bool initialScanRunning;
    private string initialScanProgress = string.Empty;
    private Func<Task>? retryFailedHistory;
    private int failedHistoryItemCount;
    public int FailedHistoryItemCount { get { lock (gate) return failedHistoryItemCount; } }
    public void RetryFailedItems()
    {
        lock (gate)
        {
            if (initialScanRunning || cancellation is null || failedHistoryItemCount == 0 || retryFailedHistory is null) return;
            initialScanRunning = true;
            initialScanProgress = "Retrying failed history items…";
            _ = Task.Run(retryFailedHistory);
        }
    }
    private string liveFeedStatus = "not connected";

    public SniperMonitor(UniversalisClient universalis, PluginConfig config, IReadOnlyList<ItemChoice> items,
        IReadOnlyDictionary<uint, string> worldNames)
    {
        this.universalis = universalis;
        this.config = config;
        marketableItems = items;
        itemNames = items.ToDictionary(item => item.ItemId, item => item.Name);
        this.worldNames = worldNames;
    }

    public bool IsListening { get { lock (gate) return isListening; } }
    public bool IsRunning { get { lock (gate) return cancellation is not null; } }
    public bool IsWatchingWorld { get { lock (gate) return cancellation is not null && !cancellation.IsCancellationRequested && worldOnlyRun; } }
    public bool IsInitialScanRunning { get { lock (gate) return initialScanRunning; } }
    public string Status
    {
        get
        {
            lock (gate)
            {
                if (cancellation is null || initialScanRunning || activeState is not { DiscoveryComplete: true } state) return status;
                var now = DateTimeOffset.UtcNow;
                var ready = state.CandidateIds.Count(id => !state.NextCandidateCheck.TryGetValue(id, out var next) || next <= now);
                var deferred = state.CandidateIds.Count - ready;
                var activity = verifyingCandidate ? $"Verifying history: {verifyingItemName}"
                    : state.ExistingOneGilItems.Count > 0 ? $"Checking existing 1-gil offers ({state.ExistingOneGilItems.Count} lookups pending)"
                    : ready > 0 ? $"Processing verification queue ({ready} ready)"
                    : deferred > 0 ? $"Waiting to retry failed candidates; next attempt in {Math.Max(0, Math.Ceiling((state.CandidateIds.Select(id => state.NextCandidateCheck.GetValueOrDefault(id, now)).Min() - now).TotalSeconds)):N0}s"
                    : "Watching live listings — waiting for new candidates";
                return $"{activity} · {liveFeedStatus} · {state.LiveListingsSeen:N0} new listings received · {ready:N0} ready / {deferred:N0} deferred · {state.CompletedHistoryItemIds.Count:N0} items checked ({state.Baselines.Keys.Select(key => key.ItemId).Distinct().Count():N0} with usable history) · {state.FailedItemIds.Count:N0} pending history failures · {state.CacheFailedBatches:N0} cache batch failures · {state.ExistingOneGilItems.Count:N0} existing 1-gil lookups pending. Cached discovery is complete; {(config.SniperSkipOneGilLookup ? "existing 1-gil lookup skipped" : "existing 1-gil offers checked once")}, live alerts remain active.";
            }
        }
    }
    public IReadOnlyList<SniperDeal> Deals { get { lock (gate) return deals.ToArray(); } }
    public IReadOnlyList<SniperDeal> TakeItemForBuyList(string name)
    {
        lock (gate)
        {
            var rows = deals.Where(deal => StringComparer.OrdinalIgnoreCase.Equals(deal.ItemName, name)).ToArray();
            deals.RemoveAll(deal => StringComparer.OrdinalIgnoreCase.Equals(deal.ItemName, name));
            return rows;
        }
    }
    public int MarketableItemCount => marketableItems.Count;
    public string DiscoveryHelp => "Cached four-day averages screen live candidates; detailed history verifies your selected window. Candidates within threshold + 25 percentage points (capped at 125% of average) are checked. Unknown cache values are checked too. Cache refresh age is not provided; source upload timestamps do not prove aggregate refresh time. This screen can miss deals versus a full history scan.";
    public IReadOnlyList<ItemChoice> MarketableItems => marketableItems;

    public void Start(MarketWorld? world, bool useDataCenter, bool useRegion, IReadOnlyCollection<uint>? selectedItemIds = null)
    {
        if (world is null)
        {
            SetStatus("Log in to start Sniper.");
            return;
        }
        var selectedIds = selectedItemIds?.ToHashSet();
        IReadOnlyList<ItemChoice> watchedItems = selectedIds is { Count: > 0 }
            ? marketableItems.Where(item => selectedIds.Contains(item.ItemId)).ToArray() : marketableItems;
        if (watchedItems.Count == 0) { SetStatus("Select a marketable item before starting Sniper."); return; }
        CancellationTokenSource run;
        lock (gate)
        {
            if (cancellation is not null) return;
            cancellation = run = new CancellationTokenSource();
            worldOnlyRun = !useDataCenter && !useRegion;
            activeMarketScope = useRegion ? 2 : useDataCenter ? 1 : 0;
            failedHistoryItemCount = 0;
            retryFailedHistory = null;
            deals.Clear();
            isListening = false;
            initialScanRunning = false;
            initialScanProgress = string.Empty;
            liveFeedStatus = "not connected";
            status = $"Preparing to scan {watchedItems.Count:N0} marketable items using the selected market scope…";
        }
        _ = Task.Run(() => RunAsync(world, useDataCenter, useRegion, watchedItems, Math.Clamp(config.SniperMinimumSales14Days, 1, 500),
            Math.Clamp(config.SniperThresholdFraction, 0.01, 1.0), Math.Clamp(config.SniperHistoryDays, 0, 14),
            Math.Clamp(config.SniperMinimumItemPrice, 1, 999_999_999), run.Token));
    }

    public void Stop()
    {
        CancellationTokenSource? active;
        lock (gate) active = cancellation;
        try { active?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    private async Task RunAsync(MarketWorld world, bool useDataCenter, bool useRegion,
        IReadOnlyList<ItemChoice> watchedItems, int minimumSales, double threshold, int historyDays, int minimumItemPrice,
        CancellationToken cancellationToken)
    {
        try
        {
            var scope = await universalis.ResolveSniperMarketScopeAsync(world, useDataCenter, useRegion, cancellationToken)
                .ConfigureAwait(false);
            var watchedWorlds = scope.WorldIds.ToHashSet();
            if (watchedWorlds.Count == 0)
                throw new InvalidOperationException("The selected Sniper scope did not contain any supported worlds.");

            var runState = new RunState { WatchedItemIds = watchedItems.Select(item => item.ItemId).ToHashSet(), PrioritizeOneGilHistory = !config.SniperSkipOneGilLookup };
            lock (gate)
            {
                activeState = runState;
                retryFailedHistory = async () =>
                {
                    ItemChoice[] failedItems;
                    lock (gate) failedItems = watchedItems.Where(item => runState.FailedItemIds.Contains(item.ItemId)).ToArray();
                    await ScanHistoryAsync(scope, failedItems, world, watchedWorlds, minimumSales,
                        threshold, historyDays, minimumItemPrice, runState, cancellationToken, slowRescan: true).ConfigureAwait(false);
                };
                initialScanRunning = true;
                initialScanProgress = "Initial scan is starting";
                liveFeedStatus = "connecting to Universalis live listings";
                RefreshInitialScanStatusAndGetLocked(runState);
            }

            // Start listening before the catalog scan completes. Live listings for unscanned
            // items are held until their history batch supplies a usable HQ/NQ baseline.
            var historyScanTask = ScanCachedCatalogAsync(scope, watchedItems, world, watchedWorlds, minimumSales,
                threshold, historyDays, minimumItemPrice, runState, cancellationToken);
            HistoryScanSummary? historySummary = null;

            var backoffSeconds = 2;
            while (!cancellationToken.IsCancellationRequested)
            {
                var reconnectReason = "the server closed the connection";
                try
                {
                    await using var socket = new UniversalisWebSocketClient();
                    await socket.ConnectAsync(cancellationToken).ConfigureAwait(false);
                    foreach (var worldId in watchedWorlds)
                    {
                        await socket.SubscribeWorldAsync(worldId, "add", cancellationToken).ConfigureAwait(false);
                        await socket.SubscribeWorldAsync(worldId, "remove", cancellationToken).ConfigureAwait(false);
                    }
                    // The scanner flips initialScanRunning off just before its Task completes.
                    // Await it in that tiny window so the status below never sees a null summary.
                    if (historySummary is null && (!IsInitialScanRunning || historyScanTask.IsCompleted))
                        historySummary = await historyScanTask.ConfigureAwait(false);
                    backoffSeconds = 2;
                    lock (gate)
                    {
                        isListening = true;
                        liveFeedStatus = "live feed connected";
                        status = initialScanRunning
                            ? RefreshInitialScanStatusAndGetLocked(runState)
                            : BuildListeningStatus(scope, watchedWorlds.Count, watchedItems.Count, historyDays, minimumSales, runState.Summary ?? historySummary!);
                    }

                    while (!cancellationToken.IsCancellationRequested && socket.State == System.Net.WebSockets.WebSocketState.Open)
                    {
                        var receiveTask = socket.ReceiveListingsAsync(cancellationToken);
                        IReadOnlyList<UniversalisListing>? listings;
                        if (historySummary is null)
                        {
                            var completedTask = await Task.WhenAny(receiveTask, historyScanTask).ConfigureAwait(false);
                            if (completedTask == historyScanTask)
                            {
                                historySummary = await historyScanTask.ConfigureAwait(false);
                                lock (gate)
                                    status = BuildListeningStatus(scope, watchedWorlds.Count, watchedItems.Count,
                                        historyDays, minimumSales, runState.Summary ?? historySummary);
                            }
                            listings = await receiveTask.ConfigureAwait(false);
                        }
                        else
                        {
                            listings = await receiveTask.ConfigureAwait(false);
                        }
                        if (listings is null) break;
                        HandleLiveListings(listings, world, scope, watchedWorlds, runState, threshold, minimumItemPrice);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    reconnectReason = ex.Message;
                }

                if (cancellationToken.IsCancellationRequested) break;
                lock (gate)
                {
                    isListening = false;
                    liveFeedStatus = $"live feed reconnecting in {backoffSeconds}s ({reconnectReason})";
                    status = initialScanRunning
                        ? RefreshInitialScanStatusAndGetLocked(runState)
                        : $"Universalis live feed disconnected ({reconnectReason}). Reconnecting in {backoffSeconds}s…";
                }
                await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), cancellationToken).ConfigureAwait(false);
                backoffSeconds = Math.Min(backoffSeconds * 2, 30);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception ex)
        {
            SetStatus($"Sniper stopped: {ex.Message}");
        }
        finally
        {
            lock (gate)
            {
                isListening = false;
                initialScanRunning = false;
                cancellation?.Dispose();
                cancellation = null;
                if (cancellationToken.IsCancellationRequested)
                    status = "Sniper stopped.";
                else if (string.IsNullOrWhiteSpace(status) || status.StartsWith("Listening", StringComparison.Ordinal))
                    status = "Sniper is stopped.";
            }
        }
    }

    private async Task<HistoryScanSummary> ScanCachedCatalogAsync(SniperMarketScope scope,
        IReadOnlyList<ItemChoice> watchedItems, MarketWorld world, IReadOnlySet<uint> watchedWorlds,
        int minimumSales, double threshold, int historyDays, int minimumItemPrice, RunState state, CancellationToken ct)
    {
        var errors = new System.Collections.Concurrent.ConcurrentBag<string>();
        var done = 0;
        await Parallel.ForEachAsync(watchedItems.Chunk(100), new ParallelOptions { MaxDegreeOfParallelism = 6, CancellationToken = ct }, async (batch, token) =>
        {
            try
            {
                var prices = await universalis.FetchSniperCachedPricesAsync(scope.HistoryScopes.Single(), batch.Select(item => item.ItemId).ToArray(), token, id => { lock (gate) { if (!config.SniperSkipOneGilLookup) state.ExistingOneGilItems.Add(id); } }).ConfigureAwait(false);
                lock (gate)
                {
                    foreach (var pair in prices) state.CachedPrices[pair.Key] = pair.Value;
                    foreach (var item in batch) state.CachedItems.Add(item.ItemId);
                    foreach (var listing in state.PendingLiveListings.Values.ToArray()) QueueCandidateLocked(listing, state, threshold, minimumItemPrice);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { errors.Add(ex.Message); }
            lock (gate)
            {
                done += batch.Length;
                initialScanProgress = $"Cached discovery · {done:N0}/{watchedItems.Count:N0} items attempted · {errors.Count} cache batch failures · detailed history is fetched only for live candidates";
                RefreshInitialScanStatusAndGetLocked(state);
            }
        }).ConfigureAwait(false);
        var summary = new HistoryScanSummary(0, errors.ToArray());
        lock (gate)
        {
            state.CacheFailedBatches = errors.Count;
            state.DiscoveryComplete = true;
            foreach (var listing in state.PendingLiveListings.Values.ToArray()) QueueCandidateLocked(listing, state, threshold, minimumItemPrice);
            state.Summary = summary;
            initialScanRunning = false;
        }
        _ = Task.Run(async () =>
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    uint[] existingIds;
                    lock (gate) existingIds = state.ExistingOneGilItems.Where(id => !state.ExistingOneGilNext.TryGetValue(id, out var next) || next <= DateTimeOffset.UtcNow).Take(100).ToArray();
                    if (existingIds.Length > 0)
                    {
                        lock (gate) { verifyingCandidate = true; verifyingItemName = $"existing 1-gil listings: {existingIds.Length} items"; }
                        try
                        {
                            var listings = await universalis.FetchExistingOneGilAsync(scope.HistoryScopes.Single(), existingIds, world.WorldId, ct).ConfigureAwait(false);
                            lock (gate)
                            {
                                foreach (var listing in listings.Values.SelectMany(offers => offers).Where(listing => watchedWorlds.Contains(listing.WorldId)))
                                    ProcessLiveListingLocked(new UniversalisListing(listing.ItemId, listing.WorldId, listing.IsHq, 1, listing.Quantity, null), world, scope, watchedWorlds, state, threshold, minimumItemPrice);
                                foreach (var id in existingIds)
                                    if (listings.ContainsKey(id)) { state.ExistingOneGilItems.Remove(id); state.ExistingOneGilNext.Remove(id); }
                                    else state.ExistingOneGilNext[id] = DateTimeOffset.UtcNow.AddSeconds(30);
                            }
                        }
                        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                        catch (Exception) { lock (gate) foreach (var id in existingIds) state.ExistingOneGilNext[id] = DateTimeOffset.UtcNow.AddSeconds(30); }
                        finally { lock (gate) verifyingCandidate = false; }
                        await Task.Delay(1000, ct).ConfigureAwait(false);
                        continue;
                    }
                    ItemChoice? item;
                    lock (gate)
                    {
                        var id = state.CandidateIds.Where(id => !state.NextCandidateCheck.TryGetValue(id, out var next) || next <= DateTimeOffset.UtcNow).OrderBy(id => state.CandidateAttempts.GetValueOrDefault(id)).ThenBy(id => state.PrioritizeOneGilHistory && state.OneGilCandidateIds.Contains(id) ? 0 : 1).FirstOrDefault();
                        item = watchedItems.FirstOrDefault(item => item.ItemId == id);
                        if (item is not null) state.CandidateIds.Remove(item.ItemId);
                    }
                    if (item is null) { await Task.Delay(1000, ct).ConfigureAwait(false); continue; }
                    await ScanHistoryAsync(scope, new[] { item }, world, watchedWorlds, minimumSales, threshold, historyDays, minimumItemPrice, state, ct).ConfigureAwait(false);
                    lock (gate)
                    {
                        if (state.FailedItemIds.Contains(item.ItemId))
                        {
                            var attempts = Math.Min(1_000_000, state.CandidateAttempts.GetValueOrDefault(item.ItemId) + 1);
                            state.CandidateAttempts[item.ItemId] = attempts;
                            state.NextCandidateCheck[item.ItemId] = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, 15 * attempts));
                            state.CandidateIds.Add(item.ItemId);
                        }
                    }
                    await Task.Delay(5000, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex) { universalis.DiagnosticLog?.Invoke($"Sniper candidate verification stopped: {ex.Message}"); }
        }, ct);
        return summary;
    }

    private static void QueueCandidateLocked(UniversalisListing listing, RunState state, double threshold, int minimumPrice)
    {
        if (listing.PricePerUnit == 1) state.OneGilCandidateIds.Add(listing.ItemId);
        if (listing.PricePerUnit != 1 && !state.DiscoveryComplete && !state.CachedItems.Contains(listing.ItemId)) return;
        if (state.CompletedHistoryItemIds.Contains(listing.ItemId)) return;
        if (listing.PricePerUnit == 1 || listing.PricePerUnit >= minimumPrice &&
            (!state.CachedPrices.TryGetValue((listing.ItemId, listing.IsHq), out var average) ||
                listing.PricePerUnit <= average * (decimal)Math.Min(1.25, threshold + 0.25)))
            state.CandidateIds.Add(listing.ItemId);
    }

    private async Task<HistoryScanSummary> ScanHistoryAsync(SniperMarketScope scope,
        IReadOnlyList<ItemChoice> watchedItems, MarketWorld world, IReadOnlySet<uint> watchedWorlds,
        int minimumSales, double threshold, int historyDays, int minimumItemPrice, RunState runState,
        CancellationToken cancellationToken, bool autoRetry = false, bool slowRescan = false)
    {
        await historyVerificationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        lock (gate) { verifyingCandidate = true; verifyingItemName = watchedItems.Count == 1 ? watchedItems[0].Name : $"{watchedItems.Count} retry items"; }
        try { return await ScanHistoryCoreAsync(scope, watchedItems, world, watchedWorlds, minimumSales, threshold, historyDays, minimumItemPrice, runState, cancellationToken, slowRescan: slowRescan).ConfigureAwait(false); }
        finally { lock (gate) verifyingCandidate = false; historyVerificationGate.Release(); }
    }

    private async Task<HistoryScanSummary> ScanHistoryCoreAsync(SniperMarketScope scope,
        IReadOnlyList<ItemChoice> watchedItems, MarketWorld world, IReadOnlySet<uint> watchedWorlds,
        int minimumSales, double threshold, int historyDays, int minimumItemPrice, RunState runState,
        CancellationToken cancellationToken, bool autoRetry = false, bool slowRescan = false)
    {
        var failed = new System.Collections.Concurrent.ConcurrentBag<string>();
        var batchSize = UniversalisClient.SniperHistoryBatchSize;
        var batchCount = (watchedItems.Count + batchSize - 1) / batchSize;
        var scanStartedAt = Stopwatch.GetTimestamp();
        universalis.DiagnosticLog?.Invoke($"Sniper scan started: scope={scope.Label}; items={watchedItems.Count}; historyDays={historyDays}; minimumSales={minimumSales}; concurrentBatches=1; maxBatchItems={batchSize}; maxSalesPerItem={UniversalisClient.SniperHistoryEntryLimit}");
        var completedBatches = 0;
        var processedItems = 0;
        var loadedItems = 0;
        var failedItemsThisScan = 0;
        try
        {
            await Parallel.ForEachAsync(watchedItems.Chunk(batchSize),
                new ParallelOptions { MaxDegreeOfParallelism = 1, CancellationToken = cancellationToken },
                async (batch, batchCancellation) =>
            {
                batchCancellation.ThrowIfCancellationRequested();
                var successfulItemIds = new HashSet<uint>();
                var batchBaselines = new Dictionary<(uint ItemId, bool IsHq), SniperQualityBaseline>();
                var availableHistory = new Dictionary<(uint ItemId, bool IsHq), SniperQualityBaseline>();
                try
                {
                    var batchSales = new Dictionary<uint, List<SniperSale>>();
                    var returnedInEveryScope = batch.Select(item => item.ItemId).ToHashSet();
                    foreach (var historyScope in scope.HistoryScopes)
                    {
                        var snapshots = await universalis.FetchSniperSalesBatchAsync(historyScope,
                                batch.Select(item => item.ItemId).ToArray(), historyDays, world.WorldId, cancellationToken, slowRescan)
                            .ConfigureAwait(false);
                        returnedInEveryScope.IntersectWith(snapshots.Keys);
                        foreach (var snapshot in snapshots.Values)
                        {
                            if (!batchSales.TryGetValue(snapshot.ItemId, out var sales))
                                batchSales[snapshot.ItemId] = sales = [];
                            sales.AddRange(snapshot.Sales);
                        }
                    }
                    if (returnedInEveryScope.Count != batch.Length)
                    {
                        var missingIds = batch.Select(item => item.ItemId).Where(id => !returnedInEveryScope.Contains(id)).ToArray();
                        failed.Add($"Partial history response: {missingIds.Length} item(s) missing usable responses.");
                        universalis.DiagnosticLog?.Invoke($"Sniper partial history response: scope={scope.Label}; requestedItems={batch.Length}; returnedItems={returnedInEveryScope.Count}; missingIds={string.Join(',', missingIds)}");
                    }
                    foreach (var (itemId, sales) in batchSales)
                    {
                        if (!returnedInEveryScope.Contains(itemId)) continue;
                        var fetchedAt = DateTimeOffset.UtcNow;
                        var oldestSale = sales.Where(sale => sale.SoldAt.HasValue).Min(sale => sale.SoldAt);
                        double? coverageDays = oldestSale is { } oldest && oldest < fetchedAt
                            ? (fetchedAt - oldest).TotalDays : null;
                        var capped = sales.Count >= UniversalisClient.SniperHistoryEntryLimit;
                        foreach (var qualitySales in sales.GroupBy(sale => sale.IsHq))
                        {
                            var orderedPrices = qualitySales.Select(sale => sale.PricePerUnit).OrderBy(price => price).ToArray();
                            if (orderedPrices.Length == 0) continue;
                            var middle = orderedPrices.Length / 2;
                            var median = orderedPrices.Length % 2 == 0
                                ? (uint)(((ulong)orderedPrices[middle - 1] + orderedPrices[middle]) / 2)
                                : orderedPrices[middle];
                            var history = new SniperQualityBaseline(qualitySales.Key, orderedPrices.Length, median,
                                historyDays, fetchedAt, qualitySales.Max(sale => sale.SoldAt), coverageDays, capped, qualitySales.Where(sale => sale.SoldAt.HasValue).OrderByDescending(sale => sale.SoldAt).FirstOrDefault()?.PricePerUnit);
                            availableHistory[(itemId, qualitySales.Key)] = history;
                            if (orderedPrices.Length >= minimumSales) batchBaselines[(itemId, qualitySales.Key)] = history;
                        }
                        successfulItemIds.Add(itemId);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    var label = batch.Length == 1 ? batch[0].Name : $"{batch[0].Name} … {batch[^1].Name}";
                    failed.Add($"{label}: {ex.Message}");
                }

                var batchItemIds = batch.Select(item => item.ItemId).ToHashSet();
                lock (gate)
                {
                    foreach (var (key, baseline) in batchBaselines)
                        runState.Baselines[key] = baseline;
                    foreach (var (key, history) in availableHistory) runState.AvailableHistory[key] = history;
                    for (var dealIndex = 0; dealIndex < deals.Count; dealIndex++)
                    {
                        var alert = deals[dealIndex];
                        if (alert.IsOneGilAlert && availableHistory.TryGetValue((alert.ItemId, alert.IsHq), out var history))
                            deals[dealIndex] = alert with { MedianSalePrice = history.MedianSalePrice,
                                SalesInHistoryWindow = history.SaleCount, HistoryDays = history.HistoryDays,
                                HistoryFetchedAt = history.HistoryFetchedAt, LastSaleAt = history.LastSaleAt,
                                SampleCoverageDays = history.SampleCoverageDays, HistoryWasCapped = history.HistoryWasCapped, LastSalePrice = history.LastSalePrice };
                    }
                    foreach (var itemId in batchItemIds)
                    {
                        if (successfulItemIds.Contains(itemId)) { runState.FailedItemIds.Remove(itemId); runState.CompletedHistoryItemIds.Add(itemId); runState.CandidateIds.Remove(itemId); runState.NextCandidateCheck.Remove(itemId); runState.CandidateAttempts.Remove(itemId); }
                        else runState.FailedItemIds.Add(itemId);
                    }
                    failedHistoryItemCount = runState.FailedItemIds.Count;
                    foreach (var pending in runState.PendingLiveListings
                                 .Where(pair => batchItemIds.Contains(pair.Value.ItemId) && successfulItemIds.Contains(pair.Value.ItemId)).ToArray())
                    {
                        runState.PendingLiveListings.Remove(pending.Key);
                        ProcessLiveListingLocked(pending.Value, world, scope, watchedWorlds, runState, threshold, minimumItemPrice);
                    }
                    processedItems += batch.Length;
                    loadedItems += successfulItemIds.Count;
                    failedItemsThisScan += batch.Length - successfulItemIds.Count;
                    completedBatches++;
                    initialScanProgress = $"Initial scan · {processedItems:N0}/{watchedItems.Count:N0} items attempted · {loadedItems:N0} history loaded · {failedItemsThisScan:N0} failed · {(historyDays == 0 ? "any-age history" : $"{historyDays}-day history")} · {completedBatches}/{batchCount} batches attempted · {FormatEta(Stopwatch.GetElapsedTime(scanStartedAt), completedBatches, batchCount - completedBatches)}";
                    RefreshInitialScanStatusAndGetLocked(runState);
                }
            }).ConfigureAwait(false);

            if (autoRetry && runState.FailedItemIds.Count > 0)
            {
                ItemChoice[] retryItems;
                lock (gate)
                {
                    retryItems = watchedItems.Where(item => runState.FailedItemIds.Contains(item.ItemId)).ToArray();
                    initialScanProgress = $"Full scan attempted · automatically retrying {retryItems.Length:N0} failed items after a 15-second cooldown…";
                    RefreshInitialScanStatusAndGetLocked(runState);
                }
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
                return await ScanHistoryAsync(scope, retryItems, world, watchedWorlds, minimumSales,
                    threshold, historyDays, minimumItemPrice, runState, cancellationToken, slowRescan: true).ConfigureAwait(false);
            }
            var eligible = runState.Baselines.Keys.Select(key => key.ItemId).Distinct().Count();
            var summary = new HistoryScanSummary(eligible, failed.ToArray(), FailedItems: runState.FailedItemIds.Count);
            universalis.DiagnosticLog?.Invoke($"Sniper scan finished: scope={scope.Label}; requestedItems={watchedItems.Count}; failedBatches={failed.Count}; unresolvedItems={summary.FailedItems}; usableItems={eligible}; elapsedSeconds={Stopwatch.GetElapsedTime(scanStartedAt).TotalSeconds:F2}");
            lock (gate)
            {
                runState.Summary = summary;
                initialScanRunning = false;
                status = isListening
                    ? BuildListeningStatus(scope, scope.WorldIds.Count, runState.WatchedItemIds.Count, historyDays, minimumSales, summary)
                    : $"Initial history scan complete · {liveFeedStatus}. {BuildHistorySummary(summary, watchedItems.Count, minimumSales, historyDays)}";
            }
            return summary;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lock (gate) initialScanRunning = false;
            return new HistoryScanSummary(runState.Baselines.Keys.Select(key => key.ItemId).Distinct().Count(), failed.ToArray());
        }
        catch (Exception ex)
        {
            var summary = new HistoryScanSummary(runState.Baselines.Keys.Select(key => key.ItemId).Distinct().Count(), failed.ToArray(), ex.Message);
            lock (gate)
            {
                runState.Summary = summary;
                initialScanRunning = false;
                status = isListening
                    ? BuildListeningStatus(scope, scope.WorldIds.Count, runState.WatchedItemIds.Count, historyDays, minimumSales, summary)
                    : $"Initial history scan stopped: {ex.Message}. {liveFeedStatus}.";
            }
            return summary;
        }
    }

    private void HandleLiveListings(IReadOnlyList<UniversalisListing> listings, MarketWorld world, SniperMarketScope scope,
        IReadOnlySet<uint> watchedWorlds, RunState runState, double threshold, int minimumItemPrice)
    {
        lock (gate)
        {
            foreach (var listing in listings)
            {
                if (!listing.IsRemoved && runState.WatchedItemIds.Contains(listing.ItemId))
                {
                    runState.LiveListingsSeen++;
                    runState.LastLiveListingAt = DateTimeOffset.UtcNow;
                }
                ProcessLiveListingLocked(listing, world, scope, watchedWorlds, runState, threshold, minimumItemPrice);
            }
        }
    }

    private void ProcessLiveListingLocked(UniversalisListing listing, MarketWorld world, SniperMarketScope scope,
        IReadOnlySet<uint> watchedWorlds, RunState runState, double threshold, int minimumItemPrice)
    {
        if (!runState.WatchedItemIds.Contains(listing.ItemId)) return;
        var eventKey = ListingEventKey(listing);
        if (!listing.IsRemoved) QueueCandidateLocked(listing, runState, threshold, minimumItemPrice);
        if (listing.IsRemoved)
        {
            runState.PendingLiveListings.Remove(eventKey);
            if (listing.ListingId is { Length: > 0 } removedId)
                deals.RemoveAll(previous => previous.Key == $"{listing.WorldId}:{listing.ItemId}:{removedId}" ||
                    previous.IsOneGilAlert && previous.ItemId == listing.ItemId &&
                    previous.WorldId == listing.WorldId && previous.IsHq == listing.IsHq &&
                    (previous.ListingId == removedId || previous.ListingId is null &&
                        previous.PricePerUnit == listing.PricePerUnit && previous.Quantity == listing.Quantity));
            else if (listing.PricePerUnit == 1)
                deals.RemoveAll(previous => previous.IsOneGilAlert && previous.ItemId == listing.ItemId &&
                    previous.WorldId == listing.WorldId && previous.IsHq == listing.IsHq && previous.Quantity == listing.Quantity);
            else
                deals.RemoveAll(previous => previous.ItemId == listing.ItemId && previous.WorldId == listing.WorldId &&
                    previous.IsHq == listing.IsHq && previous.PricePerUnit == listing.PricePerUnit && previous.Quantity == listing.Quantity);
            if (initialScanRunning) RefreshInitialScanStatusAndGetLocked(runState);
            return;
        }

        if (!watchedWorlds.Contains(listing.WorldId) || !itemNames.TryGetValue(listing.ItemId, out var name))
            return;

        if ((config.SniperBuyList ?? []).Any(row => StringComparer.OrdinalIgnoreCase.Equals(row.ItemName, name))) return;
        var isOneGilAlert = listing.PricePerUnit == 1;
        if (isOneGilAlert && config.SniperSkipOneGilLookup && !runState.Baselines.ContainsKey((listing.ItemId, listing.IsHq)))
        {
            if (!runState.CompletedHistoryItemIds.Contains(listing.ItemId)) runState.PendingLiveListings[eventKey] = listing;
            return;
        }
        if (!isOneGilAlert && listing.ListingId is { Length: > 0 } updatedId)
            deals.RemoveAll(previous => previous.IsOneGilAlert && previous.ListingId == updatedId &&
                previous.WorldId == listing.WorldId && previous.ItemId == listing.ItemId);
        SniperQualityBaseline? baseline = null;
        if (isOneGilAlert) (config.SniperSkipOneGilLookup ? runState.Baselines : runState.AvailableHistory).TryGetValue((listing.ItemId, listing.IsHq), out baseline);
        if (!isOneGilAlert)
        {
            if (!runState.Baselines.TryGetValue((listing.ItemId, listing.IsHq), out baseline))
            {
                if (!runState.CompletedHistoryItemIds.Contains(listing.ItemId))
                {
                    if (runState.PendingLiveListings.ContainsKey(eventKey) ||
                        runState.PendingLiveListings.Count < MaximumPendingLiveListings)
                        runState.PendingLiveListings[eventKey] = listing;
                    else
                        runState.DroppedPendingListings++;
                }
                return;
            }

            // A listing update may reuse its ID with a price that no longer qualifies.
            deals.RemoveAll(previous => previous.Key == eventKey);
            if (listing.PricePerUnit < minimumItemPrice || listing.PricePerUnit > baseline.MedianSalePrice * threshold)
            {
                if (initialScanRunning) RefreshInitialScanStatusAndGetLocked(runState);
                return;
            }
        }

        var listingWorldName = worldNames.TryGetValue(listing.WorldId, out var resolvedWorldName)
            ? resolvedWorldName : listing.WorldId == world.WorldId ? world.Name : $"World {listing.WorldId}";
        var dataCenterName = scope.WorldDataCenterNames.GetValueOrDefault(listing.WorldId)
            ?? (listing.WorldId == world.WorldId ? world.DataCenterName : null);
        var dealKey = isOneGilAlert
            ? $"alert:{listing.WorldId}:{listing.ItemId}:{listing.IsHq}"
            : eventKey;
        var deal = new SniperDeal(dealKey, listing.ItemId, name, listing.WorldId, listingWorldName,
            listing.IsHq, listing.PricePerUnit, listing.Quantity, baseline?.MedianSalePrice ?? 0,
            baseline?.SaleCount ?? 0, DateTimeOffset.UtcNow, listing.ListingId, isOneGilAlert,
            baseline?.HistoryDays ?? config.SniperHistoryDays, baseline?.HistoryFetchedAt, baseline?.LastSaleAt, dataCenterName, baseline?.SampleCoverageDays, baseline?.HistoryWasCapped ?? false, baseline?.LastSalePrice);
        if (isOneGilAlert)
            deals.RemoveAll(previous => previous.IsOneGilAlert && previous.ItemId == listing.ItemId &&
                previous.WorldId == listing.WorldId && previous.IsHq == listing.IsHq);
        else
            deals.RemoveAll(previous => previous.Key == dealKey);
        deals.Insert(0, deal);
        if (deals.Count > MaximumDeals) deals.RemoveRange(MaximumDeals, deals.Count - MaximumDeals);
        if (initialScanRunning) RefreshInitialScanStatusAndGetLocked(runState);
    }

    private static string ListingEventKey(UniversalisListing listing) =>
        listing.ListingId is { Length: > 0 } id
            ? $"{listing.WorldId}:{listing.ItemId}:{id}"
            : $"{listing.WorldId}:{listing.ItemId}:{listing.IsHq}:{listing.PricePerUnit}:{listing.Quantity}";

    private void SetInitialScanProgress(string progress, RunState runState)
    {
        lock (gate)
        {
            initialScanProgress = progress;
            RefreshInitialScanStatusAndGetLocked(runState);
        }
    }

    private string RefreshInitialScanStatusAndGetLocked(RunState runState)
    {
        var dropped = runState.DroppedPendingListings > 0
            ? $" · {runState.DroppedPendingListings:N0} early listing updates skipped due to the buffer limit"
            : string.Empty;
        status = $"{initialScanProgress} · {deals.Count:N0} live deal(s) found so far · {liveFeedStatus}{dropped}";
        return status;
    }

    private string BuildListeningStatus(SniperMarketScope scope, int watchedWorldCount, int watchedItemCount,
        int historyDays, int minimumSales, HistoryScanSummary summary)
    {
        var pending = activeState?.CandidateIds.Count ?? 0;
        var status = $"Listening on {scope.Label} across {watchedWorldCount:N0} world(s) · {watchedItemCount:N0} watched items · cached discovery finished · {summary.EligibleItems:N0} items with verified usable history · {pending:N0} pending verification · {summary.FailedItems:N0} history failures. 1-gil alerts are immediate; ordinary deals require verified history. Purchases are manual.";
        if (summary.FatalError is not null) status += $" Verification error: {summary.FatalError}";
        return status;
    }

    private static string BuildHistorySummary(HistoryScanSummary summary, int watchedItemCount,
        int minimumSales, int historyDays)
    {
        var details = new List<string>();
        if (summary.FailedItems > 0) details.Add($"{summary.FailedItems:N0} item(s) missing history because their requests failed. Use Retry failed items; ordinary deals for these items cannot be evaluated.");
        if (summary.EligibleItems == 0 && watchedItemCount > 0)
            details.Add($"No item reached the minimum of {minimumSales} sales {(historyDays == 0 ? "of any age" : $"in {historyDays} days")}; ordinary deals need a usable baseline.");
        if (summary.FailedBatches.Count > 0)
            details.Add($"{summary.FailedBatches.Count} history batch(es) failed; first error: {summary.FailedBatches[0]}.");
        if (summary.FatalError is { Length: > 0 })
            details.Add($"The history scan stopped early: {summary.FatalError}.");
        return string.Join(" ", details);
    }

    private static string FormatEta(TimeSpan elapsed, int completedBatches, int remainingBatches)
    {
        if (completedBatches <= 0 || remainingBatches <= 0 || elapsed <= TimeSpan.Zero)
            return "ETA calculating";

        var secondsRemaining = Math.Ceiling(elapsed.TotalSeconds / completedBatches * remainingBatches);
        if (secondsRemaining < 60) return $"ETA about {Math.Max(1, secondsRemaining):N0}s";

        var minutesRemaining = (int)Math.Ceiling(secondsRemaining / 60);
        if (minutesRemaining < 60) return $"ETA about {minutesRemaining:N0} min";

        var hours = minutesRemaining / 60;
        var minutes = minutesRemaining % 60;
        if (hours < 24)
            return minutes == 0 ? $"ETA about {hours} hr" : $"ETA about {hours} hr {minutes} min";

        var days = hours / 24;
        hours %= 24;
        return hours == 0 ? $"ETA about {days} days" : $"ETA about {days} days {hours} hr";
    }
    private void SetStatus(string value)
    {
        lock (gate) status = value;
    }

    public void Dispose() => Stop();
}

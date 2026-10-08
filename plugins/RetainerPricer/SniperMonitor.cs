using System.Diagnostics;

namespace RetainerPricer;

internal sealed class SniperMonitor : IDisposable
{
    private const int MaximumDeals = 250;
    private const int MaximumPendingLiveListings = 50_000;

    private sealed class RunState
    {
        public Dictionary<(uint ItemId, bool IsHq), SniperQualityBaseline> Baselines { get; } = [];
        public HashSet<uint> CompletedHistoryItemIds { get; } = [];
        public Dictionary<string, UniversalisListing> PendingLiveListings { get; } = new(StringComparer.Ordinal);
        public int DroppedPendingListings { get; set; }
    }

    private sealed record HistoryScanSummary(int EligibleItems, IReadOnlyList<string> FailedBatches,
        string? FatalError = null);

    private readonly object gate = new();
    private readonly UniversalisClient universalis;
    private readonly PluginConfig config;
    private readonly IReadOnlyList<ItemChoice> marketableItems;
    private readonly IReadOnlyDictionary<uint, string> itemNames;
    private readonly IReadOnlyDictionary<uint, string> worldNames;
    private readonly List<SniperDeal> deals = [];
    private CancellationTokenSource? cancellation;
    private string status = "Sniper is stopped.";
    private bool isListening;
    private bool initialScanRunning;
    private string initialScanProgress = string.Empty;
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
    public bool IsInitialScanRunning { get { lock (gate) return initialScanRunning; } }
    public string Status { get { lock (gate) return status; } }
    public IReadOnlyList<SniperDeal> Deals { get { lock (gate) return deals.ToArray(); } }
    public int MarketableItemCount => marketableItems.Count;

    public void Start(MarketWorld? world, bool useDataCenter, bool useRegion)
    {
        if (world is null)
        {
            SetStatus("Log in to start Sniper.");
            return;
        }
        var watchedItems = marketableItems;
        CancellationTokenSource run;
        lock (gate)
        {
            if (cancellation is not null) return;
            cancellation = run = new CancellationTokenSource();
            deals.Clear();
            isListening = false;
            initialScanRunning = false;
            initialScanProgress = string.Empty;
            liveFeedStatus = "not connected";
            status = $"Preparing to scan {watchedItems.Count:N0} marketable items using the selected market scope…";
        }
        _ = Task.Run(() => RunAsync(world, useDataCenter, useRegion, watchedItems, Math.Clamp(config.SniperMinimumSales14Days, 1, 1_800),
            Math.Clamp(config.SniperThresholdFraction, 0.01, 1.0), Math.Clamp(config.SniperHistoryDays, 3, 14),
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

            var runState = new RunState();
            lock (gate)
            {
                initialScanRunning = true;
                initialScanProgress = "Initial scan is starting";
                liveFeedStatus = "connecting to Universalis live listings";
                RefreshInitialScanStatusAndGetLocked(runState);
            }

            // Start listening before the catalog scan completes. Live listings for unscanned
            // items are held until their history batch supplies a usable HQ/NQ baseline.
            var historyScanTask = ScanHistoryAsync(scope, watchedItems, world, watchedWorlds, minimumSales,
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
                            : BuildListeningStatus(scope, watchedWorlds.Count, watchedItems.Count, historyDays, minimumSales, historySummary!);
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
                                        historyDays, minimumSales, historySummary);
                            }
                            listings = await receiveTask.ConfigureAwait(false);
                        }
                        else
                        {
                            listings = await receiveTask.ConfigureAwait(false);
                        }
                        if (listings is null) break;
                        HandleLiveListings(listings, world, watchedWorlds, runState, threshold, minimumItemPrice);
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

    private async Task<HistoryScanSummary> ScanHistoryAsync(SniperMarketScope scope,
        IReadOnlyList<ItemChoice> watchedItems, MarketWorld world, IReadOnlySet<uint> watchedWorlds,
        int minimumSales, double threshold, int historyDays, int minimumItemPrice, RunState runState,
        CancellationToken cancellationToken)
    {
        var failed = new List<string>();
        var batchCount = (watchedItems.Count + UniversalisClient.SniperHistoryBatchSize - 1) /
                         UniversalisClient.SniperHistoryBatchSize;
        var scanStartedAt = Stopwatch.GetTimestamp();
        try
        {
            for (var index = 0; index < watchedItems.Count; index += UniversalisClient.SniperHistoryBatchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var batch = watchedItems.Skip(index).Take(UniversalisClient.SniperHistoryBatchSize).ToArray();
                var currentBatch = index / UniversalisClient.SniperHistoryBatchSize + 1;
                var completedBatches = currentBatch - 1;
                var eta = completedBatches > 0
                    ? FormatEta(Stopwatch.GetElapsedTime(scanStartedAt), completedBatches, batchCount - completedBatches)
                    : "Estimating ETA from the first batch";
                SetInitialScanProgress($"Initial scan · {index:N0}/{watchedItems.Count:N0} items processed · {historyDays}-day history batch {currentBatch} of {batchCount} ({batch.Length} items) · {eta}…", runState);

                var batchBaselines = new Dictionary<(uint ItemId, bool IsHq), SniperQualityBaseline>();
                try
                {
                    var batchSales = new Dictionary<uint, List<SniperSale>>();
                    foreach (var historyScope in scope.HistoryScopes)
                    {
                        var snapshots = await universalis.FetchSniperSalesBatchAsync(historyScope,
                                batch.Select(item => item.ItemId).ToArray(), historyDays, world.WorldId, cancellationToken)
                            .ConfigureAwait(false);
                        foreach (var snapshot in snapshots.Values)
                        {
                            if (!batchSales.TryGetValue(snapshot.ItemId, out var sales))
                                batchSales[snapshot.ItemId] = sales = [];
                            sales.AddRange(snapshot.Sales);
                        }
                    }
                    foreach (var (itemId, sales) in batchSales)
                    {
                        foreach (var qualitySales in sales.GroupBy(sale => sale.IsHq))
                        {
                            var orderedPrices = qualitySales.Select(sale => sale.PricePerUnit).OrderBy(price => price).ToArray();
                            if (orderedPrices.Length < minimumSales) continue;
                            var middle = orderedPrices.Length / 2;
                            var median = orderedPrices.Length % 2 == 0
                                ? (uint)(((ulong)orderedPrices[middle - 1] + orderedPrices[middle]) / 2)
                                : orderedPrices[middle];
                            batchBaselines[(itemId, qualitySales.Key)] = new SniperQualityBaseline(qualitySales.Key, orderedPrices.Length, median);
                        }
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
                    foreach (var itemId in batchItemIds)
                        runState.CompletedHistoryItemIds.Add(itemId);
                    foreach (var pending in runState.PendingLiveListings
                                 .Where(pair => batchItemIds.Contains(pair.Value.ItemId)).ToArray())
                    {
                        runState.PendingLiveListings.Remove(pending.Key);
                        ProcessLiveListingLocked(pending.Value, world, watchedWorlds, runState, threshold, minimumItemPrice);
                    }
                    var processedItems = Math.Min(index + batch.Length, watchedItems.Count);
                    initialScanProgress = $"Initial scan · {processedItems:N0}/{watchedItems.Count:N0} items processed · {historyDays}-day history batch {currentBatch} of {batchCount} complete";
                    RefreshInitialScanStatusAndGetLocked(runState);
                }
            }

            var eligible = runState.Baselines.Keys.Select(key => key.ItemId).Distinct().Count();
            var summary = new HistoryScanSummary(eligible, failed.ToArray());
            lock (gate)
            {
                initialScanRunning = false;
                status = isListening
                    ? BuildListeningStatus(scope, scope.WorldIds.Count, watchedItems.Count, historyDays, minimumSales, summary)
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
                initialScanRunning = false;
                status = isListening
                    ? BuildListeningStatus(scope, scope.WorldIds.Count, watchedItems.Count, historyDays, minimumSales, summary)
                    : $"Initial history scan stopped: {ex.Message}. {liveFeedStatus}.";
            }
            return summary;
        }
    }

    private void HandleLiveListings(IReadOnlyList<UniversalisListing> listings, MarketWorld world,
        IReadOnlySet<uint> watchedWorlds, RunState runState, double threshold, int minimumItemPrice)
    {
        lock (gate)
        {
            foreach (var listing in listings)
                ProcessLiveListingLocked(listing, world, watchedWorlds, runState, threshold, minimumItemPrice);
        }
    }

    private void ProcessLiveListingLocked(UniversalisListing listing, MarketWorld world,
        IReadOnlySet<uint> watchedWorlds, RunState runState, double threshold, int minimumItemPrice)
    {
        var eventKey = ListingEventKey(listing);
        if (listing.IsRemoved)
        {
            runState.PendingLiveListings.Remove(eventKey);
            if (listing.ListingId is { Length: > 0 } removedId)
                deals.RemoveAll(previous => previous.Key == $"{listing.WorldId}:{listing.ItemId}:{removedId}" ||
                    previous.IsOneGilAlert && previous.ItemId == listing.ItemId &&
                    previous.WorldId == listing.WorldId && previous.IsHq == listing.IsHq);
            else if (listing.PricePerUnit == 1)
                deals.RemoveAll(previous => previous.IsOneGilAlert && previous.ItemId == listing.ItemId &&
                    previous.WorldId == listing.WorldId && previous.IsHq == listing.IsHq);
            else
                deals.RemoveAll(previous => previous.ItemId == listing.ItemId && previous.WorldId == listing.WorldId &&
                    previous.IsHq == listing.IsHq && previous.PricePerUnit == listing.PricePerUnit && previous.Quantity == listing.Quantity);
            if (initialScanRunning) RefreshInitialScanStatusAndGetLocked(runState);
            return;
        }

        if (!watchedWorlds.Contains(listing.WorldId) || !itemNames.TryGetValue(listing.ItemId, out var name))
            return;

        var isOneGilAlert = listing.PricePerUnit == 1;
        SniperQualityBaseline? baseline = null;
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
        var dealKey = isOneGilAlert
            ? $"alert:{listing.WorldId}:{listing.ItemId}:{listing.IsHq}"
            : eventKey;
        var deal = new SniperDeal(dealKey, listing.ItemId, name, listing.WorldId, listingWorldName,
            listing.IsHq, listing.PricePerUnit, listing.Quantity, baseline?.MedianSalePrice ?? 0,
            baseline?.SaleCount ?? 0, DateTimeOffset.UtcNow, listing.ListingId, isOneGilAlert);
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
        var status = $"Listening on {scope.Label} across {watchedWorldCount:N0} world(s) · all {watchedItemCount:N0} marketable items scanned, {summary.EligibleItems:N0} with usable {historyDays}-day history · 1-gil alerts cover all marketable items. Purchases are manual.";
        status += " " + BuildHistorySummary(summary, watchedItemCount, minimumSales, historyDays);
        return status;
    }

    private static string BuildHistorySummary(HistoryScanSummary summary, int watchedItemCount,
        int minimumSales, int historyDays)
    {
        var details = new List<string>();
        if (summary.EligibleItems == 0 && watchedItemCount > 0)
            details.Add($"No item reached the minimum of {minimumSales} sales in {historyDays} days; ordinary deals need a usable baseline.");
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

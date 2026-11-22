using System.Diagnostics;

namespace RetainerPricer;

internal sealed record SkippedItem(string Name, string Reason, PriceCacheKey? LookupKey);

internal sealed class AutomationRunSummary
{
    private readonly long startedAt = Stopwatch.GetTimestamp();
    private long phaseStartedAt;
    private string? currentPhase;
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.Now;
    public TimeSpan Elapsed { get; private set; }
    public bool Finished { get; private set; }
    public string Outcome { get; private set; } = "Running";
    public string Detail { get; private set; } = "";
    public int CacheHits { get; set; }
    public int PriceChecks { get; set; }
    public int Listed { get; set; }
    public int Repriced { get; set; }
    public int Vendored { get; set; }
    public int AlreadyPriced { get; set; }
    public Dictionary<string, TimeSpan> Phases { get; } = [];
    public Dictionary<string, int> SkipReasons { get; } = [];
    public List<SkippedItem> SkippedItems { get; } = [];

    public void BeginPhase(string name)
    {
        if (Finished) return;
        EndPhase();
        currentPhase = name;
        phaseStartedAt = Stopwatch.GetTimestamp();
    }

    private void EndPhase()
    {
        if (currentPhase is null) return;
        Phases[currentPhase] = Phases.GetValueOrDefault(currentPhase) + Stopwatch.GetElapsedTime(phaseStartedAt);
        currentPhase = null;
    }

    public void Skip(string reason, string? item = null, int count = 1)
    {
        if (Finished || count <= 0) return;
        SkipReasons[reason] = SkipReasons.GetValueOrDefault(reason) + count;
        if (item is not null) SkippedItems.Add(new(item, reason, null));
    }

    public void SkipLookup(PriceCacheKey key, string reason, string item)
    {
        if (Finished) return;
        SkipReasons[reason] = SkipReasons.GetValueOrDefault(reason) + 1;
        SkippedItems.Add(new(item, reason, key));
    }

    public void ResolveLookup(PriceCacheKey key)
    {
        if (Finished) return;
        for (var index = SkippedItems.Count - 1; index >= 0; index--)
        {
            var skipped = SkippedItems[index];
            if (skipped.LookupKey != key) continue;

            SkippedItems.RemoveAt(index);
            var remaining = SkipReasons[skipped.Reason] - 1;
            if (remaining == 0) SkipReasons.Remove(skipped.Reason);
            else SkipReasons[skipped.Reason] = remaining;
        }
    }

    public void Complete(string outcome, string detail)
    {
        if (Finished) return;
        EndPhase();
        Elapsed = Stopwatch.GetElapsedTime(startedAt);
        Outcome = outcome;
        Detail = detail;
        Finished = true;
    }
}

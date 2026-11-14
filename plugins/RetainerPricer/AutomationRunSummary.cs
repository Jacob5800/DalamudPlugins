using System.Diagnostics;

namespace RetainerPricer;

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
    public List<string> SkippedItems { get; } = [];

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
        if (item is not null) SkippedItems.Add($"{item} — {reason}");
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

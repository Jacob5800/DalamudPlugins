namespace RetainerPricer;

/// <summary>Attempts every cleanup step, preserving failures for the caller to report.</summary>
internal sealed class ShutdownSequence
{
    private int started;
    public IReadOnlyList<(string Step, Exception Error)> Run(params (string Name, Action Cleanup)[] steps)
    {
        if (Interlocked.Exchange(ref started, 1) != 0) return [];
        var failures = new List<(string, Exception)>();
        foreach (var (name, cleanup) in steps)
        {
            try { cleanup(); }
            catch (Exception error) { failures.Add((name, error)); }
        }
        return failures;
    }
}

namespace RetainerPricer;

// Retry only the observed transient market rejection, before any price is submitted.
internal sealed class MarketComparisonRetry
{
    private DateTimeOffset retryAt;
    public int Attempts { get; private set; }
    public bool IsReady(DateTimeOffset now) => now >= retryAt;
    public bool TrySchedule(int error, DateTimeOffset now)
    {
        if (error != 0x70000003 || Attempts >= 2) return false;
        Attempts++;
        retryAt = now.AddMilliseconds(500);
        return true;
    }
    public void Reset() { Attempts = 0; retryAt = default; }
}

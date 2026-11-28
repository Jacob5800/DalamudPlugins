namespace RetainerPricer;

// Retry only the observed transient market rejection, before any price is submitted.
internal sealed class MarketComparisonRetry
{
    public const int MaxAttempts = 3;

    private DateTimeOffset retryAt;
    public int Attempts { get; private set; }
    public int DelayMilliseconds { get; private set; }
    public bool IsReady(DateTimeOffset now) => now >= retryAt;
    public bool TrySchedule(int error, DateTimeOffset now)
    {
        if (error != 0x70000003 || Attempts >= MaxAttempts) return false;
        Attempts++;
        DelayMilliseconds = 500 << (Attempts - 1);
        retryAt = now.AddMilliseconds(DelayMilliseconds);
        return true;
    }
    public void Reset()
    {
        Attempts = 0;
        DelayMilliseconds = 0;
        retryAt = default;
    }
}

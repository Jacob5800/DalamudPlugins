namespace TataruBrawler;

// Transient send failures may happen across a phase/input-field transition.
// Manual stop, unsupported input and results are never recovery candidates.
public sealed class AutomaticRecovery
{
    private long readySince = -1, retryAt;
    public void Reset() { readySince = -1; retryAt = 0; }
    public bool ShouldResume(long now, bool enabled, bool inDuty, bool running,
        string status, bool ready, bool manualPause, bool completed)
    {
        if (!enabled || !inDuty || running || status != "SendFailed" || !ready || manualPause || completed)
        { readySince = -1; return false; }
        if (readySince < 0) readySince = now;
        if (now - readySince < 2000 || now < retryAt) return false;
        readySince = -1;
        retryAt = now + 5000;
        return true;
    }
}

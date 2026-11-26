namespace RetainerPricer;

// Re-requests a trip teleport that was interrupted. The wait restarts while the character is casting or
// loading, so a teleport that is still in progress is never requested again.
internal sealed class TeleportRetry
{
    public const int MaxAttempts = 3;
    private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(5);
    private DateTimeOffset lastActivity;
    public int Attempts { get; private set; }

    public void Reset() { Attempts = 0; lastActivity = default; }
    public void Busy(DateTimeOffset now) => lastActivity = now;
    public bool Waiting(DateTimeOffset now) => Attempts > 0 && now - lastActivity < Quiet;
    public bool Exhausted => Attempts >= MaxAttempts;
    public void Requested(DateTimeOffset now) { Attempts++; lastActivity = now; }
}

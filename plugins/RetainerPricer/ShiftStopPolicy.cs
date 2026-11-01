namespace RetainerPricer;

internal static class ShiftStopPolicy
{
    internal static bool ShouldStop(bool enabled, bool shiftHeld, bool atBell, bool sequenceRunning, bool ventureRunning)
        => enabled && shiftHeld && atBell && (sequenceRunning || ventureRunning);
}

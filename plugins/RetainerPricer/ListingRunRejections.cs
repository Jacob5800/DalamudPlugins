namespace RetainerPricer;

internal sealed class ListingRunRejections
{
    private readonly HashSet<(uint ItemId, bool IsHq)> rejected = [];
    internal bool Contains(uint itemId, bool isHq) => rejected.Contains((itemId, isHq));
    internal void Record(uint itemId, bool isHq, PriceProposal proposal)
    {
        if (!proposal.CanApply && proposal.Rejection is PriceRejection.CannotUndercutOneGil or PriceRejection.BelowMinimum)
            rejected.Add((itemId, isHq));
    }
    internal void Clear() => rejected.Clear();

    // What the player declined stays declined for the whole run, across retainers and across its phases.
    private readonly HashSet<(uint ItemId, bool IsHq)> declined = [];
    internal bool Declined(uint itemId, bool isHq) => declined.Contains((itemId, isHq));
    internal void Decline(uint itemId, bool isHq) => declined.Add((itemId, isHq));
    internal void ForgetDeclined() => declined.Clear();
}

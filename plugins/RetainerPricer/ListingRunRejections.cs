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
}

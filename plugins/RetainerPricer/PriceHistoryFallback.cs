namespace RetainerPricer;

internal static class PriceHistoryFallback
{
    public static bool ShouldFetch(PriceSnapshot live, uint itemId, uint worldId, bool isHq,
        IReadOnlySet<ulong> ownIds, DateTimeOffset now) => live.Source == PriceSource.Local &&
        PriceCalculator.Calculate(live, itemId, worldId, isHq, ownIds, 1, now, null,
            requireRecentSale: false, allowSalesFallback: true).Rejection == PriceRejection.InsufficientHistory;

    public static PriceSnapshot Attach(PriceSnapshot live, PriceSnapshot history)
    {
        if (live.Source != PriceSource.Local || !live.IsComplete || history.Source != PriceSource.Universalis ||
            !history.IsComplete || live.ItemId != history.ItemId || live.WorldId != history.WorldId ||
            history.DataCenterName is not null || history.RegionName is not null)
            throw new InvalidOperationException("History does not match the live item and home world.");
        // Preserve the complete live listings; remote listings must never replace a live empty market.
        return live with { Sales = history.Sales ?? [], MostRecentSaleAt = history.MostRecentSaleAt,
            WasCached = history.WasCached, RetrievedAt = history.RetrievedAt };
    }
}

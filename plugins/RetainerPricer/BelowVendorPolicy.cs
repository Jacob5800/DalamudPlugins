namespace RetainerPricer;

internal static class BelowVendorPolicy
{
    // Highest city rate; a reduced 3% rate only makes the check slightly cautious.
    private const ulong MarketTaxPercent = 5;

    internal static uint NetAfterTax(uint price) => (uint)(price * (100 - MarketTaxPercent) / 100);

    // A sale that nets no more than the NPC pays is not worth a market slot.
    internal static bool RequiresReview(bool existing, uint current, uint proposed, uint vendor)
        => vendor > 0 && NetAfterTax(proposed) <= vendor && (!existing || proposed < current);
}

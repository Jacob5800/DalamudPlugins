namespace RetainerPricer;

internal sealed class LargeStackListingPlan(uint perListing, uint total)
{
    internal uint PerListing { get; } = Math.Clamp(perListing, 1u, 999u);
    internal uint Remaining { get; private set; } = total;
    internal uint Next(uint carried) => Math.Min(carried, Math.Min(PerListing, Remaining));
    internal void Confirm(uint quantity)
    {
        if (quantity > Remaining) throw new InvalidOperationException("Confirmed listing exceeds the approved quantity.");
        Remaining -= quantity;
    }
}

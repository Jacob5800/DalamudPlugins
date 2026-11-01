namespace RetainerPricer;

internal static class BelowVendorPolicy
{
    internal static bool RequiresReview(bool existing, uint current, uint proposed, uint vendor)
        => vendor > 0 && proposed < vendor && (!existing || proposed < current);
}

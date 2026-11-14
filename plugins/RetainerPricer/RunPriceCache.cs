namespace RetainerPricer;

internal readonly record struct PriceCacheKey(ulong ContentId, uint WorldId, uint ItemId,
    bool IsHq, PriceSource Source, string Scope);

internal sealed class RunPriceCache
{
    private readonly Dictionary<PriceCacheKey, PriceSnapshot> quotes = [];

    public bool TryGet(PriceCacheKey key, DateTimeOffset now, TimeSpan? maximumAge, out PriceSnapshot snapshot)
    {
        if (quotes.TryGetValue(key, out snapshot!))
        {
            if (snapshot.ObservedAt <= now && (maximumAge is null || now - snapshot.ObservedAt <= maximumAge))
                return true;
            quotes.Remove(key);
        }
        snapshot = null!;
        return false;
    }

    public void Remember(PriceCacheKey key, PriceSnapshot snapshot)
    {
        if (snapshot.IsComplete && snapshot.ItemId == key.ItemId && snapshot.WorldId == key.WorldId && snapshot.Source == key.Source)
            quotes[key] = snapshot;
    }

    public void Remove(PriceCacheKey key) => quotes.Remove(key);
    public void Clear() => quotes.Clear();
}

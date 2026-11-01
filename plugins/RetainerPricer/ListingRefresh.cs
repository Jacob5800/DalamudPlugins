namespace RetainerPricer;

internal static class ListingRefresh
{
    internal static bool TryRemap<T, TKey>(IReadOnlyList<T> pending, IReadOnlyList<T> fresh, Func<T, TKey> key, out List<T> replacements)
    {
        replacements = [];
        var available = fresh.ToList();
        foreach (var old in pending)
        {
            var index = available.FindIndex(x => EqualityComparer<TKey>.Default.Equals(key(x), key(old)));
            if (index < 0) { replacements.Clear(); return false; }
            replacements.Add(available[index]);
            available.RemoveAt(index);
        }
        return true;
    }
}

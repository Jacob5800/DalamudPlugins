using System.Collections.Concurrent;

namespace RetainerPricer;

/// <summary>The sale history the game itself receives when a market comparison is opened.</summary>
internal sealed class GameSaleHistory
{
    private readonly ConcurrentDictionary<uint, (DateTimeOffset At, IReadOnlyList<MarketSale> Sales)> byItem = new();

    public void Record(uint itemId, IReadOnlyList<MarketSale> sales, DateTimeOffset now)
    {
        if (itemId != 0) byItem[itemId] = (now, sales);
    }

    /// <summary>Only history that arrived for the current request counts.</summary>
    public bool TryGet(uint itemId, DateTimeOffset since, out IReadOnlyList<MarketSale> sales)
    {
        if (byItem.TryGetValue(itemId, out var entry) && entry.At >= since) { sales = entry.Sales; return true; }
        sales = [];
        return false;
    }

    public static PriceSnapshot Attach(PriceSnapshot live, IReadOnlyList<MarketSale> sales) =>
        live with { Sales = sales, MostRecentSaleAt = sales.Count == 0 ? null : sales.Max(sale => sale.SoldAt) };
}
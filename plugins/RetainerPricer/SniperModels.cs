namespace RetainerPricer;

public sealed record SniperQualityBaseline(bool IsHq, int SaleCount, uint MedianSalePrice,
    int HistoryDays = 7, DateTimeOffset? HistoryFetchedAt = null, DateTimeOffset? LastSaleAt = null, double? SampleCoverageDays = null, bool HistoryWasCapped = false);

internal enum SniperHistoryScopeKind { World, DataCenter, Region }

internal sealed record SniperHistoryScope(string Name, SniperHistoryScopeKind Kind);

internal sealed record SniperMarketScope(string Label, IReadOnlyList<SniperHistoryScope> HistoryScopes,
    IReadOnlyList<uint> WorldIds, IReadOnlyDictionary<uint, string> WorldDataCenterNames);

public sealed record SniperSale(uint PricePerUnit, bool IsHq, DateTimeOffset? SoldAt = null);

public sealed record SniperSalesSnapshot(uint ItemId, uint WorldId, DateTimeOffset RetrievedAt,
    IReadOnlyList<SniperQualityBaseline> Qualities, IReadOnlyList<SniperSale> Sales);

public sealed record SniperDeal(string Key, uint ItemId, string ItemName, uint WorldId, string WorldName,
    bool IsHq, uint PricePerUnit, uint Quantity, uint MedianSalePrice, int SalesInHistoryWindow,
    DateTimeOffset DetectedAt, string? ListingId, bool IsOneGilAlert = false,
    int HistoryDays = 7, DateTimeOffset? HistoryFetchedAt = null, DateTimeOffset? LastSaleAt = null,
    string? DataCenterName = null, double? SampleCoverageDays = null, bool HistoryWasCapped = false)
{
    public decimal DiscountPercent => MedianSalePrice > 0 ? (1m - (decimal)PricePerUnit / MedianSalePrice) * 100m : 0;
    public bool HasSalesPerDayEstimate => HistoryDays > 0 && !HistoryWasCapped ||
        SalesInHistoryWindow >= 2 && SampleCoverageDays is > 0;
    public decimal SalesPerDay => !HasSalesPerDayEstimate ? 0 : (decimal)SalesInHistoryWindow /
        (decimal)((HistoryDays == 0 || HistoryWasCapped) ? SampleCoverageDays!.Value : HistoryDays);
    public decimal EstimatedUnitCost(double purchaseFee) => PricePerUnit * (1m + (decimal)purchaseFee / 100m);
    public decimal EstimatedUnitSaving(double purchaseFee) => MedianSalePrice - EstimatedUnitCost(purchaseFee);
    public decimal EstimatedUnitProfit(double resaleTax, double purchaseFee) =>
        MedianSalePrice * (1m - (decimal)resaleTax / 100m) - PricePerUnit * (1m + (decimal)purchaseFee / 100m);
}

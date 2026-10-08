using Dalamud.Configuration;

namespace RetainerPricer;

public sealed class PluginConfig : IPluginConfiguration
{
    public int Version { get; set; } = 14;
    public bool AutoPriceNewListings { get; set; } = true;
    public bool OpenWithRetainer { get; set; } = true;
    public bool ShowServerInfoBarButton { get; set; } = true;
    public PriceSource Source { get; set; } = PriceSource.Universalis;
    public int MaximumAgeMinutes { get; set; } = 15;
    public bool UseMaximumPriceAge { get; set; }
    public int UniversalisCacheMinutes { get; set; } = 5;
    public bool UseDataCenterPrices { get; set; }
    public bool UseRegionPrices { get; set; }
    public int MinimumPrice { get; set; } = 1;
    public PriceStrategy PriceStrategy { get; set; } = PriceStrategy.UndercutByOne;
    public bool RunVentures { get; set; }
    public bool AssignQuickExplorationWhenIdle { get; set; } = true;
    public bool RepeatCompletedVentures { get; set; } = true;
    public Dictionary<ulong, RetainerVentureJob> RetainerVentureJobOverrides { get; set; } = [];
    public Dictionary<ulong, uint> RetainerVentureTaskOverrides { get; set; } = [];
    public int AutoVendorPriceThreshold { get; set; } = 1;
    public List<uint> RetainerSaleWhitelistItemIds { get; set; } = [];
    public bool DontShowEmptyExceptionsWarningAgain { get; set; }
    public int PriceDropUnder10KPercent { get; set; } = 50;
    public int PriceDrop10KTo999KPercent { get; set; } = 25;
    public int PriceDrop1MTo9999KPercent { get; set; } = 10;
    public int PriceDrop10MPlusPercent { get; set; } = 5;
    public List<uint> ExcludedItemIds { get; set; } = [];
    public List<uint> NoRepriceItemIds { get; set; } = [];
    public Dictionary<uint, uint> BatchSaleQuantities { get; set; } = [];
    // Zero or missing means unlimited total quantity for that item during one listing run.
    public Dictionary<uint, uint> BatchSaleMaxQuantities { get; set; } = [];
    public Dictionary<ulong, RetainerListingCache> UniversalisRetainerListings { get; set; } = [];
    public List<uint> UniversalisPendingItemIds { get; set; } = [];
    public Dictionary<uint, DateTimeOffset> UniversalisLastBoardSearchAt { get; set; } = [];
    public double SniperThresholdFraction { get; set; } = 0.910;
    public int SniperMinimumSales14Days { get; set; } = 5;
    public int SniperHistoryDays { get; set; } = 7;
    public int SniperMinimumItemPrice { get; set; } = 1;
    public bool SniperUseDataCenterPrices { get; set; }
    public bool SniperUseRegionPrices { get; set; }

    public void Normalize()
    {
        if (!Enum.IsDefined(Source)) Source = PriceSource.Universalis;
        if (!Enum.IsDefined(PriceStrategy)) PriceStrategy = PriceStrategy.UndercutByOne;
        RetainerVentureJobOverrides = (RetainerVentureJobOverrides ?? [])
            .Where(pair => pair.Key != 0 && Enum.IsDefined(pair.Value) && pair.Value != RetainerVentureJob.Auto)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        RetainerVentureTaskOverrides = (RetainerVentureTaskOverrides ?? [])
            .Where(pair => pair.Key != 0 && pair.Value != 0)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        MaximumAgeMinutes = Math.Clamp(MaximumAgeMinutes, 1, 120);
        UniversalisCacheMinutes = Math.Clamp(UniversalisCacheMinutes, 0, 60);
        if (UseRegionPrices) UseDataCenterPrices = false;
        MinimumPrice = Math.Clamp(MinimumPrice, 1, 999_999_999);
        AutoVendorPriceThreshold = Math.Clamp(AutoVendorPriceThreshold, 1, 999_999_999);
        RetainerSaleWhitelistItemIds = (RetainerSaleWhitelistItemIds ?? []).Where(id => id != 0).Distinct().ToList();
        PriceDropUnder10KPercent = Math.Clamp(PriceDropUnder10KPercent, 1, 99);
        PriceDrop10KTo999KPercent = Math.Clamp(PriceDrop10KTo999KPercent, 1, 99);
        PriceDrop1MTo9999KPercent = Math.Clamp(PriceDrop1MTo9999KPercent, 1, 99);
        PriceDrop10MPlusPercent = Math.Clamp(PriceDrop10MPlusPercent, 1, 99);
        ExcludedItemIds = (ExcludedItemIds ?? []).Where(id => id != 0).Distinct().ToList();
        NoRepriceItemIds = (NoRepriceItemIds ?? []).Where(id => id != 0).Distinct().ToList();
        BatchSaleQuantities = (BatchSaleQuantities ?? []).Where(pair => pair.Key != 0)
            .ToDictionary(pair => pair.Key, pair => (uint)Math.Clamp((long)pair.Value, 1, 9_999));
        BatchSaleMaxQuantities = (BatchSaleMaxQuantities ?? []).Where(pair => pair.Key != 0 && BatchSaleQuantities.ContainsKey(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value == 0 ? 0 : (uint)Math.Clamp((long)pair.Value, 1, 999_999_999));
        UniversalisRetainerListings = (UniversalisRetainerListings ?? [])
            .Where(pair => pair.Key != 0 && pair.Value is not null && pair.Value.RetainerId == pair.Key)
            .ToDictionary(pair => pair.Key, pair => new RetainerListingCache
            {
                RetainerId = pair.Key,
                RetainerName = pair.Value.RetainerName ?? string.Empty,
                RefreshedAt = pair.Value.RefreshedAt,
                Listings = (pair.Value.Listings ?? []).Where(item => item.ItemId != 0 && item.Quantity != 0).ToList()
            });
        UniversalisPendingItemIds = (UniversalisPendingItemIds ?? []).Where(id => id != 0).Distinct().ToList();
        UniversalisLastBoardSearchAt = (UniversalisLastBoardSearchAt ?? []).Where(pair => pair.Key != 0)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        SniperThresholdFraction = double.IsFinite(SniperThresholdFraction)
            ? Math.Clamp(SniperThresholdFraction, 0.01, 1.0) : 0.910;
        SniperMinimumSales14Days = Math.Clamp(SniperMinimumSales14Days, 1, 1_800);
        SniperHistoryDays = Math.Clamp(SniperHistoryDays, 3, 14);
        SniperMinimumItemPrice = Math.Clamp(SniperMinimumItemPrice, 1, 999_999_999);
        if (SniperUseRegionPrices) SniperUseDataCenterPrices = false;
    }

    public int PriceDropReviewPercentFor(uint currentPrice) => currentPrice < 10_000
        ? PriceDropUnder10KPercent
        : currentPrice < 1_000_000
            ? PriceDrop10KTo999KPercent
            : currentPrice < 10_000_000
                ? PriceDrop1MTo9999KPercent
                : PriceDrop10MPlusPercent;
}

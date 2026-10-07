using System;
using System.Collections.Generic;

namespace RetainerPricer;

public enum PriceSource { Universalis, Local }
public enum PriceStrategy { MatchLowest, UndercutByOne }
public enum RetainerVentureJob { Auto = 0, Combat = 1, Miner = 2, Botanist = 3, Fisher = 4 }

public static class RetainerVentureJobExtensions
{
    public static RetainerVentureJob DetectFromClassJobId(byte classJobId) => classJobId switch
    {
        16 => RetainerVentureJob.Miner,
        17 => RetainerVentureJob.Botanist,
        18 => RetainerVentureJob.Fisher,
        >= 1 and <= 7 => RetainerVentureJob.Combat,
        >= 19 => RetainerVentureJob.Combat,
        _ => RetainerVentureJob.Auto
    };

    public static string DisplayName(this RetainerVentureJob job) => job switch
    {
        RetainerVentureJob.Combat => "Combat",
        RetainerVentureJob.Miner => "Miner",
        RetainerVentureJob.Botanist => "Botanist",
        RetainerVentureJob.Fisher => "Fisher",
        _ => "Auto-detect"
    };
}

public sealed record ItemChoice(uint ItemId, string Name);
public sealed record CarriedItemCandidate(uint ItemId, string Name, bool IsHq, uint Quantity, int InventoryType, int Slot);
public sealed record MarketWorld(uint WorldId, string Name, string? DataCenterName = null);

public sealed record MarketListing(
    uint ItemId,
    bool IsHq,
    uint PricePerUnit,
    uint Quantity,
    ulong RetainerId,
    bool OnMannequin = false,
    uint WorldId = 0,
    string? WorldName = null,
    DateTimeOffset? ReviewedAt = null);

public sealed record MarketSale(bool IsHq, uint PricePerUnit, uint WorldId, string? WorldName, DateTimeOffset SoldAt);

public sealed class RetainerListingCache
{
    public ulong RetainerId { get; set; }
    public string RetainerName { get; set; } = string.Empty;
    public DateTimeOffset RefreshedAt { get; set; }
    public List<RetainerListingCacheEntry> Listings { get; set; } = [];
}

public sealed record RetainerListingCacheEntry(uint ItemId, bool IsHq, uint Quantity, uint Price);

public sealed record PriceSnapshot(
    uint ItemId,
    uint WorldId,
    PriceSource Source,
    DateTimeOffset ObservedAt,
    IReadOnlyList<MarketListing> Listings,
    bool IsComplete = true,
    DateTimeOffset? MostRecentSaleAt = null,
    DateTimeOffset? RetrievedAt = null,
    bool WasCached = false,
    string? DataCenterName = null,
    string? RegionName = null,
    IReadOnlyList<MarketSale>? Sales = null);

public sealed record PriceProposal(uint LowestPrice, uint SuggestedPrice, int MatchingListings, string? Error)
{
    public bool CanApply => Error is null;
}

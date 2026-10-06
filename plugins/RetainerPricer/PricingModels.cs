using System;
using System.Collections.Generic;

namespace RetainerPricer;

public enum PriceSource { Universalis, Local }

public sealed record ItemChoice(uint ItemId, string Name);
public sealed record CarriedItemCandidate(uint ItemId, string Name, bool IsHq, uint Quantity, int InventoryType, int Slot);
public sealed record MarketWorld(uint WorldId, string Name, string? DataCenterName = null);

public sealed record MarketListing(
    uint ItemId,
    bool IsHq,
    uint PricePerUnit,
    uint Quantity,
    ulong RetainerId,
    bool OnMannequin = false);

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
    string? RegionName = null);

public sealed record PriceProposal(uint LowestPrice, uint SuggestedPrice, int MatchingListings, string? Error)
{
    public bool CanApply => Error is null;
}

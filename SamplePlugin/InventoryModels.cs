using System;

namespace SamplePlugin;

[Serializable]
public class InventoryItem
{
    public string InstanceId { get; set; } = Guid.NewGuid().ToString("N");
    public string DropId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DropRarity Rarity { get; set; }
    public int BaseValue { get; set; }
    public DateTime AcquiredAtUtc { get; set; } = DateTime.UtcNow;
}

[Serializable]
public class MarketListing
{
    public string ListingId { get; set; } = Guid.NewGuid().ToString("N");
    public string InventoryItemId { get; set; } = string.Empty;
    public string DropId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DropRarity Rarity { get; set; }
    public int AskPrice { get; set; }
    public int LastFairPrice { get; set; }
    public float LastSaleChance { get; set; }
    public int ReviewCount { get; set; }
    public DateTime ListedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime NextReviewAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(5);
}

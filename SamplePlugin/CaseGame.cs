using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SamplePlugin;

public enum DropRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary,
    Relic
}

public sealed record DropDefinition(
    string Id,
    string Name,
    string Category,
    DropRarity Rarity,
    int Weight,
    int BaseValue,
    float BaseDemand,
    float Volatility,
    int MarketSeed);

public sealed record CaseDefinition(string Id, string Name, IReadOnlyList<DropDefinition> Drops)
{
    public int TotalWeight => Drops.Sum(drop => drop.Weight);
}

public sealed record MarketSnapshot(int FairPrice, float SaleChance);
public sealed record ShopOffer(string Id, string Name, int Cost, int Keys, int Crates, string Description);

public static class CaseGame
{
    public const string DefaultCaseId = "duty-crate";

    public static readonly CaseDefinition DutyCrate = new(
        DefaultCaseId,
        "Duty Recovery Crate",
        [
            new("fast-blade", "Fast Blade", "Starter GCD", DropRarity.Common, 2200, 30, 0.54f, 0.10f, 1),
            new("split-shot", "Split Shot", "Starter GCD", DropRarity.Common, 2200, 32, 0.54f, 0.10f, 2),
            new("stone", "Stone", "Starter GCD", DropRarity.Common, 2200, 34, 0.53f, 0.11f, 3),
            new("ruin", "Ruin", "Starter GCD", DropRarity.Common, 2100, 36, 0.53f, 0.11f, 4),
            new("hakaze", "Hakaze", "Starter GCD", DropRarity.Common, 2100, 38, 0.53f, 0.10f, 5),
            new("fell-cleave", "Fell Cleave", "Level 60 GCD", DropRarity.Uncommon, 1400, 95, 0.42f, 0.13f, 11),
            new("holy-spirit", "Holy Spirit", "Level 60 GCD", DropRarity.Uncommon, 1350, 100, 0.41f, 0.13f, 12),
            new("bloodspiller", "Bloodspiller", "Level 60 GCD", DropRarity.Uncommon, 1300, 110, 0.40f, 0.14f, 13),
            new("reprisal", "Reprisal", "Role Action", DropRarity.Uncommon, 1250, 120, 0.39f, 0.12f, 14),
            new("swiftcast", "Swiftcast", "Role Action", DropRarity.Uncommon, 1200, 125, 0.39f, 0.12f, 15),
            new("addle", "Addle", "Role Action", DropRarity.Uncommon, 1200, 128, 0.39f, 0.12f, 16),
            new("drill", "Drill", "Burst GCD", DropRarity.Rare, 850, 240, 0.31f, 0.15f, 21),
            new("glare-iv", "Glare IV", "Burst GCD", DropRarity.Rare, 820, 250, 0.30f, 0.15f, 22),
            new("xenoglossy", "Xenoglossy", "Burst GCD", DropRarity.Rare, 780, 275, 0.29f, 0.16f, 23),
            new("double-down", "Double Down", "Big Hit", DropRarity.Epic, 420, 620, 0.20f, 0.18f, 31),
            new("afflatus-misery", "Afflatus Misery", "Big Hit", DropRarity.Epic, 410, 650, 0.19f, 0.18f, 32),
            new("phantom-rush", "Phantom Rush", "Big Hit", DropRarity.Epic, 360, 710, 0.18f, 0.19f, 33),
            new("technical-step", "Technical Step", "Showpiece", DropRarity.Legendary, 150, 1550, 0.11f, 0.22f, 41),
            new("resolution", "Resolution", "Showpiece", DropRarity.Legendary, 145, 1620, 0.10f, 0.22f, 42),
            new("starfall-dance", "Starfall Dance", "Showpiece", DropRarity.Legendary, 120, 1880, 0.09f, 0.23f, 43),
            new("summon-bahamut", "Summon Bahamut", "Relic Hit", DropRarity.Relic, 28, 5200, 0.045f, 0.27f, 51),
            new("pictomancy-raid-burst", "Star Prism", "Relic Hit", DropRarity.Relic, 25, 5600, 0.042f, 0.28f, 52)
        ]);

    public static readonly IReadOnlyList<CaseDefinition> Cases = [DutyCrate];
    public static readonly IReadOnlyList<ShopOffer> ShopOffers =
    [
        new("key-single", "Raid Night Key", 140, 1, 0, "A key, carved from the Drill of a Machinist."),
        new("crate-single", "Moogle Mog Crate", 220, 0, 1, "Mog!"),
        new("runner-bundle", "Bee My Combo", 340, 1, 1, "A key!?!?!? ANNNNNNND A CRATE??!?!?!?"),
        new("wipe-night", "Wipe Night Pack", 980, 3, 2, "The opposite, of Raid Night"),
        new("degenerate-box", "Recovered Island Supply", 1850, 4, 4, "The label reads... "From Aloalo Island". ")
    ];

    public static CaseDefinition GetCase(string caseId) =>
        Cases.FirstOrDefault(definition => definition.Id == caseId) ?? DutyCrate;

    public static DropDefinition GetDrop(string dropId) =>
        DutyCrate.Drops.First(drop => drop.Id == dropId);

    public static DropDefinition RollDrop(CaseDefinition caseDefinition, Random random)
    {
        var roll = random.Next(caseDefinition.TotalWeight);
        var accumulated = 0;

        foreach (var drop in caseDefinition.Drops)
        {
            accumulated += drop.Weight;
            if (roll < accumulated)
                return drop;
        }

        return caseDefinition.Drops[^1];
    }

        //hi guys im stola
    public static List<DropDefinition> BuildReel(CaseDefinition caseDefinition, DropDefinition winningDrop, Random random, int length = 33, int winningIndex = 26)
    {
        var items = new List<DropDefinition>(length);
        for (var i = 0; i < length; i++)
            items.Add(i == winningIndex ? winningDrop : RollDrop(caseDefinition, random));

        return items;
    }

    public static MarketSnapshot EvaluateListing(DropDefinition drop, MarketListing listing, DateTime nowUtc)
    {
        var cycle = nowUtc.Ticks / TimeSpan.FromMinutes(5).Ticks;
        var wave = Math.Sin((cycle + drop.MarketSeed) * 0.73d);
        var volatilityMultiplier = 1.0d + wave * drop.Volatility;
        var fairPrice = Math.Max(10, (int)Math.Round(drop.BaseValue * volatilityMultiplier));

        var pricingFactor = Math.Clamp((double)fairPrice / Math.Max(1, listing.AskPrice), 0.30d, 1.70d);
        var ageBonus = Math.Min(0.28d, listing.ReviewCount * 0.045d);

        var rarityPressure = drop.Rarity switch
        {
            DropRarity.Common => 1.15d,
            DropRarity.Uncommon => 1.02d,
            DropRarity.Rare => 0.86d,
            DropRarity.Epic => 0.68d,
            DropRarity.Legendary => 0.48d,
            DropRarity.Relic => 0.28d,
            _ => 1d
        };

        var saleChance = Math.Clamp(drop.BaseDemand * rarityPressure * pricingFactor + ageBonus, 0.03d, 0.96d);
        return new MarketSnapshot(fairPrice, (float)saleChance);
    }

    public static Vector4 GetRarityColor(DropRarity rarity) => rarity switch
    {
        DropRarity.Common => new Vector4(0.72f, 0.72f, 0.76f, 1f),
        DropRarity.Uncommon => new Vector4(0.41f, 0.73f, 0.98f, 1f),
        DropRarity.Rare => new Vector4(0.43f, 0.48f, 0.98f, 1f),
        DropRarity.Epic => new Vector4(0.70f, 0.33f, 0.89f, 1f),
        DropRarity.Legendary => new Vector4(0.92f, 0.33f, 0.22f, 1f),
        DropRarity.Relic => new Vector4(0.96f, 0.79f, 0.24f, 1f),
        _ => Vector4.One
    };

    public static string GetRarityLabel(DropRarity rarity) => rarity.ToString();
}

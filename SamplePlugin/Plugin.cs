using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SamplePlugin.Windows;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SamplePlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static IToastGui Toasts { get; private set; } = null!;

    private const string CommandName = "/gamba";
    private readonly Random random = new();
    private bool wasDead;
    private static readonly string[] MotdPool =
    [
        "good job",
        "gamba gamba gamba",
        "GOLD GOLD GOLD GOLD",
        "just one good gold and we stop",
        "knife out, trust",
        "blue gem angle",
        "surely this one is the hit",
        "market says hold, heart says open",
        "another duty, another degenerate spin",
        "today is absolutely the relic day"
    ];

    public Configuration Configuration { get; init; }
    public string MessageOfTheDay { get; private set; } = "good job";

    public readonly WindowSystem WindowSystem = new("GambaFantasyXIV");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.EnsureValidState();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "/gamba, /gamba reset"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        Framework.Update += OnFrameworkUpdate;
        DutyState.DutyCompleted += OnDutyCompleted;
        RollMessageOfTheDay();

        Log.Information($"===Loaded {PluginInterface.Manifest.Name}===");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        Framework.Update -= OnFrameworkUpdate;
        DutyState.DutyCompleted -= OnDutyCompleted;

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();
        CommandManager.RemoveHandler(CommandName);
    }

    public bool TryOpenCrate(out DropDefinition? reward, out List<DropDefinition> reel)
    {
        reward = null;
        reel = [];

        if (Configuration.Keys <= 0 || Configuration.Crates <= 0)
            return false;

        var crate = CaseGame.GetCase(Configuration.SelectedCaseId);
        reward = CaseGame.RollDrop(crate, random);
        reel = CaseGame.BuildReel(crate, reward, random);

        Configuration.Keys--;
        Configuration.Crates--;
        AppendLog("Started opening a crate.");
        Configuration.Save();
        return true;
    }

    public void FinalizeOpenedCrate(DropDefinition reward)
    {
        Configuration.CratesOpened++;
        Configuration.BiggestHit = Math.Max(Configuration.BiggestHit, reward.BaseValue);
        Configuration.Inventory.Insert(0, new InventoryItem
        {
            DropId = reward.Id,
            Name = reward.Name,
            Rarity = reward.Rarity,
            BaseValue = reward.BaseValue,
            AcquiredAtUtc = DateTime.UtcNow
        });

        AppendLog($"Opened crate: {reward.Name} [{CaseGame.GetRarityLabel(reward.Rarity)}]");
        Configuration.Save();
    }

    public bool TryCreateListing(string inventoryItemId, int askPrice, out string error)
    {
        error = string.Empty;
        askPrice = Math.Max(10, askPrice);

        var item = Configuration.Inventory.FirstOrDefault(entry => entry.InstanceId == inventoryItemId);
        if (item is null)
        {
            error = "Item no longer exists.";
            return false;
        }

        Configuration.Inventory.Remove(item);
        var listing = new MarketListing
        {
            InventoryItemId = item.InstanceId,
            DropId = item.DropId,
            Name = item.Name,
            Rarity = item.Rarity,
            AskPrice = askPrice,
            ListedAtUtc = DateTime.UtcNow,
            NextReviewAtUtc = DateTime.UtcNow.AddMinutes(5)
        };

        var snapshot = CaseGame.EvaluateListing(CaseGame.GetDrop(item.DropId), listing, DateTime.UtcNow);
        listing.LastFairPrice = snapshot.FairPrice;
        listing.LastSaleChance = snapshot.SaleChance;
        Configuration.Listings.Insert(0, listing);
        AppendLog($"Listed {item.Name} for {askPrice:N0} credits.");
        Configuration.Save();
        return true;
    }

    public bool TryBuyOffer(string offerId, out string error)
    {
        error = string.Empty;
        var offer = CaseGame.ShopOffers.FirstOrDefault(entry => entry.Id == offerId);
        if (offer is null)
        {
            error = "Offer not found.";
            return false;
        }

        if (Configuration.Credits < offer.Cost)
        {
            error = "Not enough credits.";
            return false;
        }

        Configuration.Credits -= offer.Cost;
        Configuration.Keys += offer.Keys;
        Configuration.Crates += offer.Crates;
        AppendLog($"Bought {offer.Name} for {offer.Cost:N0} credits.");
        Configuration.Save();
        return true;
    }

    public bool TryTradeUp(IReadOnlyCollection<string> inventoryItemIds, out string error, out InventoryItem? reward)
    {
        reward = null;
        error = string.Empty;

        if (inventoryItemIds.Count != 10)
        {
            error = "Trade-ups require exactly 10 items.";
            return false;
        }

        var selectedItems = Configuration.Inventory
            .Where(item => inventoryItemIds.Contains(item.InstanceId))
            .ToList();

        if (selectedItems.Count != 10)
        {
            error = "Some selected items are no longer in inventory.";
            return false;
        }

        var rarity = selectedItems[0].Rarity;
        if (selectedItems.Any(item => item.Rarity != rarity))
        {
            error = "All trade-up items must be the same rarity.";
            return false;
        }

        var nextRarity = rarity switch
        {
            DropRarity.Common => DropRarity.Uncommon,
            DropRarity.Uncommon => DropRarity.Rare,
            DropRarity.Rare => DropRarity.Epic,
            DropRarity.Epic => DropRarity.Legendary,
            DropRarity.Legendary => DropRarity.Relic,
            _ => (DropRarity?)null
        };

        if (nextRarity is null)
        {
            error = "This rarity cannot be traded up.";
            return false;
        }

        var pool = CaseGame.DutyCrate.Drops.Where(drop => drop.Rarity == nextRarity.Value).ToList();
        if (pool.Count == 0)
        {
            error = "No valid trade-up targets found.";
            return false;
        }

        var target = pool[random.Next(pool.Count)];
        foreach (var item in selectedItems)
            Configuration.Inventory.Remove(item);

        reward = new InventoryItem
        {
            DropId = target.Id,
            Name = target.Name,
            Rarity = target.Rarity,
            BaseValue = target.BaseValue,
            AcquiredAtUtc = DateTime.UtcNow
        };

        Configuration.Inventory.Insert(0, reward);
        Configuration.BiggestHit = Math.Max(Configuration.BiggestHit, reward.BaseValue);
        AppendLog($"Trade-up hit: {reward.Name} [{CaseGame.GetRarityLabel(reward.Rarity)}]");
        Configuration.Save();
        return true;
    }

    public void ProcessMarketListings(bool forceAll = false)
    {
        if (Configuration.Listings.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var soldListings = new List<MarketListing>();
        var processedAny = false;

        foreach (var listing in Configuration.Listings.ToList())
        {
            if (!forceAll && listing.NextReviewAtUtc > now)
                continue;

            processedAny = true;
            var snapshot = CaseGame.EvaluateListing(CaseGame.GetDrop(listing.DropId), listing, now);
            listing.LastFairPrice = snapshot.FairPrice;
            listing.LastSaleChance = snapshot.SaleChance;
            listing.ReviewCount++;

            if (random.NextDouble() <= snapshot.SaleChance)
            {
                Configuration.Credits += listing.AskPrice;
                soldListings.Add(listing);
                AppendLog($"Sold {listing.Name} for {listing.AskPrice:N0} credits.");
            }
            else
            {
                listing.NextReviewAtUtc = now.AddMinutes(5);
            }
        }

        if (soldListings.Count > 0)
        {
            foreach (var listing in soldListings)
                Configuration.Listings.Remove(listing);
        }

        if (processedAny)
            Configuration.Save();
    }

    public string GetMarketFormulaSummary(MarketListing listing)
    {
        return $"chance = clamp(baseDemand * rarityPressure * fairPrice/ask + ageBonus, 3%, 96%)";
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        ProcessMarketListings();

        var isDead = Condition[ConditionFlag.Unconscious];
        if (isDead && !wasDead)
        {
            AwardKey("Death");
            wasDead = true;
        }
        else if (!isDead && wasDead)
        {
            wasDead = false;
        }
    }

    private void OnDutyCompleted(object? sender, ushort e)
    {
        AwardCrate("Duty clear");
    }

    private void AwardKey(string reason)
    {
        Configuration.Keys++;
        AppendLog($"+1 Key ({reason})");
        Configuration.Save();
    }

    private void AwardCrate(string reason)
    {
        Configuration.Crates++;
        AppendLog($"+1 Crate ({reason})");
        Toasts.ShowNormal($"+1 Crate acquired ({reason})");
        Configuration.Save();
    }

    private void AppendLog(string message)
    {
        Configuration.ActivityLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        Configuration.ActivityLog = Configuration.ActivityLog.Take(24).ToList();
    }

    private void RollMessageOfTheDay()
    {
        MessageOfTheDay = MotdPool[random.Next(MotdPool.Length)];
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "key":
                AwardKey("Command");
                break;
            case "crate":
                AwardCrate("Command");
                break;
            case "tick":
                ProcessMarketListings(forceAll: true);
                break;
            case "reset":
                Configuration.ResetProgress();
                break;
            default:
                ToggleMainUi();
                break;
        }
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi()
    {
        var wasOpen = MainWindow.IsOpen;
        MainWindow.Toggle();
        if (!wasOpen && MainWindow.IsOpen)
            RollMessageOfTheDay();
    }
}

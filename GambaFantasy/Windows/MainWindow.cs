using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly Dictionary<string, int> askPrices = [];
    private readonly HashSet<string> selectedTradeItems = [];

    private bool spinActive;
    private DateTime spinStartedAt;
    private TimeSpan spinDuration = TimeSpan.FromSeconds(3.85);
    private List<DropDefinition> reelItems = [];
    private DropDefinition? pendingDrop;
    private DropDefinition? lastDrop;
    private readonly int winningIndex = 26;

    public MainWindow(Plugin plugin)
        : base("Gamba Fantasy XIV###GambaFantasyXIV", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(980, 640),
            MaximumSize = new Vector2(1800, 1200)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (spinActive && DateTime.UtcNow - spinStartedAt >= spinDuration)
            FinishSpin();

        DrawHeader();
        ImGui.Spacing();

        if (!spinActive)
        {
            if (plugin.Configuration.Keys <= 0 || plugin.Configuration.Crates <= 0)
                ImGui.BeginDisabled();

            if (ImGui.Button("Open Duty Recovery Crate", new Vector2(220, 0)))
                StartSpin();

            if (plugin.Configuration.Keys <= 0 || plugin.Configuration.Crates <= 0)
                ImGui.EndDisabled();
        }
        else
        {
            ImGui.Button("Opening...", new Vector2(220, 0));
        }

        ImGui.SameLine();
        if (ImGui.Button("Settings", new Vector2(120, 0)))
            plugin.ToggleConfigUi();

        ImGui.Spacing();
        DrawReel();
        ImGui.Spacing();

        if (ImGui.BeginTable("MainLayout", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Inventory", ImGuiTableColumnFlags.WidthStretch, 0.56f);
            ImGui.TableSetupColumn("Market", ImGuiTableColumnFlags.WidthStretch, 0.44f);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            DrawInventory();

            ImGui.TableNextColumn();
            DrawShop();
            ImGui.Spacing();
            DrawMarket();

            ImGui.EndTable();
        }

        ImGui.Spacing();
        DrawLog();
    }

    private void DrawHeader()
    {
        var cfg = plugin.Configuration;
        ImGui.TextColored(new Vector4(0.94f, 0.77f, 0.19f, 1f), "Gamba Fantasy XIV");
        ImGui.TextDisabled("Death -> key. Duty clear -> crate. Key + crate -> roll.");
        ImGui.TextColored(new Vector4(0.98f, 0.88f, 0.45f, 1f), $"MOTD: {plugin.MessageOfTheDay}");
        ImGui.Separator();

        if (ImGui.BeginTable("Stats", 5, ImGuiTableFlags.SizingStretchSame))
        {
            ImGui.TableNextRow();
            DrawStat("Keys", cfg.Keys.ToString("N0"));
            DrawStat("Crates", cfg.Crates.ToString("N0"));
            DrawStat("Credits", cfg.Credits.ToString("N0"));
            DrawStat("Inventory", cfg.Inventory.Count.ToString("N0"));
            DrawStat("Opened", cfg.CratesOpened.ToString("N0"));
            ImGui.EndTable();
        }
    }

    private static void DrawStat(string label, string value)
    {
        ImGui.TableNextColumn();
        ImGui.TextDisabled(label);
        ImGui.TextUnformatted(value);
    }

    private void DrawReel()
    {
        ImGui.BeginChild("Reel", new Vector2(0, 210), true);
        var crate = CaseGame.DutyCrate;

        ImGui.TextDisabled(spinActive ? "Rolling crate..." : "Duty Recovery Crate");
        ImGui.SameLine();
        ImGui.TextUnformatted("Possible top hits: Summon Bahamut, Star Prism, Technical Step");
        ImGui.Spacing();

        var visibleItems = spinActive || reelItems.Count > 0
            ? GetVisibleReelItems()
            : crate.Drops.OrderByDescending(drop => drop.BaseValue).Take(7).ToList();

        if (ImGui.BeginTable("ReelTable", visibleItems.Count, ImGuiTableFlags.SizingStretchSame))
        {
            foreach (var _ in visibleItems)
                ImGui.TableSetupColumn(string.Empty);

            ImGui.TableNextRow();
            for (var i = 0; i < visibleItems.Count; i++)
            {
                var drop = visibleItems[i];
                var isCenter = i == visibleItems.Count / 2;
                var color = CaseGame.GetRarityColor(drop.Rarity);

                ImGui.TableNextColumn();
                if (isCenter)
                    ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(color.X * 0.18f, color.Y * 0.18f, color.Z * 0.18f, 0.85f));

                ImGui.BeginChild($"Cell{i}", new Vector2(0, 130), true);
                ImGui.TextColored(color, CaseGame.GetRarityLabel(drop.Rarity));
                ImGui.Separator();
                ImGui.TextWrapped(drop.Name);
                ImGui.TextDisabled(drop.Category);
                ImGui.Spacing();
                ImGui.Text($"{drop.BaseValue:N0} credits");
                ImGui.EndChild();

                if (isCenter)
                    ImGui.PopStyleColor();
            }

            ImGui.EndTable();
        }

        if (lastDrop is not null && !spinActive)
        {
            ImGui.Spacing();
            ImGui.Text("Last hit:");
            ImGui.SameLine();
            ImGui.TextColored(CaseGame.GetRarityColor(lastDrop.Rarity), lastDrop.Name);
            ImGui.SameLine();
            ImGui.TextDisabled($"{lastDrop.BaseValue:N0} base");
        }
        else if (spinActive)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("Reward remains hidden until the reel resolves...");
        }

        ImGui.EndChild();
    }

    private List<DropDefinition> GetVisibleReelItems()
    {
        if (reelItems.Count == 0)
            return [];

        var currentIndex = winningIndex;
        if (spinActive)
        {
            var progress = (float)((DateTime.UtcNow - spinStartedAt).TotalMilliseconds / spinDuration.TotalMilliseconds);
            progress = Math.Clamp(progress, 0f, 1f);
            var eased = 1f - MathF.Pow(1f - progress, 3f);
            currentIndex = Math.Clamp((int)MathF.Floor(eased * winningIndex), 0, winningIndex);
        }

        var result = new List<DropDefinition>(7);
        for (var offset = -3; offset <= 3; offset++)
        {
            var index = Math.Clamp(currentIndex + offset, 0, reelItems.Count - 1);
            result.Add(reelItems[index]);
        }

        return result;
    }

    private void DrawInventory()
    {
        selectedTradeItems.RemoveWhere(id => plugin.Configuration.Inventory.All(item => item.InstanceId != id));

        ImGui.TextDisabled("Inventory");
        ImGui.Separator();

        if (plugin.Configuration.Inventory.Count == 0)
        {
            ImGui.TextDisabled("No drops yet. Die for keys. Clear content for crates.");
            return;
        }

        if (ImGui.BeginTable("InventoryTable", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Pick", ImGuiTableColumnFlags.WidthFixed, 36f);
            ImGui.TableSetupColumn("Ability");
            ImGui.TableSetupColumn("Tier");
            ImGui.TableSetupColumn("Base");
            ImGui.TableSetupColumn("Ask");
            ImGui.TableSetupColumn("Action");
            ImGui.TableHeadersRow();

            foreach (var item in plugin.Configuration.Inventory.ToList())
            {
                askPrices.TryAdd(item.InstanceId, item.BaseValue);

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                var selected = selectedTradeItems.Contains(item.InstanceId);
                if (ImGui.Checkbox($"##trade_{item.InstanceId}", ref selected))
                {
                    if (selected)
                        selectedTradeItems.Add(item.InstanceId);
                    else
                        selectedTradeItems.Remove(item.InstanceId);
                }

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(item.Name);
                ImGui.TextDisabled(CaseGame.GetDrop(item.DropId).Category);

                ImGui.TableNextColumn();
                ImGui.TextColored(CaseGame.GetRarityColor(item.Rarity), CaseGame.GetRarityLabel(item.Rarity));

                ImGui.TableNextColumn();
                ImGui.Text($"{item.BaseValue:N0}");

                ImGui.TableNextColumn();
                var ask = askPrices[item.InstanceId];
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputInt($"##ask_{item.InstanceId}", ref ask))
                    askPrices[item.InstanceId] = Math.Max(10, ask);

                ImGui.TableNextColumn();
                if (ImGui.Button($"List##{item.InstanceId}") &&
                    plugin.TryCreateListing(item.InstanceId, askPrices[item.InstanceId], out _))
                {
                    askPrices.Remove(item.InstanceId);
                }
            }

            ImGui.EndTable();
        }

        DrawTradeUpPanel();
    }

    private void DrawTradeUpPanel()
    {
        var selectedItems = plugin.Configuration.Inventory
            .Where(item => selectedTradeItems.Contains(item.InstanceId))
            .ToList();

        var validCount = selectedItems.Count == 10;
        var sameRarity = selectedItems.Count == 0 || selectedItems.All(item => item.Rarity == selectedItems[0].Rarity);
        var tradeableRarity = selectedItems.Count == 0 || selectedItems[0].Rarity is not DropRarity.Relic;

        ImGui.Spacing();
        ImGui.TextDisabled("Trade-Up");
        ImGui.Separator();
        ImGui.TextWrapped("Select exactly 10 items of the same rarity to convert them into 1 random item from the next rarity tier.");
        ImGui.Text($"Selected: {selectedItems.Count}/10");

        if (selectedItems.Count > 0)
        {
            ImGui.SameLine();
            ImGui.TextColored(CaseGame.GetRarityColor(selectedItems[0].Rarity), CaseGame.GetRarityLabel(selectedItems[0].Rarity));
        }

        var canTradeUp = validCount && sameRarity && tradeableRarity;
        if (!canTradeUp)
            ImGui.BeginDisabled();

        if (ImGui.Button("Trade Up 10 -> 1", new Vector2(-1, 0)) &&
            plugin.TryTradeUp(selectedTradeItems.ToList(), out _, out _))
        {
            selectedTradeItems.Clear();
        }

        if (!canTradeUp)
            ImGui.EndDisabled();

        if (!sameRarity)
            ImGui.TextDisabled("Selected items must be the same rarity.");
        else if (!tradeableRarity && selectedItems.Count > 0)
            ImGui.TextDisabled("Relic-tier items cannot be traded up further.");
        else if (!validCount && selectedItems.Count > 0)
            ImGui.TextDisabled("You need exactly 10 items selected.");
    }

    private void DrawShop()
    {
        ImGui.TextDisabled("Shop");
        ImGui.SameLine();
        ImGui.TextUnformatted("(buy more runs)");
        ImGui.Separator();

        foreach (var offer in CaseGame.ShopOffers)
        {
            if (plugin.Configuration.Credits < offer.Cost)
                ImGui.BeginDisabled();

            if (ImGui.Button($"{offer.Name}##{offer.Id}", new Vector2(-1, 0)))
                plugin.TryBuyOffer(offer.Id, out _);

            if (plugin.Configuration.Credits < offer.Cost)
                ImGui.EndDisabled();

            ImGui.TextDisabled($"{offer.Cost:N0} credits");
            ImGui.SameLine();
            ImGui.TextUnformatted($"+{offer.Keys} key / +{offer.Crates} crate");
            ImGui.TextWrapped(offer.Description);
            ImGui.Spacing();
        }
    }

    private void DrawMarket()
    {
        ImGui.TextDisabled("Market");
        ImGui.SameLine();
        ImGui.TextUnformatted("(5 min review cycle)");
        ImGui.Separator();

        if (plugin.Configuration.Listings.Count == 0)
        {
            ImGui.TextDisabled("No active listings.");
            return;
        }

        if (ImGui.BeginTable("MarketTable", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Listing");
            ImGui.TableSetupColumn("Ask/Fair");
            ImGui.TableSetupColumn("Sale Chance");
            ImGui.TableSetupColumn("Next Review");
            ImGui.TableHeadersRow();

            foreach (var listing in plugin.Configuration.Listings)
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(listing.Name);
                ImGui.TextColored(CaseGame.GetRarityColor(listing.Rarity), CaseGame.GetRarityLabel(listing.Rarity));

                ImGui.TableNextColumn();
                ImGui.Text($"{listing.AskPrice:N0} / {listing.LastFairPrice:N0}");

                ImGui.TableNextColumn();
                ImGui.Text($"{listing.LastSaleChance * 100f:0.#}%");

                ImGui.TableNextColumn();
                var timeRemaining = listing.NextReviewAtUtc - DateTime.UtcNow;
                if (timeRemaining < TimeSpan.Zero)
                    timeRemaining = TimeSpan.Zero;

                ImGui.Text($"{timeRemaining:mm\\:ss}");
            }

            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.TextWrapped("Formula: sale chance = clamp(baseDemand * rarityPressure * fairPrice/ask + ageBonus, 3%, 96%). Listing cheaper than fair value sells faster. Unsold items gain a small age bonus each review.");
    }

    private void DrawLog()
    {
        ImGui.TextDisabled("Activity");
        ImGui.Separator();

        ImGui.BeginChild("Log", new Vector2(0, 120), true);
        if (plugin.Configuration.ActivityLog.Count == 0)
        {
            ImGui.TextDisabled("No events yet.");
        }
        else
        {
            foreach (var entry in plugin.Configuration.ActivityLog)
                ImGui.BulletText(entry);
        }

        ImGui.EndChild();
    }

    private void StartSpin()
    {
        if (!plugin.TryOpenCrate(out var reward, out var reel) || reward is null)
            return;

        pendingDrop = reward;
        reelItems = reel;
        spinDuration = plugin.Configuration.InstantOpen ? TimeSpan.Zero : TimeSpan.FromSeconds(3.85);
        spinStartedAt = DateTime.UtcNow;
        spinActive = true;

        if (plugin.Configuration.InstantOpen)
            FinishSpin();
    }

    private void FinishSpin()
    {
        if (!spinActive || pendingDrop is null)
            return;

        spinActive = false;
        lastDrop = pendingDrop;
        plugin.FinalizeOpenedCrate(lastDrop);
        pendingDrop = null;
    }
}

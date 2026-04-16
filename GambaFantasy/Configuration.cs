using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace SamplePlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public bool IsConfigWindowMovable { get; set; } = true;
    public bool InstantOpen { get; set; }
    public int Credits { get; set; }
    public int Keys { get; set; }
    public int Crates { get; set; }
    public int CratesOpened { get; set; }
    public int BiggestHit { get; set; }
    public string SelectedCaseId { get; set; } = CaseGame.DefaultCaseId;
    public List<InventoryItem> Inventory { get; set; } = [];
    public List<MarketListing> Listings { get; set; } = [];
    public List<string> ActivityLog { get; set; } = [];

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    public void EnsureValidState()
    {
        if (string.IsNullOrWhiteSpace(SelectedCaseId))
            SelectedCaseId = CaseGame.DefaultCaseId;

        Credits = Math.Max(0, Credits);
        Keys = Math.Max(0, Keys);
        Crates = Math.Max(0, Crates);
        Inventory ??= [];
        Listings ??= [];
        ActivityLog ??= [];
    }

    public void ResetProgress()
    {
        Credits = 0;
        Keys = 0;
        Crates = 0;
        CratesOpened = 0;
        BiggestHit = 0;
        Inventory = [];
        Listings = [];
        ActivityLog = [];
        Save();
    }
}

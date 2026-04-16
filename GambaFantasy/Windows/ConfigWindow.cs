using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SamplePlugin.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("Gamba Settings###GambaSettings")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(380, 170);
        SizeCondition = ImGuiCond.Always;
        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (configuration.IsConfigWindowMovable)
            Flags &= ~ImGuiWindowFlags.NoMove;
        else
            Flags |= ImGuiWindowFlags.NoMove;
    }

    public override void Draw()
    {
        var instantOpen = configuration.InstantOpen;
        if (ImGui.Checkbox("Instant open", ref instantOpen))
        {
            configuration.InstantOpen = instantOpen;
            configuration.Save();
        }

        var movable = configuration.IsConfigWindowMovable;
        if (ImGui.Checkbox("Movable Config Window", ref movable))
        {
            configuration.IsConfigWindowMovable = movable;
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.TextWrapped("Market listings are reviewed every 5 minutes. Sale chance scales with demand, rarity pressure, your ask price versus fair value, and how long the listing has been alive.");
        ImGui.Spacing();

        if (ImGui.Button("Reset Progress", new Vector2(-1, 0)))
            configuration.ResetProgress();
    }
}

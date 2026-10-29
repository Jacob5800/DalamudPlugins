using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Plugin;

namespace TataruBrawler.Interface;

// Adapted shell layout from Auto Keybound Brawler by XeldarAlz.
// See LICENSE-AGPL.md and NOTICE. Gameplay controls are supplied by our engine.
internal sealed class AutomationShell
{
    private readonly Palette colors = Palette.Grindless();
    private int page;
    private bool compact;
    private Vector2 expanded = new(960, 720);
    private bool restoreSize;
    private readonly string[] pages = { "Brawl", "Settings", "Log", "Changelog", "About" };
    private readonly FontAwesomeIcon[] icons = { FontAwesomeIcon.Keyboard, FontAwesomeIcon.SlidersH,
        FontAwesomeIcon.Terminal, FontAwesomeIcon.Newspaper, FontAwesomeIcon.InfoCircle };

    public void Draw(ref bool open, IDalamudPluginInterface pi, bool running, string status, string word,
        Action brawl, Action settings, Action log, Action changelog, Action about, Action playback)
    {
        float scale = ImGuiHelpers.GlobalScale;
        PushStyle();
        try
        {
            if (compact) ImGui.SetNextWindowSize(new Vector2(expanded.X, Layout.HeaderHeight) * scale, ImGuiCond.Always);
            else if (restoreSize) { ImGui.SetNextWindowSize(expanded * scale, ImGuiCond.Always); restoreSize = false; }
            else ImGui.SetNextWindowSize(expanded * scale, ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(590, compact ? Layout.HeaderHeight : 440) * scale, new Vector2(1800, 1400) * scale);
            var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
            if (compact) flags |= ImGuiWindowFlags.NoResize;
            bool visible = ImGui.Begin("Keybound Automation###KeyboundAutomationShell", ref open, flags);
            try
            {
                if (!visible) return;
                var pos = ImGui.GetWindowPos();
                var size = ImGui.GetWindowSize();
                if (!compact) expanded = size / scale;
                float header = Layout.HeaderHeight * scale;
                var dl = ImGui.GetWindowDrawList();
                dl.AddRectFilled(pos, pos + new Vector2(size.X, header), ImGui.GetColorU32(colors.Surface1), 12 * scale);
                ImGui.SetCursorScreenPos(pos + new Vector2(16, 12) * scale);
                using (pi.UiBuilder.IconFontHandle.Push()) ImGui.TextColored(colors.AccentTeal, FontAwesomeIcon.Keyboard.ToIconString());
                ImGui.SameLine();
                ImGui.TextUnformatted("Keybound Automation");
                ImGui.SameLine();
                ImGui.TextColored(running ? colors.AccentMint : colors.TextDim, status);
                // The header drag region excludes the close/compact buttons.
                ImGui.SetCursorScreenPos(pos);
                ImGui.InvisibleButton("header_drag", new Vector2(Math.Max(1, size.X - 110 * scale), header));
                bool toggle = ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left);
                if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
                    ImGui.SetWindowPos(pos + ImGui.GetIO().MouseDelta);
                ImGui.SetCursorScreenPos(pos + new Vector2(size.X - 96 * scale, 10 * scale));
                if (IconButton(pi, compact ? FontAwesomeIcon.ChevronUp : FontAwesomeIcon.ChevronDown, "Compact mode")) toggle = true;
                ImGui.SameLine();
                if (IconButton(pi, FontAwesomeIcon.Times, "Close")) open = false;
                if (toggle) { compact = !compact; restoreSize = !compact; }
                if (compact) return;

                float dock = Layout.DockHeight * scale;
                float rail = Layout.RailWidth * scale;
                float body = Math.Max(1, size.Y - header - dock);
                ImGui.SetCursorScreenPos(pos + new Vector2(0, header));
                bool railVisible = ImGui.BeginChild("navigation", new Vector2(rail, body), false);
                try
                {
                    if (railVisible)
                        for (int i = 0; i < pages.Length; i++)
                        {
                            ImGui.SetCursorPosX(10 * scale);
                            bool selected = i == page;
                            if (selected) ImGui.PushStyleColor(ImGuiCol.Button, colors.AccentTealDeep with { W = .55f });
                            if (IconButton(pi, icons[i], pages[i], 42)) page = i;
                            if (selected) ImGui.PopStyleColor();
                            ImGui.Dummy(new Vector2(0, 6 * scale));
                        }
                }
                finally { ImGui.EndChild(); }
                dl.AddLine(pos + new Vector2(rail, header + 8 * scale), pos + new Vector2(rail, header + body - 8 * scale), ImGui.GetColorU32(colors.BorderDim));
                ImGui.SetCursorScreenPos(pos + new Vector2(rail + 22 * scale, header + 18 * scale));
                bool content = ImGui.BeginChild("page", new Vector2(Math.Max(1, size.X - rail - 42 * scale), Math.Max(1, body - 34 * scale)), false);
                try
                {
                    if (content)
                    {
                        ImGui.TextColored(colors.TextStrong, pages[page]);
                        ImGui.Separator();
                        ImGui.Spacing();
                        switch (page)
                        {
                            case 0:
                                DrawStatus(running, status, word, scale);
                                brawl(); break;
                            case 1: settings(); break;
                            case 2: log(); break;
                            case 3: changelog(); break;
                            default: about(); break;
                        }
                    }
                }
                finally { ImGui.EndChild(); }
                dl.AddRectFilled(pos + new Vector2(0, size.Y - dock), pos + size, ImGui.GetColorU32(colors.Surface1), 12 * scale);
                ImGui.SetCursorScreenPos(pos + new Vector2(rail + 22 * scale, size.Y - dock + 16 * scale));
                bool dockVisible = ImGui.BeginChild("playback_dock", new Vector2(Math.Max(1, size.X - rail - 40 * scale), dock - 20 * scale), false, ImGuiWindowFlags.NoScrollbar);
                try { if (dockVisible) playback(); }
                finally { ImGui.EndChild(); }
            }
            finally { ImGui.End(); }
        }
        finally { ImGui.PopStyleVar(4); ImGui.PopStyleColor(11); }
    }

    public static void Card(string title, Action content)
    {
        float scale = ImGuiHelpers.GlobalScale;
        ImGui.BeginGroup();
        ImGui.TextColored(new Vector4(.32f,.78f,.78f,1), title);
        ImGui.Separator();
        ImGui.Spacing();
        content();
        ImGui.Spacing();
        ImGui.EndGroup();
        ImGui.Dummy(new Vector2(0, 12 * scale));
    }

    private void DrawStatus(bool running, string status, string word, float scale)
    {
        var start = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        float radius = 25 * scale;
        var center = start + new Vector2(34, 37) * scale;
        dl.AddCircle(center, radius, ImGui.GetColorU32(colors.BorderDim), 48, 4 * scale);
        if (running)
        {
            float angle = (float)(ImGui.GetTime() % 2 * Math.PI);
            dl.PathArcTo(center, radius, angle, angle + 4.5f, 40);
            dl.PathStroke(ImGui.GetColorU32(colors.AccentTeal), ImDrawFlags.None, 4 * scale);
        }
        ImGui.SetCursorScreenPos(start + new Vector2(78, 12) * scale);
        ImGui.TextColored(running ? colors.AccentTeal : colors.TextDim, status);
        ImGui.TextWrapped(string.IsNullOrEmpty(word) ? "Ready for the next prompt" : word);
        ImGui.SetCursorScreenPos(start + new Vector2(0, 82) * scale);
        ImGui.Separator();
        ImGui.Spacing();
    }

    private static bool IconButton(IDalamudPluginInterface pi, FontAwesomeIcon icon, string tip, float width = 32)
    {
        bool clicked;
        using (pi.UiBuilder.IconFontHandle.Push())
            clicked = ImGui.Button(icon.ToIconString() + "##" + tip, new Vector2(width, 30) * ImGuiHelpers.GlobalScale);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
        return clicked;
    }

    private void PushStyle()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, colors.WindowBg);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, colors.CardBg);
        ImGui.PushStyleColor(ImGuiCol.Text, colors.TextStrong);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, colors.TextDim);
        ImGui.PushStyleColor(ImGuiCol.Button, colors.Surface2);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colors.Surface3);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, colors.AccentTealDeep);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, colors.Surface1);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, colors.AccentTeal);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, colors.AccentTeal);
        ImGui.PushStyleColor(ImGuiCol.Border, colors.BorderDim);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 7f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10, 10) * ImGuiHelpers.GlobalScale);
    }
}

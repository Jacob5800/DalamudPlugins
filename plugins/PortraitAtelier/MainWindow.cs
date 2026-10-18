using System.Text.Json;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace PortraitAtelier;

internal sealed class MainWindow : Window
{
    private readonly PluginConfig config;
    private readonly Action saveConfig;
    private readonly string configDirectory;
    private string settingsJson = "";
    private string settingsStatus = "";
    private SettingsBackup? settingsPreview;
    private sealed record SettingsBackup(string Plugin, int FormatVersion, PluginConfig Settings);
    private IReadOnlyList<PortraitStyle> styles = PortraitStyle.Featured;
    private Task<StyleCatalogResult>? catalogRefreshTask;
    private string search = "";
    private string shareCodeInput = "";
    private string generatedCode = "";
    private string status = "Open the game's Portrait Editor and capture its current settings.";
    private PortraitPreset? generatedPreset;
    private readonly PortraitEditorBridge editorBridge;
    private PortraitStyle selectedStyle;
    private PortraitPreset? basePreset;
    private bool basePresetFromCode;
    private float strength = 0.85f;
    private int variation;
    private bool favoritesOnly;
    private bool randomLook;
    private DateTimeOffset nextOwnedDesignRefresh;
    private List<PortraitCharacterOption> ownedBackgrounds = [];
    private List<PortraitCharacterOption> ownedFrames = [];
    private List<PortraitCharacterOption> ownedAccents = [];
    private readonly PortraitDesignSession automaticSession = new();

    public MainWindow(PluginConfig config, Action saveConfig, PortraitEditorBridge editorBridge, string configDirectory) : base("Portrait Atelier")
    {
        this.config = config;
        this.saveConfig = saveConfig;
        this.editorBridge = editorBridge;
        this.configDirectory = configDirectory;
        selectedStyle = PortraitStyle.Featured[0];
        Size = new Vector2(820, 710);
        SizeCondition = ImGuiCond.FirstUseEver;
        Flags = ImGuiWindowFlags.NoCollapse;
    }

    public override void Draw()
    {
        CompleteCatalogRefresh();
        DrawSettingsBackup();
        DrawDesignPreferences();
        ImGui.TextWrapped("Build a portrait look using lighting, framing and your unlocked poses, expressions and designs.");
        var styleCharacter = config.StylePoseAndExpression;
        if (ImGui.Checkbox("Choose pose and expression", ref styleCharacter))
        {
            config.StylePoseAndExpression = styleCharacter;
            saveConfig();
        }
        var styleDesign = config.StyleOwnedDesign;
        if (ImGui.Checkbox("Choose owned background, frame and decoration", ref styleDesign))
        {
            config.StyleOwnedDesign = styleDesign;
            saveConfig();
        }
        ImGui.BeginDisabled(!config.StyleOwnedDesign);
        var changeBackground = config.StyleBackground;
        if (ImGui.Checkbox("Change background", ref changeBackground)) { config.StyleBackground = changeBackground; saveConfig(); }
        ImGui.SameLine();
        var changeFrame = config.StyleFrame;
        if (ImGui.Checkbox("Change frame", ref changeFrame)) { config.StyleFrame = changeFrame; saveConfig(); }
        ImGui.SameLine();
        var changeAccent = config.StyleAccent;
        if (ImGui.Checkbox("Change accent", ref changeAccent)) { config.StyleAccent = changeAccent; saveConfig(); }
        ImGui.EndDisabled();
        if (ImGui.Button("Make a portrait for me"))
            CreateAndApplyRecommended();
        ImGui.TextWrapped("Open the game's Portrait Editor and wait for the character preview. This button applies a full-strength look. Review it, then use the game's Save button to keep it.");
        ImGui.TextWrapped(status);
        ImGui.TextDisabled("Applying changes only the open editor. It never presses the game's save button.");
        ImGui.Separator();

        DrawBasePreset();
        ImGui.Separator();
        DrawStylePicker();
        ImGui.Separator();
        DrawGenerator();
        ImGui.Separator();
        DrawOutput();
        ImGui.Separator();
        if (DiscordJoinButton.Draw()) Dalamud.Utility.Util.OpenLink("https://discord.gg/TTPZ82xaUd");
        ImGui.Dummy(new Vector2(0, ImGui.GetTextLineHeightWithSpacing()));
        var version = $"v{typeof(MainWindow).Assembly.GetName().Version}";
        var windowPos = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var versionSize = ImGui.CalcTextSize(version);
        ImGui.GetWindowDrawList().AddText(new Vector2(windowPos.X + windowSize.X - versionSize.X - 18, windowPos.Y + windowSize.Y - versionSize.Y - 8), ImGui.GetColorU32(ImGuiCol.TextDisabled), version);
    }

    private void DrawDesignPreferences()
    {
        if (!ImGui.CollapsingHeader("Preferred frames, backgrounds and accents")) return;
        config.PreferredFrameIds ??= []; config.PreferredBackgroundIds ??= []; config.PreferredAccentIds ??= [];
        if (DateTimeOffset.UtcNow >= nextOwnedDesignRefresh)
        {
            nextOwnedDesignRefresh = DateTimeOffset.UtcNow.AddSeconds(1);
            if (!editorBridge.TryGetOwnedDesigns(out ownedBackgrounds, out ownedFrames, out ownedAccents))
            { ownedBackgrounds = []; ownedFrames = []; ownedAccents = []; }
        }
        ImGui.TextWrapped("Owned designs load automatically while the Portrait Editor is open. Preferences are favored on about three out of four draws; with no preferences, a random owned design is used.");
        if (ImGui.Button("Load owned designs"))
            status = editorBridge.TryGetOwnedDesigns(out ownedBackgrounds, out ownedFrames, out ownedAccents) ? "Owned design choices loaded." : "Open the Portrait Editor first.";
        void List(string label, List<ushort> preferred, List<PortraitCharacterOption> owned)
        {
            if (ImGui.BeginCombo(label, $"{preferred.Count} preferred"))
            {
                foreach (var option in owned)
                {
                    var chosen = preferred.Contains(option.Id);
                    if (ImGui.Selectable($"{option.Name} (#{option.Id})", chosen, ImGuiSelectableFlags.DontClosePopups))
                    { if (chosen) preferred.Remove(option.Id); else preferred.Add(option.Id); saveConfig(); }
                }
                if (owned.Count == 0) ImGui.TextDisabled("Open the Portrait Editor to see owned designs.");
                ImGui.EndCombo();
            }
            foreach (var id in preferred.ToArray())
            {
                ImGui.PushID(label + id);
                if (ImGui.SmallButton("X")) { preferred.Remove(id); saveConfig(); }
                ImGui.SameLine();
                ImGui.TextUnformatted(owned.FirstOrDefault(option => option.Id == id)?.Name ?? $"Design #{id} (open the Portrait Editor for name)");
                ImGui.PopID();
            }
        }
        List("Prefer Frame", config.PreferredFrameIds, ownedFrames);
        List("Prefer background", config.PreferredBackgroundIds, ownedBackgrounds);
        List("Prefer accent", config.PreferredAccentIds, ownedAccents);
    }

    private void DrawSettingsBackup()
    {
        if (!ImGui.CollapsingHeader("Settings / import and export")) return;
        var showBar = config.ShowServerBar;
        if (ImGui.Checkbox("Show PA server-bar button", ref showBar)) { config.ShowServerBar = showBar; saveConfig(); }
        ImGui.TextWrapped("Back up favorites, styling preferences, automatic-style position and the server-bar setting. Portrait share codes are separate.");
        if (ImGui.Button("Export settings to clipboard"))
        {
            settingsJson = JsonSerializer.Serialize(new SettingsBackup("PortraitAtelier", 1, config), new JsonSerializerOptions { WriteIndented = true });
            ImGui.SetClipboardText(settingsJson);
            settingsStatus = "Settings copied to clipboard.";
        }
        ImGui.SameLine();
        if (ImGui.Button("Paste settings")) { settingsJson = ImGui.GetClipboardText() ?? ""; settingsPreview = null; }
        if (ImGui.InputTextMultiline("Settings JSON", ref settingsJson, 262144, new Vector2(-1, 100))) settingsPreview = null;
        if (ImGui.Button("Preview import"))
        {
            try
            {
                if (settingsJson.Length > 250000) throw new InvalidOperationException("Settings are too large.");
                var backup = JsonSerializer.Deserialize<SettingsBackup>(settingsJson) ?? throw new InvalidOperationException("Invalid settings JSON.");
                if (backup.Plugin != "PortraitAtelier" || backup.FormatVersion != 1 || backup.Settings is null || backup.Settings.Version != config.Version)
                    throw new InvalidOperationException("Unsupported Portrait Atelier settings backup.");
                if (backup.Settings.FavoriteStyleIds is null || backup.Settings.FavoriteStyleIds.Count > 5000 || backup.Settings.FavoriteStyleIds.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 200) || backup.Settings.NextAutomaticStyleIndex < 0)
                    throw new InvalidOperationException("Invalid favorite styles or automatic-style position.");
                settingsPreview = backup;
                settingsStatus = "Preview ready. Nothing imported yet.";
            }
            catch (Exception ex) { settingsPreview = null; settingsStatus = $"Import preview failed: {ex.Message}"; }
        }
        if (settingsPreview is { } preview)
        {
            ImGui.TextWrapped($"Replace settings: {preview.Settings.FavoriteStyleIds.Count} favorites; pose/expression {(preview.Settings.StylePoseAndExpression ? "on" : "off")}; owned designs {(preview.Settings.StyleOwnedDesign ? "on" : "off")}; PA button {(preview.Settings.ShowServerBar ? "on" : "off")}.");
            ImGui.TextWrapped($"Change background: {preview.Settings.StyleBackground}; frame: {preview.Settings.StyleFrame}; accent: {preview.Settings.StyleAccent}.");
            ImGui.TextWrapped($"Preferred designs: {preview.Settings.PreferredFrameIds?.Count ?? 0} frames, {preview.Settings.PreferredBackgroundIds?.Count ?? 0} backgrounds, {preview.Settings.PreferredAccentIds?.Count ?? 0} accents.");
            if (ImGui.Button("Import previewed settings"))
            {
                try
                {
                    System.IO.Directory.CreateDirectory(configDirectory);
                    var backupPath = System.IO.Path.Combine(configDirectory, $"settings-before-import-{DateTime.Now:yyyyMMdd-HHmmssfff}.json");
                    System.IO.File.WriteAllText(backupPath, JsonSerializer.Serialize(new SettingsBackup("PortraitAtelier", 1, config), new JsonSerializerOptions { WriteIndented = true }));
                    config.FavoriteStyleIds = preview.Settings.FavoriteStyleIds.Distinct().ToList();
                    config.PreferredFrameIds = (preview.Settings.PreferredFrameIds ?? []).Distinct().Take(4096).ToList();
                    config.PreferredBackgroundIds = (preview.Settings.PreferredBackgroundIds ?? []).Distinct().Take(4096).ToList();
                    config.PreferredAccentIds = (preview.Settings.PreferredAccentIds ?? []).Distinct().Take(4096).ToList();
                    config.StylePoseAndExpression = preview.Settings.StylePoseAndExpression;
                    config.StyleOwnedDesign = preview.Settings.StyleOwnedDesign;
                    config.StyleBackground = preview.Settings.StyleBackground;
                    config.StyleFrame = preview.Settings.StyleFrame;
                    config.StyleAccent = preview.Settings.StyleAccent;
                    config.NextAutomaticStyleIndex = preview.Settings.NextAutomaticStyleIndex;
                    config.ShowServerBar = preview.Settings.ShowServerBar;
                    saveConfig();
                    settingsPreview = null;
                    settingsStatus = $"Settings imported. Safety backup: {backupPath}";
                }
                catch (Exception ex) { settingsStatus = $"Import failed: {ex.Message}"; }
            }
            ImGui.SameLine();
            if (ImGui.Button("Cancel import")) settingsPreview = null;
        }
        if (!string.IsNullOrEmpty(settingsStatus)) ImGui.TextWrapped(settingsStatus);
        ImGui.Separator();
    }

    public override void OnOpen()
    {
        config.WindowOpen = true;
        saveConfig();
    }

    public override void OnClose()
    {
        config.WindowOpen = false;
        saveConfig();
    }

    private void DrawBasePreset()
    {
        ImGui.Text("1. Capture your current portrait");
        ImGui.TextDisabled("Open the in-game Portrait Editor, then capture its settings. Capture only reads the editor when you click.");
        if (ImGui.Button("Capture current portrait"))
        {
            if (editorBridge.TryCapture(out var captured, out var error))
            {
                basePreset = captured;
                automaticSession.Reset();
                basePresetFromCode = false;
                generatedCode = "";
                generatedPreset = null;
                variation = 0;
                status = "Base captured. Choose a look below.";
            }
            else
            {
                basePreset = null;
                status = error;
            }
        }

        ImGui.SameLine();
        ImGui.TextDisabled(basePreset is null ? "No portrait captured" : $"Base ready · pose {basePreset.BannerTimeline} · expression {basePreset.Expression}");

        ImGui.SetNextItemWidth(560f);
        ImGui.InputTextWithHint("##baseShareCode", "Or paste a Portrait Helper v1 code", ref shareCodeInput, 512);
        ImGui.SameLine();
        if (ImGui.Button("Use code as base"))
        {
            if (PortraitPreset.TryParse(shareCodeInput, out var imported, out var error))
            {
                basePreset = imported;
                automaticSession.Reset();
                basePresetFromCode = true;
                generatedPreset = null;
                generatedCode = "";
                variation = 0;
                status = "Preset code loaded as the base. Choose a look and generate a variation.";
            }
            else
            {
                status = error;
            }
        }

        if (basePresetFromCode && basePreset is not null && ImGui.Button("Apply imported code as-is"))
        {
            if (editorBridge.TryApply(basePreset, out var error))
                status = AppendApplyWarning("Applied the imported preset to the open editor. Review it there and use the game's save button if you want to keep it.", error);
            else
                status = error;
        }
    }

    private void DrawStylePicker()
    {
        config.FavoriteStyleIds ??= [];
        ImGui.Text("2. Choose a featured look");
        ImGui.SetNextItemWidth(280f);
        ImGui.InputTextWithHint("##styleSearch", "Search looks...", ref search, 128);
        ImGui.SameLine();
        if (ImGui.Button(favoritesOnly ? "★ Favorites" : "☆ Favorites"))
            favoritesOnly = !favoritesOnly;
        ImGui.SameLine();
        if (ImGui.Button(catalogRefreshTask is null ? "Refresh styles" : "Refreshing styles...") && catalogRefreshTask is null)
        {
            catalogRefreshTask = StyleCatalogClient.DownloadAsync();
            status = "Checking the public style catalog...";
        }

        var availableWidth = Math.Max(ImGui.GetContentRegionAvail().X, 200f);
        var leftWidth = Math.Clamp(availableWidth * 0.44f, 250f, 340f);
        if (ImGui.BeginChild("##stylePicker", new Vector2(leftWidth, 195f), true))
        {
            if (ImGui.Selectable("Random##featuredLook", randomLook)) randomLook = true;
            foreach (var style in GetVisibleStyles())
            {
                ImGui.PushID(style.Id);
                var favorite = config.FavoriteStyleIds.Contains(style.Id);
                if (ImGui.Selectable($"{(favorite ? "★ " : "☆ ")}{style.Name}##style", !randomLook && style.Id == selectedStyle.Id))
                { selectedStyle = style; randomLook = false; }
                ImGui.SameLine();
                ImGui.TextDisabled(style.Category);
                ImGui.PopID();
            }
        }
        ImGui.EndChild();

        ImGui.SameLine();
        if (ImGui.BeginChild("##styleInfo", new Vector2(0, 195f), true))
        {
            if (randomLook) ImGui.TextWrapped("Random: each generation chooses a featured look. Last/current choice:");
            ImGui.Text(selectedStyle.Name);
            ImGui.TextDisabled(selectedStyle.Category);
            ImGui.TextWrapped(selectedStyle.Description);
            ImGui.Spacing();
            DrawSwatch("Key light", selectedStyle.DirectionalColor);
            ImGui.SameLine();
            DrawSwatch("Fill light", selectedStyle.AmbientColor);
            ImGui.Spacing();

            var favorite = config.FavoriteStyleIds.Contains(selectedStyle.Id);
            if (ImGui.Button(favorite ? "Remove favorite" : "Add favorite"))
            {
                if (favorite)
                    config.FavoriteStyleIds.Remove(selectedStyle.Id);
                else
                    config.FavoriteStyleIds.Add(selectedStyle.Id);
                saveConfig();
            }
        }
        ImGui.EndChild();
    }

    private void DrawGenerator()
    {
        ImGui.Text("3. Generate a variation");
        ImGui.SetNextItemWidth(330f);
        var strengthPercent = strength * 100f;
        if (ImGui.SliderFloat("Style strength", ref strengthPercent, 0f, 100f, "%.0f%%", ImGuiSliderFlags.None))
            strength = strengthPercent / 100f;
        strength = Math.Clamp(strength, 0f, 1f);
        ImGui.TextDisabled("Higher strength moves the lighting, crop and gentle framing closer to the selected look.");

        if (ImGui.Button("Generate"))
            GenerateNext();
        ImGui.SameLine();
        if (ImGui.Button("Generate another take"))
            GenerateNext();
        ImGui.SameLine();
        if (ImGui.Button("Generate and apply"))
        {
            if (GenerateNext()) ApplyGenerated();
        }
        ImGui.SameLine();
        ImGui.TextDisabled(variation == 0 ? "No take generated yet" : $"Take {variation}");
    }

    private void DrawOutput()
    {
        ImGui.Text("4. Apply or share the look");
        if (!string.IsNullOrWhiteSpace(status))
            ImGui.TextWrapped(status);

        if (string.IsNullOrWhiteSpace(generatedCode))
        {
            ImGui.TextDisabled("Generate a look to see its import code.");
            return;
        }

        if (ImGui.Button("Apply to open Portrait Editor")) ApplyGenerated();

        ImGui.InputTextMultiline("##generatedCode", ref generatedCode, 2048, new Vector2(-1f, 72f), ImGuiInputTextFlags.ReadOnly);
        if (ImGui.Button("Copy code"))
        {
            ImGui.SetClipboardText(generatedCode);
            status = "Copied a compatible Portrait Helper code. You can share it or import it with HaselTweaks.";
        }
    }

    private PortraitPreset PreserveDisabledDesigns(PortraitPreset generated, PortraitPreset current) => generated with
    {
        BackgroundId = config.StyleOwnedDesign && config.StyleBackground ? generated.BackgroundId : current.BackgroundId,
        FrameId = config.StyleOwnedDesign && config.StyleFrame ? generated.FrameId : current.FrameId,
        DecorationId = config.StyleOwnedDesign && config.StyleAccent ? generated.DecorationId : current.DecorationId,
    };

    private void ApplyGenerated()
    {
        if (generatedPreset is not null && editorBridge.TryCapture(out var current, out _) && current is not null)
            generatedPreset = PreserveDisabledDesigns(generatedPreset, current);
            if (generatedPreset is null)
            {
                status = "Generate a look before applying it.";
            }
            else if (editorBridge.TryApply(generatedPreset, out var error, config.StylePoseAndExpression))
            {
                if (editorBridge.TryCapture(out var actual, out _) && actual is not null)
                {
                    generatedPreset = actual;
                    generatedCode = actual.ToShareCode();
                }
                status = AppendApplyWarning("Applied to the open editor. Review it there and use the game's own save button if you want to keep it.", error);
            }
            else
            {
                status = error;
            }
    }

    private bool GenerateNext()
    {
        if (basePreset is null)
        {
            if (!editorBridge.TryCapture(out var captured, out var captureError) || captured is null)
            { status = captureError; return false; }
            basePreset = captured;
            basePresetFromCode = false;
            automaticSession.Reset();
        }

        if (randomLook && styles.Count > 0) selectedStyle = styles[Random.Shared.Next(styles.Count)];
        var styled = basePreset.WithStyle(selectedStyle, strength, variation);
        if (strength > 0)
            styled = editorBridge.StyleOptions(styled, selectedStyle, variation, config.StylePoseAndExpression, config.StyleOwnedDesign, config);
        generatedPreset = styled;
        generatedCode = styled.ToShareCode();
        variation++;
        status = $"Generated {selectedStyle.Name}. Enabled character and design choices use unlocked options from the open editor.";
        return true;
    }

    private void CreateAndApplyRecommended()
    {
        if (!editorBridge.TryCapture(out var captured, out var captureError) || captured is null)
        {
            status = captureError;
            return;
        }

        basePreset = automaticSession.BaseFor(captured);
        basePresetFromCode = false;
        var automaticStyles = PortraitStyle.Featured;
        var index = (int)((uint)config.NextAutomaticStyleIndex % (uint)automaticStyles.Count);
        selectedStyle = randomLook ? automaticStyles[Random.Shared.Next(automaticStyles.Count)] : automaticStyles[index];
        variation = 0;
        generatedPreset = basePreset.WithStyle(selectedStyle, 1f, variation);
        generatedPreset = editorBridge.StyleOptions(generatedPreset, selectedStyle, variation, config.StylePoseAndExpression, config.StyleOwnedDesign, config);
        if (!config.StylePoseAndExpression)
            generatedPreset = generatedPreset with { BannerTimeline = captured.BannerTimeline, Expression = captured.Expression, AnimationProgress = captured.AnimationProgress };
        generatedPreset = PreserveDisabledDesigns(generatedPreset, captured);
        generatedCode = generatedPreset.ToShareCode();
        variation++;

        if (!editorBridge.TryApply(generatedPreset, out var applyError, config.StylePoseAndExpression))
        {
            status = $"Generated {selectedStyle.Name}, but could not apply it: {applyError}";
            return;
        }

        config.NextAutomaticStyleIndex = (index + 1) % automaticStyles.Count;
        if (editorBridge.TryCapture(out var actual, out _) && actual is not null)
        {
            automaticSession.Remember(basePreset, actual);
            generatedPreset = actual;
            generatedCode = actual.ToShareCode();
        }
        saveConfig();
        status = AppendApplyWarning($"Made and applied {selectedStyle.Name}. Review it, then use the game's save button if you want to keep it.", applyError);
    }

    private IReadOnlyList<PortraitStyle> GetVisibleStyles()
    {
        var query = search.Trim();
        return styles
            .Where(style => !favoritesOnly || config.FavoriteStyleIds.Contains(style.Id))
            .Where(style => string.IsNullOrWhiteSpace(query)
                || style.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || style.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                || style.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private void CompleteCatalogRefresh()
    {
        if (catalogRefreshTask is not { IsCompleted: true } task)
            return;

        catalogRefreshTask = null;
        var result = task.GetAwaiter().GetResult();
        if (!result.Success)
        {
            status = result.Message;
            return;
        }

        var byId = PortraitStyle.Featured.ToDictionary(style => style.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var style in result.Styles)
            byId[style.Id] = style;
        styles = byId.Values.OrderBy(style => style.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        status = $"Style catalog refreshed: {styles.Count} looks available.";
    }

    private static void DrawSwatch(string label, Rgb color)
    {
        ImGui.ColorButton($"##{label}", color.ToColor(), ImGuiColorEditFlags.NoTooltip, new Vector2(22f, 18f));
        ImGui.SameLine();
        ImGui.TextDisabled($"{label}: {color}");
    }

    private static string AppendApplyWarning(string message, string warning) =>
        string.IsNullOrWhiteSpace(warning) ? message : $"{message} {warning}";
}

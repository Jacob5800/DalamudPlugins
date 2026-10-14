using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using LLib.ImGui;

namespace Workshoppa.Windows;

internal sealed class ConfigWindow : LWindow, IPersistableWindowConfig
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly Configuration _configuration;
    private static readonly JsonSerializerOptions BackupJsonOptions = new() { WriteIndented = true };
    private string _backupPath = string.Empty;
    private string _backupStatus = string.Empty;

    public ConfigWindow(IDalamudPluginInterface pluginInterface, Configuration configuration)
        : base("Workshoppa - Configuration###WorkshoppaConfigWindow")

    {
        _pluginInterface = pluginInterface;
        _configuration = configuration;

        Position = new Vector2(100, 100);
        PositionCondition = ImGuiCond.FirstUseEver;
        Flags = ImGuiWindowFlags.AlwaysAutoResize;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(270, 50),
        };
    }

    public WindowConfig WindowConfig => _configuration.ConfigWindowConfig;

    public override void DrawContent()
    {
        var autoOpen = _configuration.AutoOpenRetainerDeposit;
        if (ImGui.Checkbox("Automatically open Retainer Deposit", ref autoOpen))
        { _configuration.AutoOpenRetainerDeposit = autoOpen; _pluginInterface.SavePluginConfig(_configuration); }
        DrawBackup();
        bool enableRepairKitCalculator = _configuration.EnableRepairKitCalculator;
        if (ImGui.Checkbox("Enable Repair Kit Calculator", ref enableRepairKitCalculator))
        {
            _configuration.EnableRepairKitCalculator = enableRepairKitCalculator;
            _pluginInterface.SavePluginConfig(_configuration);
        }

        bool enableCeruleumTankCalculator = _configuration.EnableCeruleumTankCalculator;
        if (ImGui.Checkbox("Enable Ceruleum Tank Calculator", ref enableCeruleumTankCalculator))
        {
            _configuration.EnableCeruleumTankCalculator = enableCeruleumTankCalculator;
            _pluginInterface.SavePluginConfig(_configuration);
        }
    }

    private void DrawBackup()
    {
        if (!ImGui.CollapsingHeader("Backup / export settings")) return;
        ImGui.TextWrapped("Includes calculators, keep list, presets and queued projects. Live crafting progress is excluded. Import only while Workshoppa is idle.");
        ImGui.InputText("Backup JSON path", ref _backupPath, 1024);
        string ExportJson() => JsonSerializer.Serialize(new {
            Plugin = "Workshoppa", BackupVersion = 1,
            Settings = new {
                _configuration.EnableRepairKitCalculator, _configuration.EnableCeruleumTankCalculator, _configuration.AutoOpenRetainerDeposit,
                _configuration.RetainerDepositExcludedItemIds, _configuration.Presets, _configuration.ItemQueue
            }
        }, BackupJsonOptions);
        if (ImGui.Button("Export settings"))
        {
            try { File.WriteAllText(_backupPath.Trim().Trim('"'), ExportJson()); _backupStatus = "Settings exported."; }
            catch (Exception ex) { _backupStatus = ex.Message; }
        }
        ImGui.BeginDisabled(_configuration.CurrentlyCraftedItem is not null);
        if (ImGui.Button("Import settings and merge keep list"))
        {
            try {
                var path = Path.GetFullPath(_backupPath.Trim().Trim('"'));
                var file = new FileInfo(path);
                if (!file.Exists || file.Length > 5 * 1024 * 1024) throw new InvalidOperationException("Choose a backup smaller than 5 MB.");
                using var json = JsonDocument.Parse(File.ReadAllText(path));
                var root = json.RootElement;
                if (root.GetProperty("Plugin").GetString() != "Workshoppa" || root.GetProperty("BackupVersion").GetInt32() != 1)
                    throw new InvalidOperationException("Not a supported Workshoppa backup.");
                var imported = root.GetProperty("Settings").Deserialize<Configuration>() ?? throw new InvalidOperationException("Invalid settings.");
                if (imported.Presets is null || imported.ItemQueue is null || imported.RetainerDepositExcludedItemIds is null)
                    throw new InvalidOperationException("Missing lists.");
                var safety = Path.Combine(Path.GetDirectoryName(path)!, $"Workshoppa-before-import-{DateTime.Now:yyyyMMdd-HHmmssfff}.json");
                File.WriteAllText(safety, ExportJson());
                _configuration.AutoOpenRetainerDeposit = imported.AutoOpenRetainerDeposit;
                _configuration.EnableRepairKitCalculator = imported.EnableRepairKitCalculator;
                _configuration.EnableCeruleumTankCalculator = imported.EnableCeruleumTankCalculator;
                _configuration.RetainerDepositExcludedItemIds = _configuration.RetainerDepositExcludedItemIds.Concat(imported.RetainerDepositExcludedItemIds).Where(id => id != 0).Distinct().ToList();
                _configuration.Presets = imported.Presets;
                _configuration.ItemQueue = imported.ItemQueue;
                _pluginInterface.SavePluginConfig(_configuration);
                _backupStatus = $"Imported settings, presets and queue; merged keep list. Previous settings saved to {safety}";
            } catch (Exception ex) { _backupStatus = $"Import failed: {ex.Message}"; }
        }
        ImGui.EndDisabled();
        ImGui.TextWrapped("Import replaces calculator settings, presets and queued projects; keep-list entries are merged. A safety backup is saved before importing.");
        if (!string.IsNullOrEmpty(_backupStatus)) ImGui.TextWrapped(_backupStatus);
    }
    public void SaveWindowConfig() => _pluginInterface.SavePluginConfig(_configuration);
}

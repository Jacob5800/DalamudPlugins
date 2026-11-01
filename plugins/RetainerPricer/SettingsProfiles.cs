using System.Text.Json;
using System.Text.Json.Nodes;

namespace RetainerPricer;

internal static class SettingsProfiles
{
    internal static bool Initialize(PluginConfig config)
    {
        config.SettingsProfiles ??= [];
        if (config.SettingsProfiles.Count == 0)
        {
            config.ActiveSettingsProfile = "Default";
            Capture(config);
            return true;
        }
        if (config.ActiveSettingsProfile != null && config.SettingsProfiles.ContainsKey(config.ActiveSettingsProfile)) return false;
        // Preserve the currently loaded settings rather than applying an arbitrary saved profile.
        var name = "Recovered settings";
        for (var i = 2; config.SettingsProfiles.ContainsKey(name); i++) name = $"Recovered settings {i}";
        config.ActiveSettingsProfile = name;
        Capture(config);
        return true;
    }

    internal static void Capture(PluginConfig config)
    {
        if (string.IsNullOrEmpty(config.ActiveSettingsProfile)) return;
        config.SettingsProfiles[config.ActiveSettingsProfile] = SettingsBackup.Export(config)["Settings"]!.ToJsonString();
    }

    private static string NewName(PluginConfig config, string name)
    {
        name = name.Trim();
        if (name.Length is < 1 or > 64 || name.Any(char.IsControl))
            throw new InvalidOperationException("Use a profile name of 1–64 characters without control characters.");
        if (config.SettingsProfiles.Keys.Any(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("A profile with that name already exists.");
        return name;
    }

    internal static void Create(PluginConfig config, string name)
    {
        name = NewName(config, name);
        Capture(config);
        config.ActiveSettingsProfile = name;
        Capture(config);
    }

    internal static void Switch(PluginConfig config, string name)
    {
        if (name == config.ActiveSettingsProfile) return;
        if (!config.SettingsProfiles.TryGetValue(name, out var json)) throw new InvalidOperationException("Profile not found.");
        var settings = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Invalid profile settings.");
        _ = settings.Deserialize<PluginConfig>() ?? throw new InvalidOperationException("Invalid profile settings.");
        Capture(config);
        SettingsBackup.Apply(config, settings, false);
        config.ActiveSettingsProfile = name;
    }

    internal static void Rename(PluginConfig config, string oldName, string newName)
    {
        newName = NewName(config, newName);
        if (!config.SettingsProfiles.TryGetValue(oldName, out var json)) throw new InvalidOperationException("Profile not found.");
        config.SettingsProfiles.Add(newName, json);
        config.SettingsProfiles.Remove(oldName);
        if (config.ActiveSettingsProfile == oldName) config.ActiveSettingsProfile = newName;
    }

    internal static void Delete(PluginConfig config, string name)
    {
        if (config.ActiveSettingsProfile == name) throw new InvalidOperationException("Switch to another profile before deleting this one.");
        if (!config.SettingsProfiles.Remove(name)) throw new InvalidOperationException("Profile not found.");
    }
}

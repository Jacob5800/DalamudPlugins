using System.Text.Json;
using System.Text.Json.Nodes;

namespace RetainerPricer;
internal static class SettingsBackup
{
    private static readonly HashSet<string> Omitted = [nameof(PluginConfig.UniversalisRetainerListings), nameof(PluginConfig.UniversalisPendingItemIds), nameof(PluginConfig.UniversalisLastBoardSearchAt), nameof(PluginConfig.Version)];
    internal static JsonObject Export(PluginConfig config)
    {
        var settings = JsonSerializer.SerializeToNode(config)!.AsObject();
        foreach (var key in Omitted) settings.Remove(key);
        return new JsonObject { ["Plugin"] = "RetainerPricer", ["BackupVersion"] = 1, ["Settings"] = settings };
    }
    internal static JsonObject Read(string path)
    {
        var file = new FileInfo(path.Trim().Trim('"'));
        if (!file.Exists || file.Length > 5 * 1024 * 1024) throw new InvalidOperationException("Choose a backup JSON smaller than 5 MB.");
        var root = JsonNode.Parse(File.ReadAllText(file.FullName))?.AsObject() ?? throw new InvalidOperationException("Invalid JSON.");
        if (root["Plugin"]?.GetValue<string>() != "RetainerPricer" || root["BackupVersion"]?.GetValue<int>() != 1)
            throw new InvalidOperationException("Not a supported Retainer Pricer backup.");
        var settings = root["Settings"]?.AsObject() ?? throw new InvalidOperationException("Missing settings.");
        foreach (var key in Omitted) settings.Remove(key);
        // Validate before displaying a preview; normalization is applied when importing.
        _ = settings.Deserialize<PluginConfig>() ?? throw new InvalidOperationException("Invalid settings.");
        return settings;
    }
    internal static void Apply(PluginConfig target, JsonObject settings, bool mergeLists)
    {
        var current = JsonSerializer.SerializeToNode(target)!.AsObject();
        foreach (var pair in settings)
        {
            if (Omitted.Contains(pair.Key) || !current.ContainsKey(pair.Key)) continue;
            if (mergeLists && pair.Value is JsonArray imported && current[pair.Key] is JsonArray existing)
            {
                var known = existing.Select(value => value?.ToJsonString()).ToHashSet();
                foreach (var value in imported) if (known.Add(value?.ToJsonString())) existing.Add(value?.DeepClone());
            }
            else if (mergeLists && pair.Value is JsonObject additions && current[pair.Key] is JsonObject dictionary)
            {
                foreach (var entry in additions) if (!dictionary.ContainsKey(entry.Key)) dictionary[entry.Key] = entry.Value?.DeepClone();
            }
            else current[pair.Key] = pair.Value?.DeepClone();
        }
        var normalized = current.Deserialize<PluginConfig>()!;
        normalized.Normalize();
        foreach (var property in typeof(PluginConfig).GetProperties().Where(property => property.CanWrite && !Omitted.Contains(property.Name)))
            property.SetValue(target, property.GetValue(normalized));
    }
}
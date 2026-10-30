using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace BiSplanner.Import;

public sealed record RecommendedGearEntry
{
    public string Job { get; init; } = "";
    public string[] Fights { get; init; } = [];
    public string Name { get; init; } = "";
    public string Label { get; init; } = "";
    public string Url { get; init; } = "";
    public string OriginalUrl { get; init; } = "";
    public string SourceUrl { get; init; } = "";
    public string Source { get; init; } = "";
    public string SheetName { get; init; } = "";
    public bool Historical { get; init; }
    public bool Community { get; init; }
    public string Validated { get; init; } = "";
}

public static class RecommendedGearCatalog
{
    private sealed class Catalog { public List<RecommendedGearEntry> Entries { get; set; } = []; }
    public static readonly string[] Fights = ["All fights", "Current raid", "FRU", "TOP", "DSR", "TEA", "UWU", "UCOB", "DMU", "AMR", "AAI", "ASS", "Occult Crescent", "Unreal", "Unspecified"];
    public static IReadOnlyList<RecommendedGearEntry> Entries { get; } = Load();
    private static IReadOnlyList<RecommendedGearEntry> Load()
    {
        var assembly = typeof(RecommendedGearCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream("BiSplanner.Import.RecommendedGearCatalog.json")
            ?? throw new InvalidOperationException("Recommended gear catalog is missing from the plugin.");
        return JsonSerializer.Deserialize<Catalog>(stream)?.Entries ?? [];
    }
    public static bool MatchesFight(string assignment, string filter) => filter == "All fights" ||
        assignment.Split('/', StringSplitOptions.TrimEntries).Any(f => f.Equals(filter, StringComparison.OrdinalIgnoreCase));
}

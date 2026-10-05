using System.Text.Json;

namespace StrategyBoardLibrary;

internal sealed class CatalogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string filePath;

    public CatalogStore(string directory)
    {
        Directory.CreateDirectory(directory);
        filePath = Path.Combine(directory, "strategy-board-library.json");
        Entries = Load();
    }

    public List<CatalogEntry> Entries { get; }
    public string? LastError { get; private set; }

    public bool Add(CatalogEntry entry)
    {
        if (Entries.Any(existing => string.Equals(existing.ShareCode, entry.ShareCode, StringComparison.Ordinal)))
        {
            LastError = "That share code is already in your library.";
            return false;
        }

        Entries.Add(entry);
        Save();
        return true;
    }

    public void Remove(CatalogEntry entry)
    {
        Entries.Remove(entry);
        Save();
    }

    public void Save()
    {
        try
        {
            var contents = JsonSerializer.Serialize(Entries, JsonOptions);
            File.WriteAllText(filePath, contents);
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = $"Couldn't save the library: {ex.Message}";
        }
    }

    public string ExportJson() => JsonSerializer.Serialize(Entries, JsonOptions);

    public bool ImportJson(string json, out int imported, out string message)
    {
        imported = 0;
        try
        {
            var incoming = JsonSerializer.Deserialize<List<CatalogEntry>>(json, JsonOptions);
            if (incoming is null)
            {
                message = "The library file was empty or invalid.";
                return false;
            }

            foreach (var entry in incoming)
            {
                if (string.IsNullOrWhiteSpace(entry.Title) || !ShareCodeTools.TryExtract(entry.ShareCode, out var shareCode))
                    continue;
                entry.Encounter ??= string.Empty;
                entry.Tags ??= [];
                entry.SourceUrl ??= string.Empty;
                entry.ShareCode = shareCode;
                if (Entries.Any(existing => string.Equals(existing.ShareCode, shareCode, StringComparison.Ordinal)))
                    continue;
                if (entry.Id == Guid.Empty || Entries.Any(existing => existing.Id == entry.Id))
                    entry.Id = Guid.NewGuid();
                Entries.Add(entry);
                imported++;
            }

            Save();
            message = imported == 0 ? "No new valid boards were found." : $"Imported {imported} board(s).";
            return true;
        }
        catch (JsonException)
        {
            message = "That isn't a valid Strategy Board Library JSON export.";
            return false;
        }
    }

    private List<CatalogEntry> Load()
    {
        if (!File.Exists(filePath))
            return [];

        try
        {
            var entries = JsonSerializer.Deserialize<List<CatalogEntry>>(File.ReadAllText(filePath), JsonOptions) ?? [];
            foreach (var entry in entries)
            {
                entry.Title ??= string.Empty;
                entry.Encounter ??= string.Empty;
                entry.Tags ??= [];
                entry.SourceUrl ??= string.Empty;
                entry.ShareCode ??= string.Empty;
            }
            return entries;
        }
        catch (Exception ex)
        {
            LastError = $"Couldn't read the saved library: {ex.Message}";
            return [];
        }
    }
}

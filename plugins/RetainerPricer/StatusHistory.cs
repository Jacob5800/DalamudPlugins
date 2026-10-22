using Dalamud.Plugin.Services;

namespace RetainerPricer;

internal sealed record StatusEntry(DateTimeOffset Time, string Source, string Level, string Message, string Progress)
{
    public string Report => $"{Time:yyyy-MM-dd HH:mm:ss zzz} [{Level}] {Source}: {Message}" + (string.IsNullOrWhiteSpace(Progress) ? "" : $"\n{Progress}");
}

internal sealed class StatusHistory(IPluginLog log)
{
    private readonly object gate = new();
    private readonly List<StatusEntry> entries = [];
    private readonly Dictionary<string, string> previous = [];
    private readonly string version = typeof(StatusHistory).Assembly.GetName().Version?.ToString() ?? "unknown";
    public static bool IsComplete(string message) => message.StartsWith("Auto update complete:", StringComparison.OrdinalIgnoreCase) ||
        (TryGetSniperScanFailure(message, out var failed) && !failed);
    public static bool IsFailure(string message) => TryGetSniperScanFailure(message, out var failed) ? failed : !IsComplete(message) &&
        new[] { "failed", "error", "timed out", "timeout", "could not", "cannot", "try again", "stopped", "HTTP", "unavailable" }
            .Any(word => message.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static bool TryGetSniperScanFailure(string message, out bool failed)
    {
        failed = false;
        if (!message.StartsWith("Sniper scan finished:", StringComparison.OrdinalIgnoreCase)) return false;
        int? failedBatches = null;
        int? unresolvedItems = null;
        foreach (var field in message.Split(';'))
        {
            var parts = field.Trim().Split('=', 2);
            if (parts.Length != 2 || !int.TryParse(parts[1], out var count) || count < 0) continue;
            if (parts[0].Equals("failedBatches", StringComparison.OrdinalIgnoreCase)) failedBatches = count;
            else if (parts[0].Equals("unresolvedItems", StringComparison.OrdinalIgnoreCase)) unresolvedItems = count;
        }
        // Incomplete or malformed summaries retain the normal warning classification.
        if (failedBatches is null || unresolvedItems is null) return false;
        failed = failedBatches > 0 || unresolvedItems > 0;
        return true;
    }
    public StatusEntry[] Snapshot() { lock (gate) return entries.ToArray(); }
    public void Clear() { lock (gate) entries.Clear(); }
    public void Observe(string source, string message, string progress = "")
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        lock (gate)
        {
            if (previous.TryGetValue(source, out var old) && old == message) return;
            previous[source] = message;
        }
        Add(source, message, progress);
    }
    public void Add(string source, string message, string progress = "")
    {
        var level = IsComplete(message) ? "Complete" : IsFailure(message) ? "Warning" : "Info";
        var entry = new StatusEntry(DateTimeOffset.Now, source, level, message, progress);
        lock (gate) { entries.Add(entry); if (entries.Count > 300) entries.RemoveAt(0); }
        if (level == "Warning") log.Warning("[Status {Version}] {Entry}", version, entry.Report);
        else log.Information("[Status {Version}] {Entry}", version, entry.Report);
    }
    public string Export(IEnumerable<StatusEntry> selected) => $"Retainer Pricer {version}\n" + string.Join("\n\n", selected.Select(entry => entry.Report));
}

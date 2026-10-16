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
    public static bool IsComplete(string message) => message.StartsWith("Auto update complete:", StringComparison.OrdinalIgnoreCase);
    public static bool IsFailure(string message) => !IsComplete(message) &&
        new[] { "failed", "error", "timed out", "timeout", "could not", "cannot", "try again", "stopped", "HTTP", "unavailable" }
            .Any(word => message.Contains(word, StringComparison.OrdinalIgnoreCase));
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

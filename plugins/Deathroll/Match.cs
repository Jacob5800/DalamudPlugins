namespace Deathroll;

internal sealed class Match
{
    public Dictionary<string, int> Rolls { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Remaining { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Out { get; } = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string>? eligible;
    public int Expected { get; private set; }
    public int Maximum { get; private set; }
    public bool HighestOut { get; private set; }
    public bool Active { get; private set; }
    public bool Ready => Active && Rolls.Count == Expected;
    public int Round { get; private set; }
    public string Status { get; private set; } = "Start a match to capture rolls.";
    public void Start(int count, int maximum, bool highest)
    {
        Expected = Math.Clamp(count, 2, 100); Maximum = Math.Clamp(maximum, 2, 999999);
        HighestOut = highest; Round = 1; Active = true; eligible = null;
        Rolls.Clear(); Remaining.Clear(); Out.Clear(); Status = $"Round 1: waiting for {Expected} players.";
    }
    public bool Accept(string player, int value, int maximum)
    {
        player = player.Trim();
        if (!Active || Ready || maximum != Maximum || value < 1 || value > Maximum || player.Length == 0 ||
            Out.Contains(player) || (eligible is not null && !eligible.Contains(player)) || Rolls.ContainsKey(player)) return false;
        Rolls.Add(player, value);
        Status = Ready ? "All rolls received. Resolve the round." : $"Received {Rolls.Count}/{Expected} rolls.";
        return true;
    }
    public string Resolve()
    {
        if (!Ready) return "Waiting for remaining rolls.";
        if (Remaining.Count == 0) Remaining.UnionWith(Rolls.Keys);
        var extreme = HighestOut ? Rolls.Values.Max() : Rolls.Values.Min();
        var tied = Rolls.Where(x => x.Value == extreme).Select(x => x.Key).ToArray();
        if (tied.Length > 1)
        {
            eligible = tied.ToHashSet(StringComparer.OrdinalIgnoreCase); Expected = tied.Length; Rolls.Clear();
            return Status = $"Tie at {extreme}: {string.Join(", ", tied)} only, roll again (/random {Maximum}).";
        }
        Remaining.Remove(tied[0]); Out.Add(tied[0]); Rolls.Clear();
        var result = $"{tied[0]} is out with {extreme}.";
        if (Remaining.Count == 1)
        {
            Active = false;
            return Status = $"{result} {Remaining.Single()} wins!";
        }
        eligible = new(Remaining, StringComparer.OrdinalIgnoreCase); Expected = Remaining.Count; Round++;
        return Status = $"{result} Roll again! (/random {Maximum}; {Expected} players remain.)";
    }
    public void Stop() { Active = false; Status = "Stopped."; }
    public void MarkOut(string player)
    {
        player = player.Trim();
        if (!Active || player.Length == 0 || !Out.Add(player)) return;
        var wasEligible = eligible?.Remove(player) ?? Rolls.ContainsKey(player);
        Rolls.Remove(player);
        Remaining.Remove(player);
        if (wasEligible) Expected--;
        if (Remaining.Count == 1 && eligible is not null)
        { Active = false; Status = $"{Remaining.Single()} wins!"; }
        else if (Expected < 1) Stop();
        else Status = $"{player} marked out. Their future rolls will be ignored.";
    }
}

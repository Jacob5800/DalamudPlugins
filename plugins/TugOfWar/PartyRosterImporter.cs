namespace TugOfWar;

internal sealed record PartyRosterAssignment(string Player, TeamSide? Team);

internal static class PartyRosterImporter
{
    public static (string TeamA, string TeamB) Apply(string teamARoster, string teamBRoster,
        IReadOnlyCollection<PartyRosterAssignment> assignments)
    {
        var currentParty = assignments
            .Select(a => MatchEngine.NormalizePlayerName(a.Player))
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var teamA = ParseEntries(teamARoster)
            .Where(name => !currentParty.Contains(MatchEngine.NormalizePlayerName(name)))
            .ToList();
        var teamB = ParseEntries(teamBRoster)
            .Where(name => !currentParty.Contains(MatchEngine.NormalizePlayerName(name)))
            .ToList();

        foreach (var assignment in assignments)
        {
            var name = assignment.Player.Trim();
            if (name.Length == 0) continue;

            switch (assignment.Team)
            {
                case TeamSide.A:
                    AddIfMissing(teamA, name);
                    break;
                case TeamSide.B:
                    AddIfMissing(teamB, name);
                    break;
            }
        }

        return (string.Join(Environment.NewLine, teamA), string.Join(Environment.NewLine, teamB));
    }

    private static List<string> ParseEntries(string roster) => roster
        .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(name => name.Length > 0)
        .ToList();

    private static void AddIfMissing(List<string> roster, string name)
    {
        var normalized = MatchEngine.NormalizePlayerName(name);
        if (normalized.Length > 0 && roster.All(existing => !string.Equals(
                MatchEngine.NormalizePlayerName(existing), normalized, StringComparison.OrdinalIgnoreCase)))
            roster.Add(name);
    }
}

using System.Text.RegularExpressions;

namespace TugOfWar;

internal enum TeamSide { A, B }

internal enum RollStatus
{
    Ignored,
    UnknownPlayer,
    TeamAlreadyRolled,
    Accepted,
    Tie,
    RoundWon,
    MatchWon,
}

internal sealed record TeamRoll(string Player, int Value);
internal sealed record RollOutcome(RollStatus Status, string Message, TeamSide? Winner = null);

internal sealed class MatchEngine
{
    private static readonly Regex RandomRollPattern = new(
        @"\broll(?:s|ed)?\b[^\d]{0,24}(?<roll>\d{1,3})(?:[^\d]{0,12}(?:out\s+of|/|on\s+a|d\s*\d)\s*(?<maximum>\d{1,3}))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private HashSet<string> teamAPlayers = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> teamBPlayers = new(StringComparer.OrdinalIgnoreCase);
    private TeamRoll? rollA;
    private TeamRoll? rollB;

    public string TeamAName { get; private set; } = "Team A";
    public string TeamBName { get; private set; } = "Team B";
    public int ScoreA { get; private set; }
    public int ScoreB { get; private set; }
    public int Round { get; private set; } = 1;
    public bool IsActive { get; private set; }
    public TeamRoll? RollA => rollA;
    public TeamRoll? RollB => rollB;

    public bool Configure(string teamAName, string teamARoster, string teamBName, string teamBRoster, out string error)
    {
        var aName = string.IsNullOrWhiteSpace(teamAName) ? "Team A" : teamAName.Trim();
        var bName = string.IsNullOrWhiteSpace(teamBName) ? "Team B" : teamBName.Trim();
        var aPlayers = ParseRoster(teamARoster);
        var bPlayers = ParseRoster(teamBRoster);
        if (aPlayers.Count == 0 || bPlayers.Count == 0)
        {
            error = "Add at least one character to each team's roster.";
            return false;
        }

        var duplicate = aPlayers.Intersect(bPlayers, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        if (duplicate is not null)
        {
            error = $"{duplicate} is listed on both teams. Each character must belong to only one team.";
            return false;
        }

        TeamAName = aName;
        TeamBName = bName;
        teamAPlayers = aPlayers;
        teamBPlayers = bPlayers;
        error = string.Empty;
        return true;
    }

    public void Start()
    {
        ScoreA = 0;
        ScoreB = 0;
        Round = 1;
        rollA = null;
        rollB = null;
        IsActive = true;
    }

    public void Stop() => IsActive = false;

    public RollOutcome Accept(string sender, string message)
    {
        if (!IsActive || !TryReadRandom100(message, out var value))
            return new RollOutcome(RollStatus.Ignored, string.Empty);

        var player = NormalizePlayerName(sender);
        if (teamAPlayers.Contains(player))
        {
            if (rollA is not null)
                return new RollOutcome(RollStatus.TeamAlreadyRolled, $"{TeamAName} already rolled this turn.");
            rollA = new TeamRoll(player, value);
        }
        else if (teamBPlayers.Contains(player))
        {
            if (rollB is not null)
                return new RollOutcome(RollStatus.TeamAlreadyRolled, $"{TeamBName} already rolled this turn.");
            rollB = new TeamRoll(player, value);
        }
        else
        {
            return new RollOutcome(RollStatus.UnknownPlayer, $"Ignored roll from unlisted player {player}.");
        }

        if (rollA is null || rollB is null)
            return new RollOutcome(RollStatus.Accepted, $"{player} rolled {value}. Waiting for the other team.");

        var resolvedA = rollA;
        var resolvedB = rollB;
        rollA = null;
        rollB = null;
        if (resolvedA.Value == resolvedB.Value)
            return new RollOutcome(RollStatus.Tie, $"Tie at {resolvedA.Value}; both teams reroll. Score: {ScoreA}:{ScoreB}.");

        var winner = resolvedA.Value > resolvedB.Value ? TeamSide.A : TeamSide.B;
        if (winner == TeamSide.A)
        {
            ScoreA++;
            ScoreB = Math.Max(0, ScoreB - 1);
        }
        else
        {
            ScoreB++;
            ScoreA = Math.Max(0, ScoreA - 1);
        }

        var winningName = winner == TeamSide.A ? TeamAName : TeamBName;
        var messageText = $"{resolvedA.Player} ({resolvedA.Value}) vs {resolvedB.Player} ({resolvedB.Value}): {winningName} wins. Score: {ScoreA}:{ScoreB}.";
        if (ScoreA >= 3 || ScoreB >= 3)
        {
            IsActive = false;
            messageText += $" {winningName} wins the match!";
            return new RollOutcome(RollStatus.MatchWon, messageText, winner);
        }

        Round++;
        return new RollOutcome(RollStatus.RoundWon, messageText, winner);
    }

    public static bool TryReadRandom100(string message, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(message)) return false;
        var match = RandomRollPattern.Match(message);
        if (!match.Success || !int.TryParse(match.Groups["roll"].Value, out value)
            || !match.Groups["maximum"].Success
            || !int.TryParse(match.Groups["maximum"].Value, out var maximum)
            || maximum != 100)
            return false;
        return value is >= 1 and <= 100;
    }

    internal static HashSet<string> ParseRoster(string text) => text
        .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(NormalizePlayerName)
        .Where(name => name.Length > 0)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    internal static string NormalizePlayerName(string name)
    {
        var normalized = name.Trim();
        var worldSeparator = normalized.LastIndexOf('@');
        if (worldSeparator >= 0) normalized = normalized[..worldSeparator].Trim();
        return normalized;
    }
}

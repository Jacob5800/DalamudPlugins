using TugOfWar;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var match = new MatchEngine();
Check(match.Configure("Team A", "Mimi\nAless", "Team B", "Raine\nNana\nB1\nB2\nB3\nB4", out var error), error);
match.Start();

Check(match.Accept("Aless", "rolls 6 (out of 100)").Status == RollStatus.Accepted, "Aless roll should wait for Team B.");
var result = match.Accept("Raine", "rolls 50 (out of 100)");
Check(result.Status == RollStatus.RoundWon && match.ScoreA == 0 && match.ScoreB == 1, "First B win should produce 0:1.");

match.Accept("Mimi", "rolls 90 (out of 100)");
result = match.Accept("Nana", "rolls 5 (out of 100)");
Check(result.Status == RollStatus.RoundWon && match.ScoreA == 1 && match.ScoreB == 0, "A win should remove B's point and produce 1:0.");

match.Accept("Aless", "rolls 20 (out of 100)");
result = match.Accept("Raine", "rolls 19 (out of 100)");
Check(result.Status == RollStatus.RoundWon && match.ScoreA == 2 && match.ScoreB == 0, "Second consecutive A win should produce 2:0.");

match.Accept("Mimi", "rolls 1 (out of 100)");
result = match.Accept("Nana", "rolls 8 (out of 100)");
Check(result.Status == RollStatus.RoundWon && match.ScoreA == 1 && match.ScoreB == 1, "B win should reduce A and produce 1:1.");

match.Accept("Aless", "rolls 42 (out of 100)");
result = match.Accept("B4", "rolls 42 (out of 100)");
Check(result.Status == RollStatus.Tie && match.ScoreA == 1 && match.ScoreB == 1, "Tie should keep score and request rerolls.");
Check(match.RollA is null && match.RollB is null, "Tie should clear both submissions.");

match.Accept("Mimi", "rolls 100 (out of 100)");
Check(match.Accept("Aless", "rolls 100 (out of 100)").Status == RollStatus.TeamAlreadyRolled, "A second team member cannot submit another roll that turn.");
match.Accept("Raine", "rolls 1 (out of 100)");
Check(match.ScoreA == 2 && match.ScoreB == 0, "One of several listed B members can submit the team roll.");

match.Accept("Aless", "rolls 99 (out of 100)");
result = match.Accept("Nana", "rolls 1 (out of 100)");
Check(result.Status == RollStatus.MatchWon && match.ScoreA == 3 && !match.IsActive, "First team to three should end the match.");

Check(MatchEngine.TryReadRandom100("rolls 1 (out of 100)", out var parsed) && parsed == 1, "Parser should accept roll 1.");
Check(!MatchEngine.TryReadRandom100("rolls 75 (out of 200)", out _), "Parser should reject a roll whose maximum is not 100.");
Check(!MatchEngine.TryReadRandom100("rolls 75", out _), "Parser should reject a result that does not identify its maximum.");
Check(!MatchEngine.TryReadRandom100("Mimi rolls 0 (out of 100)", out _), "Parser should reject roll zero.");

var imported = PartyRosterImporter.Apply("Mimi\nAless\nSolo guest", "Raine\nNana", [
    new PartyRosterAssignment("Aless", TeamSide.B),
    new PartyRosterAssignment("Raine", null),
    new PartyRosterAssignment("New member", TeamSide.A),
]);
Check(imported.TeamA == $"Mimi{Environment.NewLine}Solo guest{Environment.NewLine}New member",
    "Party assignment should preserve outside-party Team A names and add newly assigned members.");
Check(imported.TeamB == $"Nana{Environment.NewLine}Aless",
    "Party assignment should move a party member between teams and omit skipped members.");

Console.WriteLine("Tug of War checks passed.");

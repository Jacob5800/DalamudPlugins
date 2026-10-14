using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace TugOfWar;

internal sealed class TugOfWarWindow : Window
{
    private readonly MatchEngine engine;
    private readonly Action start;
    private readonly Action<RollOutcome> processManualRoll;
    private readonly Action save;
    private readonly Func<IReadOnlyList<string>> readPartyMembers;
    private readonly List<string> partyMembers = [];
    private readonly List<int> partyAssignments = [];
    private string status = "Enter team names and member rosters, then start the match.";
    private string manualPlayer = string.Empty;
    private int manualRoll;

    public TugOfWarConfig Config { get; }

    public TugOfWarWindow(TugOfWarConfig config, MatchEngine engine, Action start,
        Action<RollOutcome> processManualRoll, Action save, Func<IReadOnlyList<string>> readPartyMembers) : base("Tug of War")
    {
        Config = config;
        this.engine = engine;
        this.start = start;
        this.processManualRoll = processManualRoll;
        this.save = save;
        this.readPartyMembers = readPartyMembers;
        Size = new Vector2(610, 610);
        SizeCondition = ImGuiCond.FirstUseEver;
        Flags = ImGuiWindowFlags.NoCollapse;
    }

    public override void Draw()
    {
        ImGui.TextWrapped("Track each /random 100 result for two teams. The winning team gains a point; the other team loses one if it has any. First to 3 wins.");
        ImGui.TextDisabled("Members can roll in any order and the team rosters can be different sizes. Only the first roll from each team counts per turn.");
        ImGui.Separator();

        var nameA = Config.TeamAName;
        if (ImGui.InputText("Team A name", ref nameA, 80)) { Config.TeamAName = nameA; save(); }
        ImGui.Text("Team A members");
        var rosterA = Config.TeamARoster;
        if (ImGui.InputTextMultiline("##rosterA", ref rosterA, 8000, new Vector2(-1, 85)))
        { Config.TeamARoster = rosterA; save(); }

        var nameB = Config.TeamBName;
        if (ImGui.InputText("Team B name", ref nameB, 80)) { Config.TeamBName = nameB; save(); }
        ImGui.Text("Team B members");
        var rosterB = Config.TeamBRoster;
        if (ImGui.InputTextMultiline("##rosterB", ref rosterB, 8000, new Vector2(-1, 85)))
        { Config.TeamBRoster = rosterB; save(); }
        ImGui.TextDisabled("One character name per line. Commas and semicolons also work; use Forename Surname or Forename Surname@World.");

        if (ImGui.CollapsingHeader("Import current party"))
        {
            ImGui.TextWrapped("Refresh the live party, assign each member to a team, then apply. Entries outside the current party are kept.");
            if (ImGui.Button("Refresh party roster")) RefreshPartyMembers();
            if (partyMembers.Count == 0)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("No party snapshot yet.");
            }
            else
            {
                ImGui.SameLine();
                ImGui.TextDisabled($"{partyMembers.Count} member(s)");
                for (var i = 0; i < partyMembers.Count; i++)
                {
                    ImGui.PushID(i);
                    ImGui.TextUnformatted(partyMembers[i]);
                    ImGui.SameLine();
                    ImGui.SetNextItemWidth(150);
                    var assignment = partyAssignments[i];
                    if (ImGui.Combo("##team", ref assignment, "Skip\0Team A\0Team B\0"))
                        partyAssignments[i] = assignment;
                    ImGui.PopID();
                }

                if (ImGui.Button("Apply party assignments")) ApplyPartyAssignments();
            }
        }

        var announce = Config.AnnounceToParty;
        if (ImGui.Checkbox("Announce match and resolved scores in party chat", ref announce))
        { Config.AnnounceToParty = announce; save(); }

        ImGui.Separator();
        if (!engine.IsActive)
        {
            if (ImGui.Button("Start / reset match")) start();
        }
        else
        {
            if (ImGui.Button("End match")) { engine.Stop(); SetStatus("Match ended."); }
            ImGui.SameLine();
            ImGui.Text($"Round {engine.Round}");
        }

        ImGui.Spacing();
        ImGui.Text($"{engine.TeamAName}: {engine.ScoreA}    -    {engine.ScoreB} :{engine.TeamBName}");
        ImGui.Text($"{engine.TeamAName} roll: {FormatRoll(engine.RollA)}");
        ImGui.Text($"{engine.TeamBName} roll: {FormatRoll(engine.RollB)}");
        ImGui.TextWrapped(status);

        ImGui.Separator();
        ImGui.TextDisabled("Manual entry (for rolls the chat reader did not recognize)");
        var player = manualPlayer;
        if (ImGui.InputText("Player name", ref player, 80)) manualPlayer = player;
        ImGui.SetNextItemWidth(120);
        ImGui.InputInt("Roll (1–100)", ref manualRoll);
        manualRoll = Math.Clamp(manualRoll, 0, 100);
        ImGui.SameLine();
        if (ImGui.Button("Record roll") && engine.IsActive && manualRoll > 0)
        {
            var message = $"rolls {manualRoll} (out of 100)";
            processManualRoll(engine.Accept(manualPlayer, message));
        }
    }

    public void SetStatus(string text) => status = text;

    private void RefreshPartyMembers()
    {
        partyMembers.Clear();
        partyMembers.AddRange(readPartyMembers()
            .Distinct(StringComparer.OrdinalIgnoreCase));
        partyAssignments.Clear();

        var teamA = MatchEngine.ParseRoster(Config.TeamARoster);
        var teamB = MatchEngine.ParseRoster(Config.TeamBRoster);
        foreach (var member in partyMembers)
        {
            var normalized = MatchEngine.NormalizePlayerName(member);
            partyAssignments.Add(teamA.Contains(normalized) ? 1 : teamB.Contains(normalized) ? 2 : 0);
        }

        status = partyMembers.Count == 0
            ? "No party members are currently available to import."
            : $"Loaded {partyMembers.Count} party member(s). Assign each to a team or skip them.";
    }

    private void ApplyPartyAssignments()
    {
        var assignments = partyMembers
            .Select((name, index) => new PartyRosterAssignment(name, partyAssignments[index] switch
            {
                1 => TeamSide.A,
                2 => TeamSide.B,
                _ => null,
            }))
            .ToArray();
        var (teamA, teamB) = PartyRosterImporter.Apply(Config.TeamARoster, Config.TeamBRoster, assignments);
        Config.TeamARoster = teamA;
        Config.TeamBRoster = teamB;
        save();
        status = $"Applied party assignments: {assignments.Count(a => a.Team == TeamSide.A)} to {Config.TeamAName}, " +
                 $"{assignments.Count(a => a.Team == TeamSide.B)} to {Config.TeamBName}, " +
                 $"{assignments.Count(a => a.Team is null)} skipped.";
    }

    private static string FormatRoll(TeamRoll? roll) => roll is null ? "Waiting" : $"{roll.Player} — {roll.Value}";
}

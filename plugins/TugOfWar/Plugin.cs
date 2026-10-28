using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Text.RegularExpressions;

namespace TugOfWar;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/tugofwar";
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IChatGui chatGui;
    private readonly IPartyList partyList;
    private readonly IPluginLog log;
    private readonly WindowSystem windowSystem = new("TugOfWar");
    private readonly MatchEngine engine = new();
    private readonly TugOfWarWindow mainWindow;
    private readonly Queue<string> diagnostics = new();
    private DateTime diagnosticWindow;
    private int diagnosticCount;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager,
        IChatGui chatGui, IPartyList partyList, IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.chatGui = chatGui;
        this.partyList = partyList;
        this.log = log;

        var config = pluginInterface.GetPluginConfig() as TugOfWarConfig ?? new TugOfWarConfig();
        mainWindow = new TugOfWarWindow(config, engine, StartMatch, OnOutcome, SaveConfig, ReadPartyMembers,
            () => string.Join(Environment.NewLine, diagnostics), () => diagnostics.Clear());
        windowSystem.AddWindow(mainWindow);

        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        pluginInterface.UiBuilder.OpenMainUi += ToggleWindow;
        pluginInterface.UiBuilder.OpenConfigUi += ToggleWindow;
        commandManager.AddHandler(Command, new CommandInfo((_, _) => ToggleWindow())
        {
            HelpMessage = "Open the Tug of War scorekeeper."
        });
        chatGui.ChatMessage += OnChatMessage;
        Diagnostic($"Loaded {typeof(Plugin).Assembly.GetName().Version}; random-number chat listener registered.");
    }

    public void Dispose()
    {
        chatGui.ChatMessage -= OnChatMessage;
        pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= ToggleWindow;
        pluginInterface.UiBuilder.OpenConfigUi -= ToggleWindow;
        commandManager.RemoveHandler(Command);
        windowSystem.RemoveAllWindows();
    }

    private void ToggleWindow() => mainWindow.Toggle();
    private void SaveConfig() => pluginInterface.SavePluginConfig(mainWindow.Config);

    private IReadOnlyList<string> ReadPartyMembers()
    {
        var names = new List<string>(partyList.Length);
        for (var i = 0; i < partyList.Length; i++)
        {
            var member = partyList[i];
            if (member is null) continue;
            var name = member.Name.TextValue.Trim();
            if (name.Length > 0) names.Add(name);
        }

        return names;
    }

    private void StartMatch()
    {
        if (!engine.Configure(mainWindow.Config.TeamAName, mainWindow.Config.TeamARoster,
                mainWindow.Config.TeamBName, mainWindow.Config.TeamBRoster, out var error))
        {
            Diagnostic($"Start rejected: {error}");
            mainWindow.SetStatus(error);
            return;
        }
        engine.Start(mainWindow.Config.WinningScore);
        Diagnostic($"Started; team A members={MatchEngine.ParseRoster(mainWindow.Config.TeamARoster).Count}, team B members={MatchEngine.ParseRoster(mainWindow.Config.TeamBRoster).Count}, winning score={engine.WinningScore}, party size={partyList.Length}.");
        mainWindow.SetStatus($"Match started: {engine.TeamAName} vs {engine.TeamBName}. Waiting for /random 100 rolls.");
        if (mainWindow.Config.AnnounceToParty)
            SendAnnouncement($"[Tug of War] Match started: {engine.TeamAName} vs {engine.TeamBName}. First to {engine.WinningScore}.");
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        var text = message.Message.TextValue;
        var parsed = MatchEngine.TryReadRandom100(text, out var roll);
        if (message.LogKind != XivChatType.RandomNumber && !parsed) return;
        try
        {
        // /random is a system message: its sender field can be empty. The
        // linked player lives in the message payload instead.
        var linkedPlayer = message.Message.Payloads.OfType<PlayerPayload>().FirstOrDefault()
            ?? message.Sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
        var sender = linkedPlayer?.PlayerName ?? message.Sender.TextValue;
        var source = linkedPlayer is not null ? "player payload" : "sender";
        if (string.IsNullOrWhiteSpace(sender))
        {
            var nameMatch = Regex.Match(text, @"^\s*(?<player>.+?)\s+roll(?:s|ed)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (nameMatch.Success) { sender = nameMatch.Groups["player"].Value; source = "message text"; }
        }
        Diagnostic($"Roll chat type={message.LogKind}; active={engine.IsActive}; parsed={parsed}; value={roll}; sender source={source}; player='{sender}'; payloads={string.Join(",", message.Message.Payloads.Select(p => p.GetType().Name))}; text='{text}'.");
        if (!parsed) { Diagnostic("Rejected: expected a /random 100 result (1–100 with maximum 100)."); return; }
        if (string.IsNullOrWhiteSpace(sender))
        {
            mainWindow.SetStatus("Roll detected, but its player could not be identified. Copy Diagnostics for troubleshooting.");
            return;
        }
        var outcome = engine.Accept(sender, text);
        Diagnostic($"Result={outcome.Status}; round={engine.Round}; score={engine.ScoreA}:{engine.ScoreB}; {outcome.Message}");
        if (outcome.Status == RollStatus.Ignored) return;

        mainWindow.SetStatus(outcome.Message);
        if (outcome.Status is RollStatus.Tie or RollStatus.RoundWon or RollStatus.MatchWon)
        {
            chatGui.Print(outcome.Message, "Tug of War");
            if (mainWindow.Config.AnnounceToParty)
                SendAnnouncement($"[Tug of War] {outcome.Message}");
        }
        else if (outcome.Status is RollStatus.UnknownPlayer or RollStatus.TeamAlreadyRolled)
        {
            log.Debug(outcome.Message);
        }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Tug of War roll processing failed.");
            Diagnostic($"Roll processing exception: {ex.GetType().Name}: {ex.Message}");
            mainWindow.SetStatus("Roll processing failed. Copy Diagnostics and check /xllog.");
        }
    }

    private void Diagnostic(string text)
    {
        if (!mainWindow.Config.DebugLogging) return;
        var now = DateTime.UtcNow;
        if ((now - diagnosticWindow).TotalSeconds >= 1) { diagnosticWindow = now; diagnosticCount = 0; }
        if (++diagnosticCount > 20) return;
        var line = $"{now:O} {text.Replace('\r', ' ').Replace('\n', ' ')}";
        if (line.Length > 2000) line = line[..2000];
        diagnostics.Enqueue(line);
        while (diagnostics.Count > 200) diagnostics.Dequeue();
        log.Information($"[Diagnostics] {line}");
    }

    private void OnOutcome(RollOutcome outcome)
    {
        Diagnostic($"Manual result={outcome.Status}; round={engine.Round}; score={engine.ScoreA}:{engine.ScoreB}; {outcome.Message}");
        mainWindow.SetStatus(outcome.Message);
        if (outcome.Status is RollStatus.Tie or RollStatus.RoundWon or RollStatus.MatchWon)
        {
            chatGui.Print(outcome.Message, "Tug of War");
            if (mainWindow.Config.AnnounceToParty)
                SendAnnouncement($"[Tug of War] {outcome.Message}");
        }
    }

    private void SendAnnouncement(string message)
    {
        try
        {
            var safe = Chat.SanitiseText(message);
            var channel = mainWindow.Config.AnnouncementChannel switch { "Say" => "/say", "Shout" => "/shout", _ => "/p" };
            Diagnostic($"Sending announcement via {channel}.");
            Chat.SendMessage($"{channel} {safe}");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Could not send Tug of War announcement.");
            mainWindow.SetStatus("Score updated, but the announcement could not be sent. Check the plugin log.");
        }
    }
}

public sealed class TugOfWarConfig : Dalamud.Configuration.IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public string TeamAName { get; set; } = "Team A";
    public string TeamARoster { get; set; } = string.Empty;
    public string TeamBName { get; set; } = "Team B";
    public string TeamBRoster { get; set; } = string.Empty;
    public int WinningScore { get; set; } = 3;
    public List<TeamPreset> TeamPresets { get; set; } = [];
    public sealed record TeamPreset(string Name, string TeamAName, string TeamARoster, string TeamBName, string TeamBRoster, int WinningScore);
    public bool AnnounceToParty { get; set; }
    public string AnnouncementChannel { get; set; } = "Party";
    public bool DebugLogging { get; set; } = true;
}

using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.Automation;

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

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager,
        IChatGui chatGui, IPartyList partyList, IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.chatGui = chatGui;
        this.partyList = partyList;
        this.log = log;

        var config = pluginInterface.GetPluginConfig() as TugOfWarConfig ?? new TugOfWarConfig();
        mainWindow = new TugOfWarWindow(config, engine, StartMatch, OnOutcome, SaveConfig, ReadPartyMembers);
        windowSystem.AddWindow(mainWindow);

        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        pluginInterface.UiBuilder.OpenMainUi += ToggleWindow;
        pluginInterface.UiBuilder.OpenConfigUi += ToggleWindow;
        commandManager.AddHandler(Command, new CommandInfo((_, _) => ToggleWindow())
        {
            HelpMessage = "Open the Tug of War scorekeeper."
        });
        chatGui.ChatMessage += OnChatMessage;
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
            mainWindow.SetStatus(error);
            return;
        }
        engine.Start();
        mainWindow.SetStatus($"Match started: {engine.TeamAName} vs {engine.TeamBName}. Waiting for /random 100 rolls.");
        if (mainWindow.Config.AnnounceToParty)
            SendPartyMessage($"[Tug of War] Match started: {engine.TeamAName} vs {engine.TeamBName}. First to 3.");
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (!MatchEngine.TryReadRandom100(message.Message.TextValue, out _)) return;
        var sender = message.Sender.TextValue;
        var outcome = engine.Accept(sender, message.Message.TextValue);
        if (outcome.Status == RollStatus.Ignored) return;

        mainWindow.SetStatus(outcome.Message);
        if (outcome.Status is RollStatus.Tie or RollStatus.RoundWon or RollStatus.MatchWon)
        {
            chatGui.Print(outcome.Message, "Tug of War");
            if (mainWindow.Config.AnnounceToParty)
                SendPartyMessage($"[Tug of War] {outcome.Message}");
        }
        else if (outcome.Status is RollStatus.UnknownPlayer or RollStatus.TeamAlreadyRolled)
        {
            log.Debug(outcome.Message);
        }
    }

    private void OnOutcome(RollOutcome outcome)
    {
        mainWindow.SetStatus(outcome.Message);
        if (outcome.Status is RollStatus.Tie or RollStatus.RoundWon or RollStatus.MatchWon)
        {
            chatGui.Print(outcome.Message, "Tug of War");
            if (mainWindow.Config.AnnounceToParty)
                SendPartyMessage($"[Tug of War] {outcome.Message}");
        }
    }

    private void SendPartyMessage(string message)
    {
        try
        {
            var safe = Chat.SanitiseText(message);
            Chat.SendMessage($"/p {safe}");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Could not send Tug of War party announcement.");
            mainWindow.SetStatus("Score updated, but the party announcement could not be sent. Check the plugin log.");
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
    public bool AnnounceToParty { get; set; }
}

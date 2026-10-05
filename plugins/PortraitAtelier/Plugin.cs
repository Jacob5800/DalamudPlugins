using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace PortraitAtelier;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/portraitatelier";
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly WindowSystem windowSystem = new("PortraitAtelier");
    private readonly PluginConfig config;
    private readonly MainWindow mainWindow;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, ICondition condition, IGameGui gameGui, IDataManager dataManager)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        config = pluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();
        mainWindow = new MainWindow(config, SaveConfig, new PortraitEditorBridge(condition, gameGui, dataManager));
        mainWindow.IsOpen = config.WindowOpen;
        windowSystem.AddWindow(mainWindow);

        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        pluginInterface.UiBuilder.OpenMainUi += ToggleWindow;
        commandManager.AddHandler(Command, new CommandInfo((_, _) => ToggleWindow())
        {
            HelpMessage = "Open the portrait style gallery."
        });
    }

    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= ToggleWindow;
        commandManager.RemoveHandler(Command);
        windowSystem.RemoveAllWindows();
    }

    private void ToggleWindow() => mainWindow.Toggle();

    private void SaveConfig() => pluginInterface.SavePluginConfig(config);
}

public sealed class PluginConfig : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public List<string> FavoriteStyleIds { get; set; } = [];
    public int NextAutomaticStyleIndex { get; set; }
    public bool WindowOpen { get; set; } = true;
    public bool StylePoseAndExpression { get; set; } = true;
    public bool StyleOwnedDesign { get; set; } = true;
}

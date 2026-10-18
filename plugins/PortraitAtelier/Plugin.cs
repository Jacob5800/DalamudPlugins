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
    private readonly Dalamud.Game.Gui.Dtr.IDtrBarEntry serverBar;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, ICondition condition, IGameGui gameGui, IDataManager dataManager, IDtrBar dtrBar)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        config = pluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();
        serverBar = dtrBar.Get("Portrait Atelier", "PA");
        serverBar.Tooltip = "Click to open Portrait Atelier";
        serverBar.OnClick = _ => ToggleWindow();
        serverBar.Shown = config.ShowServerBar;
        mainWindow = new MainWindow(config, SaveConfig, new PortraitEditorBridge(condition, gameGui, dataManager), pluginInterface.GetPluginConfigDirectory());
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
        serverBar.Remove();
    }

    private void ToggleWindow() => mainWindow.Toggle();

    private void SaveConfig()
    {
        serverBar.Shown = config.ShowServerBar;
        pluginInterface.SavePluginConfig(config);
    }
}

public sealed class PluginConfig : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public List<ushort> PreferredFrameIds { get; set; } = [];
    public List<ushort> PreferredBackgroundIds { get; set; } = [];
    public List<ushort> PreferredAccentIds { get; set; } = [];
    public List<string> FavoriteStyleIds { get; set; } = [];
    public int NextAutomaticStyleIndex { get; set; }
    public bool ShowServerBar { get; set; } = false;
    public bool WindowOpen { get; set; } = true;
    public bool StylePoseAndExpression { get; set; } = true;
    public bool StyleOwnedDesign { get; set; } = true;
}

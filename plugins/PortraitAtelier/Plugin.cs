using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Configuration;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace PortraitAtelier;

public sealed class Plugin : IDalamudPlugin
{
    private readonly ShutdownSequence shutdown = new();
    private volatile bool disposed;
    private readonly IPluginLog log;
    private const string Command = "/portraitatelier";
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly WindowSystem windowSystem = new("PortraitAtelier");
    private readonly PluginConfig config;
    private readonly MainWindow mainWindow;
    private readonly Dalamud.Game.Gui.Dtr.IDtrBarEntry serverBar;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, ICondition condition, IGameGui gameGui, IDataManager dataManager, IDtrBar dtrBar, IPluginLog log)
    {
        this.log = log;
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        config = pluginInterface.GetPluginConfig() as PluginConfig ?? new PluginConfig();
        serverBar = dtrBar.Get("Portrait Atelier", "PA");
        serverBar.Text = new SeStringBuilder().AddIcon(BitmapFontIcon.CameraMode).AddText(" PA").Build();
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
        disposed = true;
        var failures = shutdown.Run(
            ("Draw listener", () => { pluginInterface.UiBuilder.Draw -= windowSystem.Draw; }),
            ("Main UI listener", () => { pluginInterface.UiBuilder.OpenMainUi -= ToggleWindow; }),
            ("Command", () => { commandManager.RemoveHandler(Command); }),
            ("Windows", () => { windowSystem.RemoveAllWindows(); }),
            ("Server bar", () => { serverBar.OnClick = null; serverBar.Remove(); }));
        foreach (var (step, error) in failures) log.Error(error, "Shutdown cleanup failed: {Step}; remaining cleanup attempted.", step);
    }

    private void ToggleWindow() { if (!disposed) mainWindow.Toggle(); }

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
    public bool LockCamera { get; set; }
    public bool LockLighting { get; set; }
    public bool LockPose { get; set; }
    public bool LockExpression { get; set; }
    public Dictionary<string, string> NamedPresets { get; set; } = [];
    public bool StyleBackground { get; set; } = true;
    public bool StyleFrame { get; set; } = true;
    public bool StyleAccent { get; set; } = true;
}

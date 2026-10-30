using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace StrategyBoardLibrary;

public sealed class Plugin : IDalamudPlugin
{
    private readonly ShutdownSequence shutdown = new();
    private volatile bool disposed;
    private readonly IPluginLog log;
    private const string Command = "/stratlib";
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly WindowSystem windowSystem = new("StrategyBoardLibrary");
    private readonly CatalogStore catalog;
    private readonly MainWindow mainWindow;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager, IPluginLog log)
    {
        this.log = log;
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        catalog = new CatalogStore(pluginInterface.GetPluginConfigDirectory());
        mainWindow = new MainWindow(catalog);
        windowSystem.AddWindow(mainWindow);

        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        pluginInterface.UiBuilder.OpenMainUi += ToggleWindow;
        commandManager.AddHandler(Command, new CommandInfo((_, _) => ToggleWindow())
        {
            HelpMessage = "Open the searchable Strategy Board Library."
        });
    }

    public void Dispose()
    {
        disposed = true;
        var failures = shutdown.Run(
            ("Draw listener", () => { pluginInterface.UiBuilder.Draw -= windowSystem.Draw; }),
            ("Main UI listener", () => { pluginInterface.UiBuilder.OpenMainUi -= ToggleWindow; }),
            ("Command", () => { commandManager.RemoveHandler(Command); }),
            ("Pending import", () => { mainWindow.Dispose(); }),
            ("Windows", () => { windowSystem.RemoveAllWindows(); }));
        foreach (var (step, error) in failures) log.Error(error, "Shutdown cleanup failed: {Step}; remaining cleanup attempted.", step);
    }

    private void ToggleWindow() { if (!disposed) mainWindow.Toggle(); }
}

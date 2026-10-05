using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace StrategyBoardLibrary;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/stratlib";
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly WindowSystem windowSystem = new("StrategyBoardLibrary");
    private readonly CatalogStore catalog;
    private readonly MainWindow mainWindow;

    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
    {
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
        pluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= ToggleWindow;
        commandManager.RemoveHandler(Command);
        windowSystem.RemoveAllWindows();
    }

    private void ToggleWindow() => mainWindow.Toggle();
}

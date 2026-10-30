using BiSplanner.Resources;
using BiSplanner.Services;
using BiSplanner.Services.Configuration;
using BiSplanner.Ui.Renderers.Tabs.Main;
using BiSplanner.Ui.Windows;
using System.Collections.Generic;

namespace BiSplanner.Commands
{
    public class OpenDebugCommand(
        IConfigurationService configurationService,
        IWindowService windowService
        ) : ICommand
    {
        private static readonly IEnumerable<CommandTrigger> DebugCommandTriggers = [
            new CommandTrigger("debug", "d"),
            ];

        private readonly IConfigurationService configurationService = configurationService;
        private readonly IWindowService windowService = windowService;

        public IEnumerable<CommandTrigger> Triggers => DebugCommandTriggers;

        public string Description => Resource.CommandDescriptionOpenDebug;

        public void Invoke(string args)
        {
            if (!configurationService.EnableDebugging)
                return;

            windowService.SetMainWindowTab(MainWindowTab.PluginDebug);
            windowService.ToggleWindow(WindowType.Main);
        }
    }
}

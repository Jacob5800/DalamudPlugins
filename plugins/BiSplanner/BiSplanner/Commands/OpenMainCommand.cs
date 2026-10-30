using BiSplanner.Services;
using BiSplanner.Ui.Windows;
using System.Collections.Generic;

namespace BiSplanner.Commands
{
    public class OpenMainCommand(IWindowService windowService) : ICommand
    {
        private static readonly IEnumerable<CommandTrigger> MainCommandTriggers = [
            new CommandTrigger(""),
            ];

        private readonly IWindowService windowService = windowService;

        public IEnumerable<CommandTrigger> Triggers => MainCommandTriggers;

        // shouldn't ever be visible
        public string Description => "";

        public void Invoke(string args)
        {
            windowService.ToggleWindow(WindowType.Main);
        }
    }
}

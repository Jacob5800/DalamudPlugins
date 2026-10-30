using BiSplanner.Resources;
using BiSplanner.Services;
using BiSplanner.Ui.Windows;
using System.Collections.Generic;

namespace BiSplanner.Commands
{
    public class AddGearsetCommand(IWindowService windowService) : ICommand
    {
        private static readonly IEnumerable<CommandTrigger> AddGearsetTriggers = [
            new CommandTrigger("add", "a"),
            new CommandTrigger("new", "n"),
            ];

        private readonly IWindowService windowService = windowService;

        public IEnumerable<CommandTrigger> Triggers => AddGearsetTriggers;

        public string Description => Resource.CommandDescriptionAddGearset;

        public void Invoke(string args)
        {
            windowService.ToggleWindow(WindowType.ImportGearset);
        }
    }
}

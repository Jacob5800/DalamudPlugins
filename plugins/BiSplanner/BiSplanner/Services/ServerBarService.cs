using System;
using System.Threading;
using System.Threading.Tasks;
using BiSplanner.Services.Configuration;
using BiSplanner.Ui.Windows;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Hosting;

namespace BiSplanner.Services;

public sealed class ServerBarService(IDtrBar bar, IConfigurationService configuration, IWindowService windows) : IHostedService, IDisposable
{
    private IDtrBarEntry? entry;
    public Task StartAsync(CancellationToken cancellationToken)
    {
        entry = bar.Get("BiSplanner");
        entry.Text = new SeStringBuilder().AddText("BiSplanner").Build();
        entry.Tooltip = new SeStringBuilder().AddText("Open BiSplanner gearsets").Build();
        entry.OnClick = _ => windows.ToggleWindow(WindowType.Main);
        entry.Shown = configuration.ShowServerBar;
        configuration.OnConfigurationChange += Changed;
        return Task.CompletedTask;
    }
    private void Changed(bool affectsAssignments) { if (entry != null) entry.Shown = configuration.ShowServerBar; }
    public Task StopAsync(CancellationToken cancellationToken) { Dispose(); return Task.CompletedTask; }
    public void Dispose()
    {
        configuration.OnConfigurationChange -= Changed;
        entry?.Remove();
        entry = null;
    }
}

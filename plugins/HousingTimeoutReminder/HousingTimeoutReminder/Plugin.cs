using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using System;
using System.Linq;
using System.Threading.Tasks;

using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using ECommons.DalamudServices;

using NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Configuration;
using NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Handler;

namespace NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder;

// Ignore Spelling: htimeout

/// <summary>
/// The Dalamud Plugin Library
/// </summary>
public class Plugin : IDalamudPlugin {
  /// <summary>
  /// The name of the plugin.
  /// </summary>
  internal static string Name => "Housing Timeout Reminder";

  /// <summary>
  /// The plugin's main command name.
  /// </summary>
  private const string CommandName = "/htimeout";
  private const string CommandAlias = "/house";
  private readonly bool aliasRegistered;

  /// <summary>
  /// Bool to test if repositioning the warning dialog.
  /// </summary>
  internal bool Repositioning { get; set; }
  private long nextHousingCheck;
  private readonly IDtrBarEntry housingBar;

  /// <summary>
  /// The Dalamud Plugin constructor
  /// </summary>
  /// <param name="pluginInterface">Argument passed by Dalamud</param>
  public Plugin(IDalamudPluginInterface pluginInterface, IPlayerState playerState) {
    System.PlayerState = playerState;
    System.PluginInstance = this;

    ECommons.ECommonsMain.Init(pluginInterface, System.PluginInstance);

    System.Init();
    housingBar = Svc.DtrBar.Get("Housing Timeout Reminder", "HT");
    housingBar.OnClick = _ => System.SettingsUI.IsOpen = true;

    Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand) {
      HelpMessage = "The config menu for the housing timer reminder plugin."
    });
    aliasRegistered = Svc.Commands.AddHandler(CommandAlias, new CommandInfo(OnCommand) {
      HelpMessage = "Open Housing Timeout Reminder settings. Alias for /htimeout."
    });
    if (!aliasRegistered) Svc.Log.Warning("/house is already registered by another plugin. Use /htimeout instead.");

    Svc.PluginInterface.UiBuilder.Draw += DrawUI;
    Svc.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
    Svc.PluginInterface.UiBuilder.OpenMainUi += DrawConfigUI;
    Svc.ClientState.TerritoryChanged += OnTerritoryChanged;
    Svc.Framework.Update += OnFrameworkUpdate;
    Svc.ClientState.Logout += OnLogout;
    // ReSharper disable once InvertIf
    if (Svc.ClientState.IsLoggedIn) {
      OnTerritoryChanged(Svc.ClientState.TerritoryType);
    }
  }

  /// <summary>
  /// Dispose of the Dalamud Plugin safely.
  /// </summary>
  public void Dispose() {
    housingBar.Remove();
    System.WindowSystem.RemoveAllWindows();
    System.WarningUI.Dispose();
    System.SettingsUI.Dispose();
#if DEBUG
    System.DebugUI.Dispose();
#endif
    Svc.PluginInterface.UiBuilder.Draw -= DrawUI;
    Svc.PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigUI;
    Svc.PluginInterface.UiBuilder.OpenMainUi -= DrawConfigUI;
    Svc.ClientState.TerritoryChanged -= OnTerritoryChanged;
    Svc.Framework.Update -= OnFrameworkUpdate;
    Svc.ClientState.Logout -= OnLogout;
    Svc.Commands.RemoveHandler(CommandName);
    if (aliasRegistered) Svc.Commands.RemoveHandler(CommandAlias);
    ECommons.ECommonsMain.Dispose();
    GC.SuppressFinalize(this);
  }
  private void OnFrameworkUpdate(IFramework framework) {
    housingBar.Shown = System.PluginConfig.ShowServerBar;
    System.CachedCurrentPlayerId = System.GetAuthoritativePlayerID();
    if (System.IsLoggedIn && System.GetCurrentPlayerID() is not null &&
        Environment.TickCount64 >= nextHousingCheck && ECommons.GenericHelpers.IsScreenReady()) {
      nextHousingCheck = Environment.TickCount64 + 5000;
      try {
        Handler.HousingManager.DetectOwnedHousing();
        if (System.PluginConfig.EnableFcReminders) FreeCompanyAccess.Refresh();
        CheckTimers();
        UpdateHousingBar();
      } catch (Exception exception) {
        Svc.Log.Error(exception, "Failed to detect owned housing.");
      }
    }
  }

  private void UpdateHousingBar() {
    var config = System.PluginConfig;
    var houses = config.PlayerConfigs.SelectMany(player => new[] {
      (Player: player, House: player.PrivateEstate, Kind: "Personal estate", Allowed: true),
      (Player: player, House: player.FreeCompanyEstate, Kind: "FC estate", Allowed: config.EnableFcReminders && player.FcEstateAccess != false)
    }).Where(entry => entry.Allowed && entry.House.Enabled && entry.House.IsValid)
      .OrderBy(entry => entry.House.LastVisit == 0 ? long.MinValue : HousingTimer.GetOffset(entry.House.LastVisit)).ToArray();
    if (houses.Length == 0) {
      housingBar.Text = new SeStringBuilder().AddIcon(BitmapFontIcon.Ishgard).AddText(" HT").Build();
      housingBar.Tooltip = "No enabled houses tracked. Click to open Housing Timeout Reminder.";
      return;
    }
    var next = houses[0];
    var due = next.House.LastVisit == 0 ? DateTimeOffset.UtcNow : DateTimeOffset.FromUnixTimeSeconds(HousingTimer.GetOffset(next.House.LastVisit));
    var remaining = due - DateTimeOffset.UtcNow;
    var label = new SeStringBuilder();
    if (config.ColorServerBarUrgency && remaining.TotalDays <= 3) label.AddUiForeground((ushort)(remaining <= TimeSpan.Zero ? 17 : 31));
    label.AddIcon(BitmapFontIcon.Ishgard).AddText(" HT");
    if (config.ColorServerBarUrgency && remaining.TotalDays <= 3) label.AddUiForegroundOff();
    housingBar.Text = label.Build();
    housingBar.Tooltip = $"Next visit reminder: {next.Player.DisplayName}\n{next.Kind}: {next.House.District}, ward {next.House.Ward}, plot {next.House.Plot}\n" +
      (next.House.LastVisit == 0 ? "No interior visit recorded." : $"Last interior visit: {Config.FormatVisitDate(DateTimeOffset.FromUnixTimeSeconds(next.House.LastVisit))}\n" +
      (remaining <= TimeSpan.Zero ? "Visit reminder due now." : $"Reminder due in {Math.Ceiling(remaining.TotalDays)} day(s).")) +
      "\nBased on saved visits and your reminder interval, not Square Enix's demolition timer. Click to open.";
  }
  /// <summary>
  /// Checks the timers when necessary.
  /// </summary>
  internal static void CheckTimers(uint? territory = null) {
    if (territory is null && System.IsLoggedIn) {
      territory = Svc.ClientState.TerritoryType;
    }

    if (territory is null) {
      Svc.Log.Information("Territory is null");
      return;
    }

    HousingTimer.OnTerritoryChanged(territory.Value);
  }

  internal static bool IsWarningToBeDisplayed(uint? territory = null) {
    if (territory is null && System.IsLoggedIn) {
      territory = Svc.ClientState.TerritoryType;
    }

    if (territory is null) {
      return false;
    }

    return System.PluginConfig.PlayerConfigsWithWarnings.Count > 0;
  }

  /// <summary>
  /// The function to call when changing instance. Checks timers after.
  /// </summary>
  /// <param name="territoryId">The territory ID.</param>
  private void OnTerritoryChanged(uint territoryId) {
    // The framework callback reads game pointers after the loading screen clears.
    nextHousingCheck = 0;
  }

  /// <summary>
  /// The function to call when logging out.
  /// Unloads the housing timer.
  /// </summary>
  private static void OnLogout(int type, int code) {
    System.CachedCurrentPlayerId = null;
    // Do nothing for now.
  }

  /// <summary>
  /// In response to the slash command, just display our main UI
  /// </summary>
  /// <param name="command">The Command Name</param>
  /// <param name="args">The Arguments</param>
  private void OnCommand(string command, string args) {
    var argsParsed = !string.IsNullOrEmpty(args) ? args.Split(" ", StringSplitOptions.RemoveEmptyEntries).AsEnumerable().Last().ToLower() : string.Empty;
    if (argsParsed.Equals("check")) {
      CheckTimers();
    } else {
      System.SettingsUI.IsOpen ^= true;
    }
  }

  /// <summary>
  /// Draws the UI of the plugin.
  /// </summary>
  private void DrawUI() {
    System.WindowSystem.Draw();
    if (Repositioning) {
      System.WarningUI.IsOpen = true;
    } else {
      System.WarningUI.IsOpen = IsWarningToBeDisplayed();
    }
#if DEBUG
    System.DebugUI.IsOpen = System.SettingsUI.IsOpen;
#endif
  }

  /// <summary>
  /// Draws the UI of the settings menu of the plugin.
  /// </summary>
  public static void DrawConfigUI() {
    System.SettingsUI.IsOpen = true;
  }
}

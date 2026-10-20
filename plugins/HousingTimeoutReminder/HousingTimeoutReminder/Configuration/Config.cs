using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Configuration;
/// <summary>
/// The main plugin configuration file.
/// </summary>
[Serializable]
public class Config : IPluginConfiguration {
  internal const int CurrentVersion = 2;

  /// <summary>
  /// The version of the Configuration.
  /// </summary>
  public int Version { get; set; } = CurrentVersion;

  /// <summary>
  /// The list of player configurations.
  /// </summary>
  public List<PerPlayerConfig> PlayerConfigs { get; set; } = [];

  /// <summary>
  /// The list of player configurations.
  /// </summary>
  [JsonIgnore]
  public List<PerPlayerConfig> PlayerConfigsWithWarnings => [.. PlayerConfigs.Where(p => p.HasLateProperties && (ShowAllPlayers || p.IsCurrentPlayerConfig))];

  /// <summary>
  /// The configuration directory for the plugin.
  /// </summary>
  public static string ConfigDirectory => Svc.PluginInterface.GetPluginConfigDirectory();

  /// <summary>
  /// The default days to wait for when to notify the player that they haven't visited their property for some time.
  /// </summary>
  public ushort DaysToWait { get; set; } = 28;

  /// <summary>
  /// Gets the amount of days to wait in Unix Time (Seconds).
  /// </summary>
  public long DateToWaitSeconds => DateTimeOffset.MinValue.AddDays(DaysToWait).ToUnixTimeSeconds() - DateTimeOffset.MinValue.ToUnixTimeSeconds();

  /// <summary>
  /// The position of the warning dialog on the screen.
  /// </summary>
  public Position WarningPosition { get; set; } = new();

  /// <summary>
  /// Shows warnings for all players regardless of who is logged in.
  /// </summary>
  public bool ShowAllPlayers { get; set; } = true;
  public int ReminderRepeatHours { get; set; } = 1;
  public int SnoozeHours { get; set; } = 1;
  public List<VisitRecord> VisitHistory { get; set; } = [];
  public sealed class VisitRecord {
    public string Character { get; set; } = string.Empty;
    public string House { get; set; } = string.Empty;
    public long RecordedAt { get; set; }
  }
  public int DateFormat { get; set; }
  public static string FormatVisitDate(DateTimeOffset date) => date.ToLocalTime().ToString(
    System.PluginConfig.DateFormat switch {
      1 => "MM/dd/yyyy HH:mm:ss",
      2 => "yyyy-MM-dd HH:mm:ss",
      _ => "dd/MM/yyyy HH:mm:ss"
    }, global::System.Globalization.CultureInfo.InvariantCulture);
  public bool ShowServerBar { get; set; }
  public bool ColorServerBarUrgency { get; set; }
  public bool EchoVisitRecorded { get; set; } = true;
  public bool EnableFcReminders { get; set; } = true;


  public void ResetAll() {
    foreach (var playerConfig in PlayerConfigs) {
      playerConfig.ResetWarnings();
    }
  }

  /// <summary>
  /// Gets the current player config that exists.
  /// </summary>
  public static PerPlayerConfig? PlayerConfiguration => System.PluginConfig.PlayerConfigs.FirstOrDefault(x => x.PlayerID == System.GetCurrentPlayerID());

  /// <summary>
  /// Initializes the plugin config.
  /// </summary>
  public void Initialize() {
    if (DaysToWait > 30) {
      DaysToWait = 30;
    }

    if (DaysToWait < 1) {
      DaysToWait = 1;
    }

    switch (Version) {
      default: break;
    }
  }

  /// <summary>
  /// Saves the plugin config via the instanced <see cref="DalamudPluginInterface"/>.
  /// </summary>
  public void Save() {
#if DEBUG
    //Svc.Log.Info("Debug StackTrace:\n" + Environment.StackTrace);
#endif
    Svc.PluginInterface.SavePluginConfig(this);
  }

  /// <summary>
  /// Adds a new <see cref="PerPlayerConfig" /> to the player config list,
  /// from the currently logged in player.
  /// </summary>
  /// <returns>Returns the same new player config, else returns null if
  /// the player is not logged in.</returns>
  internal static PerPlayerConfig? AddNewPlayerFromCurrent() {
    if (System.GetCurrentPlayerID() is not PlayerID playerID) {
      return null;
    }

    var playerConfig = new PerPlayerConfig { PlayerID = playerID };
    System.PluginConfig.PlayerConfigs.Add(playerConfig);
    return playerConfig;
  }

  /// <summary>
  /// Adds a new <see cref="PerPlayerConfig" /> to the player config list,
  /// from a player identification.
  /// </summary>
  /// <param name="playerID">A player identification.</param>
  /// <returns>Returns the same new player config, else returns null if
  /// the player is not logged in.</returns>
  internal static PerPlayerConfig AddNewPlayerConfig(PlayerID playerID) {
    if (System.GetAuthoritativePlayerID() is not { } current || current != playerID)
      throw new InvalidOperationException("Only the verified logged-in character can be added automatically.");
    var legacy = System.PluginConfig.PlayerConfigs.FirstOrDefault(x => x.PlayerID is { HomeWorld: null } oldId && oldId.FirstLastName == playerID.FirstLastName);
    if (legacy is not null) {
      legacy.PlayerID = playerID;
      return legacy;
    }
    var playerConfig = new PerPlayerConfig { PlayerID = playerID };
    if (!System.PluginConfig.PlayerConfigs.Any(x => x.PlayerID == playerID)) {
      System.PluginConfig.PlayerConfigs.Add(playerConfig);
      return playerConfig;
    }

    return System.PluginConfig.PlayerConfigs.First(x => x.PlayerID == playerID);
  }

  /// <summary>
  /// Gets the <see cref="PerPlayerConfig" /> from a matching <see cref="PlayerID" />,
  /// if the player config doesn't exist it will create abstract new one from
  /// the current player config.
  /// </summary>
  /// <param name="playerID">A player identification.</param>
  /// <returns>Returns a player config for the current player config.</returns>
  internal static PerPlayerConfig GetPlayerConfig(PlayerID playerID) {
    return System.PluginConfig.PlayerConfigs.FirstOrDefault(x => x.PlayerID == playerID) ?? AddNewPlayerConfig(playerID);
  }

  [JsonExtensionData]
  [SuppressMessage("Roslynator", "RCS1169")]
  private IDictionary<string, JToken>? _additionalData = null;

  [OnDeserialized]
  private void OnDeserialized(StreamingContext context) {
    if (_additionalData is null) {
#if DEBUG
      Svc.Log.Warning("Config _additionalData is null");
#endif
      return;
    }
  }
}

public static class ConfigExtensions {
  /// <summary>
  /// Migrates the older config versions to newer ones.
  /// </summary>
  public static Config Migrate(this Config config) {
    if (config.Version < 0) config.Version = 0;
    if (config.Version == Config.CurrentVersion) return config;

    while (config.Version < Config.CurrentVersion) {
      switch (config.Version) {
        case 0:
          Svc.Log.Verbose("Migrating from v0.");
          config = config.MigrateToV1();
          break;
        case 1:
          config.ShowAllPlayers = true;
          config.Version = 2;
          Svc.Log.Verbose("Enabled reminders for saved alt characters.");
          break;
      }
    }

    Svc.Log.Info("Migrated Config.");
    return config;
  }

  /// <summary>
  /// A wrapper function to migrate from version 0 to version 1 of the config.
  /// </summary>
  public static Config MigrateToV1(this Config config) {
    /*for (int index = 0; index < config.PlayerConfigs.Count; index++) {
      switch (config.PlayerConfigs[index].OwnerName)
      {
        case string ownerName when config.PlayerConfigs[index].PlayerID is null:
          if (System.IsLoggedIn) {
            var playerID = System.GetCurrentPlayerID();
            config.PlayerConfigs[index].PlayerID = playerID ?? new PlayerID(ownerName);
          } else {
            config.PlayerConfigs[index].PlayerID = new PlayerID(ownerName);
          }
          config.PlayerConfigs[index].OwnerName = null;
          break;
        case null when config.PlayerConfigs[index].PlayerID is null:
          config.PlayerConfigs.RemoveAt(index);
          break;
        default:
          if (config.PlayerConfigs[index].IsBroken) {
            config.PlayerConfigs.RemoveAt(index);
          }
          break;
      }
    }*/
    config.Version = 1;
    return config;
  }

  /// <summary>
  /// Tries to upgrade old config names.
  /// </summary>
  internal static Config TryUpdateBrokenNames(this Config config) {
    if (System.GetCurrentPlayerID() is not PlayerID playerID) {
      return config;
    }

    if (!config.PlayerConfigs.Exists(x => x.PlayerID is not null && x.PlayerID.HomeWorld is null)) {
      return config;
    }

    int index = config.PlayerConfigs.FindIndex(x => x.PlayerID is not null && x.PlayerID.HomeWorld is null);
    config.PlayerConfigs[index].PlayerID = playerID;
    return config;
  }
}

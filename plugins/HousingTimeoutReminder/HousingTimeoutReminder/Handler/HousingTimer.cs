using System;
using System.Threading.Tasks;

using ECommons.DalamudServices;
using NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Configuration;

namespace NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Handler;
/// <summary>
/// A static housing timer function library.
/// </summary>
public static class HousingTimer {
  /// <summary>
  /// Gets the offset from two different <see cref="long" /> unix time stamps.
  /// </summary>
  /// <param name="lastVisit">The input unix time stamp.</param>
  /// <returns>The offset from the <see cref="lastVisit"/> with the amount of
  /// days added by <see cref="Config.DaysToWait"/>.</returns>
  public static long GetOffset(long lastVisit) {
    return DateTimeOffset.FromUnixTimeSeconds(lastVisit).AddDays(System.PluginConfig.DaysToWait).ToUnixTimeSeconds();
  }

  /// <summary>
  /// Checks the location of the player and returns <see langword="true"/> if successful.
  /// Inside House:
  ///     Apartment: null; ApartmentWing: null; Plot: 53; Ward: 26; Yard: null;
  /// On the Yard:
  ///     Apartment: null; ApartmentWing: null; Plot: null; Ward: 26; Yard: 53;
  /// </summary>
  /// <param name="playerID">An player identification instance.</param>
  /// <param name="territory">The ID for the territory the player is in.</param>
  /// <return>Returns <see langword="true"/> if successful.</return>
  public static bool CheckLocation(PlayerID playerID, uint territory) {
    try {
      if (System.GetAuthoritativePlayerID() is not { } current || current != playerID) return false;
      var playerConfig = Config.GetPlayerConfig(playerID);
      var loc = HousingManager.GetCurrentLocation(territory);
      if (loc?.IsInside != true) lastConfirmedVisit = null;
      if (loc is null || !CanRecordVisit(System.GetCurrentPlayerID(), playerID, loc.IsInside, loc.IsApartment, loc.Room,
          Svc.Objects.LocalPlayer?.CurrentWorld.RowId ?? 0, playerID.HomeWorld ?? 0)) return true;

      if (loc.Room == 0 && playerConfig.PrivateEstate.Enabled) {
        var privateEstate = HousingManager.From(playerID, HousingType.PrivateEstate);
        if (privateEstate?.Equals(loc) == true) {
          if (RecordVisit(playerConfig.PrivateEstate)) { Update(); ConfirmVisit(playerConfig, playerConfig.PrivateEstate, "personal estate"); }
          return true;
        }
      }

      if (loc.Room == 0 && playerConfig.FreeCompanyEstate.Enabled) {
        var freeCompanyEstate = HousingManager.From(playerID, HousingType.FreeCompanyEstate);
        if (freeCompanyEstate?.Equals(loc) == true) {
          var changed = false;
          foreach (var saved in System.PluginConfig.PlayerConfigs) {
            if (IsSameFcEstate(playerID, saved.PlayerID, playerConfig.FreeCompanyEstate, saved.FreeCompanyEstate))
              changed |= RecordVisit(saved.FreeCompanyEstate);
          }
          if (changed) { Update(); ConfirmVisit(playerConfig, playerConfig.FreeCompanyEstate, "FC estate"); }
          return true;
        }
      }
    } catch (Exception exception) {
      Svc.Log.Error(exception, "Failed to check timers.");
      return false;
    }

    return true;
  }

  private static string? lastConfirmedVisit;
  private static void ConfirmVisit(PerPlayerConfig player, HousingPlot house, string kind) {
    var key = $"{player.DisplayName}:{kind}:{house.District}:{house.Ward}:{house.Plot}";
    if (lastConfirmedVisit == key) return;
    lastConfirmedVisit = key;
    System.PluginConfig.VisitHistory.Add(new Config.VisitRecord { Character = player.DisplayName, House = $"{kind}, {house.District}, ward {house.Ward}, plot {house.Plot}", RecordedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
    if (System.PluginConfig.VisitHistory.Count > 500) System.PluginConfig.VisitHistory.RemoveRange(0, System.PluginConfig.VisitHistory.Count - 500);
    System.PluginConfig.Save();
    if (!System.PluginConfig.EchoVisitRecorded) return;
    Svc.Chat.Print(new Dalamud.Game.Text.XivChatEntry {
      Type = Dalamud.Game.Text.XivChatType.Echo,
      Message = new Dalamud.Game.Text.SeStringHandling.SeStringBuilder().AddText(
        $"[Housing Timeout Reminder] Interior visit recorded: {player.DisplayName}, {kind}, {house.District}, ward {house.Ward}, plot {house.Plot}.").Build()
    });
  }
  private static bool RecordVisit(WardProperty property) {
    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    if (now - property.LastVisit < 60) return false;
    property.LastVisit = now;
    property.IsDismissed = false;
    return true;
  }

  internal static bool IsSameFcEstate(PlayerID current, PlayerID? other, HousingPlot currentHouse, HousingPlot otherHouse) =>
    current.HomeWorld is > 0 && current.HomeWorld == other?.HomeWorld && currentHouse.IsValid && otherHouse.IsValid &&
    currentHouse.District == otherHouse.District && currentHouse.Ward == otherHouse.Ward && currentHouse.Plot == otherHouse.Plot;

  internal static bool CanRecordVisit(PlayerID? current, PlayerID owner, bool? inside, bool apartment, short room,
      uint currentWorld, uint homeWorld) =>
    current is not null && current == owner && homeWorld != 0 && currentWorld == homeWorld && inside == true && !apartment && room == 0;

  /// <summary>
  /// The function to call when changing instance. Checks timers after.
  /// </summary>
  /// <param name="territory">The territory ID.</param>
  public static void OnTerritoryChanged(uint territory) {
    PlayerID? playerID = System.GetCurrentPlayerID();

    if (playerID is not null) {
#if DEBUG
      Svc.Log.Warning("playerID is not null");
#endif
      CheckLocation(playerID, territory);
    }
#if DEBUG
    Svc.Log.Debug(playerID is null
      ? $"{nameof(System.GetCurrentPlayerID)} returned null."
      : $"{nameof(System.GetCurrentPlayerID)} returned typeof {nameof(PlayerID)}.");
#endif
  }

  /// <summary>
  /// Manually checks the ability to check for housing times when changing
  /// territory.
  /// </summary>
  /// <param name="playerID">An player identification instance.</param>
  /// <param name="territory">The territory id.</param>
  /// <returns><see langword="true"/> if successful and ready,
  /// otherwise <see langword="false"/>.</returns>
  public static bool ManualCheck(PlayerID playerID, uint territory) {
    try {
      return CheckLocation(playerID, territory);
    } catch (OperationCanceledException operationCanceledException) {
      Svc.Log.Error(operationCanceledException, "Error when waiting for task to complete.");
      return false;
    } catch (Exception exception) {
      Svc.Log.Error(exception, "Failed to run task to manually check the housing timer.");
      return false;
    }
  }

  /// <summary>Saves from the game framework or UI thread.</summary>
  internal static void Update() => System.PluginConfig.Save();
}

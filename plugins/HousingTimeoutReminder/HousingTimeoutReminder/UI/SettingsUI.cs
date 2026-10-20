using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using ECommons.DalamudServices;

using Dalamud.Bindings.ImGui;
using NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Configuration;
using NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Extensions;
using NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Handler;

namespace NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.UI;

/// <summary>
/// The class for the settings of the plugin.
/// </summary>
public class SettingsUI : Window, IDisposable {
  private PerPlayerConfig? pendingCharacterRemoval;
  private HousingPlot? pendingOverviewRemoval;
  private string pendingOverviewRemovalName = string.Empty;
  private HousingPlot? pendingTrackingRemoval;
  private string pendingTrackingRemovalName = string.Empty;
  private string backupImportPath = string.Empty;
  private string backupStatus = string.Empty;
  private global::System.Collections.Generic.List<PerPlayerConfig>? housingImportPreview;
  /// <summary>
  /// The default window flags of the settings ui.
  /// We disable the scroll with mouse because we have it layed out to have
  /// fixed elements.
  /// </summary>
  private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoCollapse;

  /// <summary>
  /// The name of the window.
  /// </summary>
  public static string Name => "Housing Timeout Reminder Settings";

  /// <summary>
  /// The default constructor.
  /// </summary>
  public SettingsUI() : base(Name, WindowFlags) {
    Size = ImGuiHelpers.ScaledVector2(630.0f, 550.0f);
    SizeCondition = ImGuiCond.FirstUseEver;
  }

  /// <summary>
  /// Disposal of this class on unload.
  /// </summary>
  public void Dispose() {
    GC.SuppressFinalize(this);
  }

  /// <summary>
  /// The max number of wards.
  /// TODO: Update ward counts when necessary.
  /// </summary>
  private const int WardMax = 32;

  /// <summary>
  /// The max number of plots.
  /// </summary>
  private const int PlotMax = 60;

  private string housingDetectionStatus = string.Empty;

  // ReSharper disable once MemberCanBeMadeStatic.Local
  /// <summary>
  /// A function to shorten the repetitiveness of <see cref="CheckConsistency"/>
  /// Gets the new date time offset required for when the housing plot is outside of the check date.
  /// </summary>
  /// <param name="playerConfig">The player config to check against.</param>
  /// <param name="type">The type of housing to check.</param>
  /// <param name="next">If to check for the next limit date.</param>
  /// <param name="now">If to check for the date from now.</param>
  /// <returns>The offset date from the last visit of <see cref="type"/>.</returns>
  private static DateTimeOffset ShortenFunction(PerPlayerConfig playerConfig, HousingType type, bool next = false, bool now = false) {
    long visit = type switch {
      HousingType.FreeCompanyEstate => playerConfig.FreeCompanyEstate.LastVisit,
      HousingType.PrivateEstate => playerConfig.PrivateEstate.LastVisit,
      _ => 0
    };

    var date = DateTimeOffset.FromUnixTimeSeconds(visit);

    if (next) {
      return date.AddDays(System.PluginConfig.DaysToWait);
    }

    if (now) {
      return DateTimeOffset.Now;
    }

    return date;
  }

  /// <summary>
  /// Gets the new date time offset required for when the housing plot is outside of the check date.
  /// </summary>
  /// <param name="playerConfig">The player config to check against.</param>
  /// <param name="type">The type of housing to check.</param>
  /// <returns>A <see cref="Tuple{String, String}"/> with the last check formatted timestamp,
  /// and the next timelimit formatted timestamp.</returns>
  private static (string Last, string Next) CheckConsistency(PerPlayerConfig playerConfig, HousingType type) {
    var lastStamp = ShortenFunction(playerConfig, type);
    var nextStamp = ShortenFunction(playerConfig, type, next: true);

    if (lastStamp.ToUnixTimeSeconds() <= 946627200 && nextStamp.ToUnixTimeSeconds() <= 946627200) {
      return (Last: "Never", Next: "Now");
    }

    if (lastStamp <= nextStamp && nextStamp >= ShortenFunction(playerConfig, type, now: true)) {
      return (Last: Config.FormatVisitDate(lastStamp), Next: Config.FormatVisitDate(nextStamp));
    }

    if (nextStamp <= ShortenFunction(playerConfig, type, now: true)) {
      return (Last: Config.FormatVisitDate(lastStamp), Next: "Now");
    }

    return (Last: "Never", Next: "Now");
  }

  public float GetMaxWidth() {
    var windowSize = 128f;
    if (this.Size.HasValue) {
      windowSize = this.Size.Value.X - ImGui.GetStyle().WindowPadding.X;
    }

    return windowSize;// ImGuiHelpers.ScaledVector2(windowSize, 0).X;
  }

  /// <summary>
  /// Draw the housing config dropdown based on a <see cref="HousingType"/>
  /// </summary>
  /// <param name="playerConfig">The player config to check against.</param>
  /// <param name="type">The type of housing to check.</param>
  /// <param name="indent">Indenting check to make the child object better formatted.</param>
  public bool DrawHousingDropDown(PerPlayerConfig playerConfig, HousingType type, float indent = 0f) {
    var playerId = playerConfig.DisplayName;
    ImGui.PushID($"{type}-{playerId}");
    try {
      if (!ImGui.CollapsingHeader(HousingTypeEnumHelper.GetDescriptor(type), ImGuiTreeNodeFlags.DefaultOpen)) return true;
      var property = playerConfig.GetOfType(type);
      if (property is HousingPlot { RemovedFromOverview: true } removedHouse) {
        ImGui.TextWrapped("This house was removed from the overview.");
        if (ImGui.Button("Restore to overview")) {
          removedHouse.RemovedFromOverview = false;
          System.PluginConfig.Save();
        }
        return true;
      }
      if (type == HousingType.FreeCompanyEstate) {
        ImGui.TextWrapped(playerConfig.FcEstateAccess switch {
          true => $"FC rank: {playerConfig.FcRankName}. Estate access allowed.",
          false => $"FC rank: {playerConfig.FcRankName}. No estate access; FC reminders are ignored.",
          _ => "FC estate permission unknown. Open the game's FC Members tab once to read your rank permissions.",
        });
      }
      if (property.IsValid) {
        ImGui.Text($"{DistrictEnumHelper.GetDescriptor(property.District)} - Ward {property.Ward}, Plot {property.Plot}");
        var (last, next) = CheckConsistency(playerConfig, type);
        ImGui.Text($"Last inside visit: {(last == "Never" ? "Not recorded" : last)}");
        ImGui.Text($"Reminder: {(next == "Now" ? "Visit needed" : next)}");
      } else {
        ImGui.TextWrapped("No house detected. Use Detect my housing on this character, or enter an address below.");
      }
      var enabled = property.Enabled;
      if (ImGui.Checkbox("Enable reminders", ref enabled)) {
        property.Enabled = enabled;
        System.PluginConfig.Save();
      }
      var location = playerConfig.IsCurrentPlayerConfig ? Handler.HousingManager.GetCurrentLocation(Svc.ClientState.TerritoryType) : null;
      var canRecord = location is not null && HousingTimer.CanRecordVisit(System.GetCurrentPlayerID(), playerConfig.PlayerID!, location.IsInside, location.IsApartment, location.Room,
          Svc.Objects.LocalPlayer?.CurrentWorld.RowId ?? 0, playerConfig.PlayerID?.HomeWorld ?? 0)
        && Handler.HousingManager.From(playerConfig.PlayerID!, type)?.Equals(location) == true && property.Enabled;
      ImGui.BeginDisabled(!canRecord);
      if (ImGui.Button("Record visit now")) Plugin.CheckTimers();
      ImGui.EndDisabled();
      ImGui.SameLine();
      if (ImGui.Button("Clear visit history")) {
        property.LastVisit = 0;
        if (property is WardProperty wardProperty) wardProperty.IsDismissed = false;
        System.PluginConfig.Save();
      }
      if (!canRecord) ImGui.TextDisabled("Record a visit by entering this house on its character.");
      using (var manual = ImRaii.TreeNode("Edit address manually")) {
        if (manual.Success) {
          var changed = false;
          ImGui.SetNextItemWidth(200);
          using (var combo = ImRaii.Combo("District", DistrictEnumHelper.GetDescriptor(property.District))) {
            if (combo.Success) {
              foreach (var (name, district) in DistrictEnumHelper.GetDescriptors()) {
                if (ImGui.Selectable(name, property.District == district)) { property.District = district; changed = true; }
              }
            }
          }
          int ward = property.Ward, plot = property.Plot;
          ImGui.SetNextItemWidth(140);
          if (ImGuiRaii.InputIntMinMax("Ward", ref ward, 1, 5, 1, WardMax)) { property.Ward = (sbyte)ward; changed = true; }
          ImGui.SetNextItemWidth(140);
          if (ImGuiRaii.InputIntMinMax("Plot", ref plot, 1, 5, 1, PlotMax)) { property.Plot = (sbyte)plot; changed = true; }
          if (changed) {
            property.LastVisit = 0;
            if (property is WardProperty wardProperty) wardProperty.IsDismissed = false;
            System.PluginConfig.Save();
          }
        }
      }
      ImGui.Separator();
      return true;
    } catch (Exception exception) {
      Svc.Log.Error(exception, "Failed to draw housing settings.");
      return false;
    } finally { ImGui.PopID(); }
  }
  /// <summary>
  /// Draws player timeout settings for a single user.
  /// </summary>
  /// <param name="playerConfig">The player config involved with the single player.</param>
  /// <param name="indent">Indenting check to make the child object better formatted.</param>
  public void DrawUserTimeoutSettings(PerPlayerConfig playerConfig, float indent = 0f) {
    var playerId = playerConfig.DisplayName;
    if (System.PluginConfig.EnableFcReminders && !DrawHousingDropDown(playerConfig, HousingType.FreeCompanyEstate, indent + ScaledIndent5)) {
      ImGui.Text($"Failed to draw {HousingTypeEnumHelper.GetDescriptor(HousingType.FreeCompanyEstate).ToLower()} tab.");
    }

    if (!DrawHousingDropDown(playerConfig, HousingType.PrivateEstate, indent + ScaledIndent5)) {
      ImGui.Text($"Failed to draw {HousingTypeEnumHelper.GetDescriptor(HousingType.PrivateEstate).ToLower()} tab.");
    }

    // ReSharper disable once InvertIf
    if (ImGui.Button($"Show dismissed reminders again##Reset-{playerId}")) {
      Plugin.CheckTimers();
      playerConfig.ResetWarnings();
    }
  }

  public bool DrawGlobalTab() {
    try {
      using (var scrolling = ImRaii.Child($"scrolling##HousingTimeoutReminder-{nameof(DrawGlobalTab)}", ImGuiHelpers.ScaledVector2(0, -(25 + ImGui.GetStyle().ItemSpacing.Y)), false)) {
        if (!scrolling.Success) {
          return true;
        }

        ImGui.Text("Remind me after this many days without an inside visit");
        ImGui.SameLine();
        using (var i = ImRaii.ItemWidth(100)) {
          if (i.Count > 0) {
            var daysToWait = (int)System.PluginConfig.DaysToWait;

            if (ImGuiRaii.InputIntMinMax("##GlobalDaysToWait", ref daysToWait, 1, 5, 1, 30)) {
              System.PluginConfig.DaysToWait = (ushort)daysToWait;
            }
          }
        }

        var displayAllPlayers = System.PluginConfig.ShowAllPlayers;

        if (ImGui.Checkbox("Show reminders for all saved characters##GlobalShowAllPlayers", ref displayAllPlayers)) {
          System.PluginConfig.ShowAllPlayers = displayAllPlayers;
        }
        var repeatHours = System.PluginConfig.ReminderRepeatHours;
        ImGui.TextUnformatted("Repeat dismissed reminders after (hours)");
        ImGui.SetNextItemWidth(160);
        if (ImGui.InputInt("##RepeatHours", ref repeatHours)) { System.PluginConfig.ReminderRepeatHours = Math.Clamp(repeatHours, 1, 168); System.PluginConfig.Save(); }
        var snoozeHours = System.PluginConfig.SnoozeHours;
        ImGui.TextUnformatted("Snooze duration (hours)");
        ImGui.SetNextItemWidth(160);
        if (ImGui.InputInt("##SnoozeHours", ref snoozeHours)) { System.PluginConfig.SnoozeHours = Math.Clamp(snoozeHours, 1, 168); System.PluginConfig.Save(); }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Snooze hides reminders for the selected house for this many hours. It does not record a visit or change the demolition timer.");
        var dateFormat = System.PluginConfig.DateFormat;
        ImGui.SetNextItemWidth(240);
        if (ImGui.Combo("Date format", ref dateFormat, "Day / Month / Year\0Month / Day / Year\0Year / Month / Day\0")) {
          System.PluginConfig.DateFormat = dateFormat;
          System.PluginConfig.Save();
        }
        ImGui.TextDisabled($"Example: {Config.FormatVisitDate(DateTimeOffset.Now)} (local time)");
        var showBar = System.PluginConfig.ShowServerBar;
        if (ImGui.Checkbox("Show HT server-bar button", ref showBar)) { System.PluginConfig.ShowServerBar = showBar; System.PluginConfig.Save(); }
        var colorBar = System.PluginConfig.ColorServerBarUrgency;
        if (ImGui.Checkbox("Color HT by reminder urgency", ref colorBar)) { System.PluginConfig.ColorServerBarUrgency = colorBar; System.PluginConfig.Save(); }
        ImGui.TextWrapped("Yellow: reminder within 3 days. Red: reminder due or no visit recorded. These are your reminder dates, not the game's demolition timer.");
        var echoVisit = System.PluginConfig.EchoVisitRecorded;
        if (ImGui.Checkbox("Echo when an interior visit is recorded", ref echoVisit)) { System.PluginConfig.EchoVisitRecorded = echoVisit; System.PluginConfig.Save(); }
        if (ImGui.Button("Export houses and visits")) {
          try {
            var directory = Config.ConfigDirectory;
            global::System.IO.Directory.CreateDirectory(directory);
            var path = global::System.IO.Path.Combine(directory, $"housing-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            global::System.IO.File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(System.PluginConfig.PlayerConfigs, Newtonsoft.Json.Formatting.Indented));
            Svc.Chat.Print($"Housing backup saved: {path}");
          } catch (Exception ex) { Svc.Log.Error(ex, "Housing backup failed"); }
        }
        ImGui.SameLine();
        if (ImGui.Button("Open backup folder")) Dalamud.Utility.Util.OpenLink(Config.ConfigDirectory);
        ImGui.TextWrapped("Export contains all saved characters, house addresses and visit dates. Move the JSON backup to your new PC; it contains character names and home worlds.");
        ImGui.InputText("Backup JSON path", ref backupImportPath, 1024);
        if (ImGui.Button("Preview housing import")) {
          try {
            var file = new global::System.IO.FileInfo(backupImportPath.Trim().Trim('"'));
            if (!file.Exists || file.Length > 5 * 1024 * 1024) throw new InvalidOperationException("Select an existing backup JSON smaller than 5 MB.");
            var entries = Newtonsoft.Json.JsonConvert.DeserializeObject<global::System.Collections.Generic.List<PerPlayerConfig>>(
              global::System.IO.File.ReadAllText(file.FullName)) ?? throw new InvalidOperationException("Invalid backup.");
            if (entries.Count > 1000 || entries.Any(entry => entry is null || entry.PlayerID is null || entry.PrivateEstate is null || entry.FreeCompanyEstate is null))
              throw new InvalidOperationException("Backup contains invalid character records.");
            housingImportPreview = entries;
            backupStatus = $"Preview ready: {entries.Count} character(s). Nothing imported yet.";
          } catch (Exception ex) { housingImportPreview = null; backupStatus = $"Preview failed: {ex.Message}"; }
        }
        if (housingImportPreview is { } preview) {
          ImGui.TextWrapped("Merge adds missing houses and keeps the newest visit for matching addresses. Conflicting existing addresses are preserved.");
          foreach (var player in preview) {
            ImGui.TextWrapped(player.DisplayName);
            foreach (var entry in new[] { (House: player.PrivateEstate, Name: "Personal"), (House: player.FreeCompanyEstate, Name: "FC") }) {
              if (!entry.House.IsValid) continue;
              ImGui.TextWrapped($"{entry.Name}: {entry.House.District}, ward {entry.House.Ward}, plot {entry.House.Plot} · last visit: {(entry.House.LastVisit > 0 ? Config.FormatVisitDate(DateTimeOffset.FromUnixTimeSeconds(entry.House.LastVisit)) : "none recorded")}");
            }
          }
          if (ImGui.Button("Cancel import preview")) housingImportPreview = null;
          ImGui.SameLine();
          if (ImGui.Button("Import previewed houses (merge)")) {
          try {
            var entries = preview;
            global::System.IO.Directory.CreateDirectory(Config.ConfigDirectory);
            var safety = global::System.IO.Path.Combine(Config.ConfigDirectory, $"housing-before-import-{DateTime.Now:yyyyMMdd-HHmmssfff}.json");
            global::System.IO.File.WriteAllText(safety, Newtonsoft.Json.JsonConvert.SerializeObject(System.PluginConfig.PlayerConfigs, Newtonsoft.Json.Formatting.Indented));
            var added = 0;
            foreach (var entry in entries) {
              var existing = System.PluginConfig.PlayerConfigs.FirstOrDefault(player => player.PlayerID == entry.PlayerID);
              if (existing is null) { System.PluginConfig.PlayerConfigs.Add(entry); added++; }
              else {
                foreach (var pair in new[] { (Current: existing.PrivateEstate, Imported: entry.PrivateEstate), (Current: existing.FreeCompanyEstate, Imported: entry.FreeCompanyEstate) }) {
                  if (!pair.Imported.IsValid) continue;
                  if (!pair.Current.IsValid) {
                    pair.Current.District = pair.Imported.District; pair.Current.Ward = pair.Imported.Ward;
                    pair.Current.Plot = pair.Imported.Plot; pair.Current.Enabled = pair.Imported.Enabled;
                    pair.Current.LastVisit = pair.Imported.LastVisit;
                  } else if (pair.Current.District == pair.Imported.District && pair.Current.Ward == pair.Imported.Ward && pair.Current.Plot == pair.Imported.Plot)
                    pair.Current.LastVisit = Math.Max(pair.Current.LastVisit, pair.Imported.LastVisit);
                }
              }
            }
            System.PluginConfig.Save();
            housingImportPreview = null;
            backupStatus = $"Imported {added} new character(s). Latest matching visits retained; conflicting addresses preserved. Safety backup: {safety}";
          } catch (Exception ex) { backupStatus = $"Import failed: {ex.Message}"; Svc.Log.Error(ex, "Housing backup import failed"); }
        }
          }
        if (!string.IsNullOrEmpty(backupStatus)) ImGui.TextWrapped(backupStatus);
        var enableFc = System.PluginConfig.EnableFcReminders;
        if (ImGui.Checkbox("Enable FC estate reminders", ref enableFc)) {
          System.PluginConfig.EnableFcReminders = enableFc;
          System.PluginConfig.Save();
          System.WarningUI.Pagination.WrapPages();
        }
        ImGui.TextWrapped("Turn this off to hide FC reminders for every saved character. Personal-house reminders are unaffected.");
      }
    } catch (Exception ex) {
      Svc.Log.Error(ex, nameof(DrawGlobalTab));
      return false;
    }

    return true;
  }

  public bool DrawCurrentPlayerTab() {
    try {
      using (var scrolling = ImRaii.Child($"scrolling##HousingTimeoutReminder-{nameof(DrawCurrentPlayerTab)}", ImGuiHelpers.ScaledVector2(0, -(25 + ImGui.GetStyle().ItemSpacing.Y)), false)) {
        if (!scrolling.Success) {
          return true;
        }

        if (System.IsLoggedIn && Config.PlayerConfiguration is not null) {
          ImGui.Text($"Housing Configuration for {Config.PlayerConfiguration.DisplayName}:");
          ImGui.TextWrapped("Owned housing is detected automatically. If details are missing, open your Teleport list and try detection again.");
          if (ImGui.Button("Detect my housing")) {
            var count = Handler.HousingManager.DetectOwnedHousing(true);
            housingDetectionStatus = count > 0
              ? $"Detected {count} owned housing location(s). Visit times are recorded when you enter the property."
              : "No owned housing data is available yet. Open Teleport on your home world, then try again.";
          }
          if (!string.IsNullOrEmpty(housingDetectionStatus)) ImGui.TextWrapped(housingDetectionStatus);
          DrawUserTimeoutSettings(Config.PlayerConfiguration);
        }
      }
    } catch (Exception ex) {
      Svc.Log.Error(ex, nameof(DrawCurrentPlayerTab));
      return false;
    }

    return true;
  }

  public static float ScaledIndent5 => ImGuiHelpers.ScaledVector2(5.0f).X;

  public bool DrawOtherPlayersTab() {
    try {
      using (var scrolling = ImRaii.Child($"scrolling##HousingTimeoutReminder-{nameof(DrawOtherPlayersTab)}", ImGuiHelpers.ScaledVector2(0, -(25 + ImGui.GetStyle().ItemSpacing.Y)), false)) {
        if (!scrolling.Success) {
          return true;
        }

#if DEBUG
        ImGui.Text($"System.PluginConfig.PlayerConfigs.Count: {System.PluginConfig.PlayerConfigs.Count}");
#endif

        ImGui.TextWrapped("Each character is remembered after you log into it with this plugin enabled. Its saved housing reminders can appear while you play another character.");
        foreach (var config in System.PluginConfig.PlayerConfigs.Where(config => !config.IsCurrentPlayerConfig)) {
          using (var header = ImRaii.TreeNode($"Housing Configuration for {config.DisplayName}", ImGuiTreeNodeFlags.Framed | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.NoTreePushOnOpen)) {
            if (header.Success) {
              ImGui.Indent(ScaledIndent5);
              DrawUserTimeoutSettings(config, ScaledIndent5);
              if (ImGui.Button("Remove saved character")) pendingCharacterRemoval = config;
              ImGui.Indent(-ScaledIndent5);
            }
          }
        }
      }
    } catch (Exception ex) {
      Svc.Log.Error(ex, nameof(DrawOtherPlayersTab));
      return false;
    }

    return true;
  }

  /// <inheritdoc />
  public override void Draw() {
    if (pendingCharacterRemoval is not null) ImGui.OpenPopup("Remove saved character?");
    if (ImGui.BeginPopupModal("Remove saved character?", ImGuiWindowFlags.AlwaysAutoResize)) {
      ImGui.TextWrapped(pendingCharacterRemoval?.DisplayName ?? string.Empty);
      ImGui.TextWrapped("Delete this character's saved houses and reminders? Visit history is kept. This cannot be undone without a backup.");
      if (ImGui.Button("Remove saved character")) {
        if (pendingCharacterRemoval is not null) System.PluginConfig.PlayerConfigs.Remove(pendingCharacterRemoval);
        System.PluginConfig.Save();
        pendingCharacterRemoval = null;
        ImGui.CloseCurrentPopup();
      }
      ImGui.SameLine();
      if (ImGui.Button("Cancel")) { pendingCharacterRemoval = null; ImGui.CloseCurrentPopup(); }
      ImGui.EndPopup();
    }
    if (pendingOverviewRemoval is not null) ImGui.OpenPopup("Remove house from overview?");
    if (ImGui.BeginPopupModal("Remove house from overview?", ImGuiWindowFlags.AlwaysAutoResize)) {
      ImGui.TextWrapped(pendingOverviewRemovalName);
      ImGui.TextWrapped("Delete this house's saved address and visit timestamp and stop its reminders? Visit history is kept. The house stays hidden until restored in character settings.");
      if (ImGui.Button("Remove from overview")) {
        pendingOverviewRemoval!.Reset();
        pendingOverviewRemoval.NextReminderAt = pendingOverviewRemoval.SnoozedUntil = 0;
        pendingOverviewRemoval.RemovedFromOverview = true;
        System.PluginConfig.Save();
        pendingOverviewRemoval = null;
        ImGui.CloseCurrentPopup();
      }
      ImGui.SameLine();
      if (ImGui.Button("Cancel")) { pendingOverviewRemoval = null; ImGui.CloseCurrentPopup(); }
      ImGui.EndPopup();
    }
    if (pendingTrackingRemoval is not null) ImGui.OpenPopup("Remove house from tracking?");
    if (ImGui.BeginPopupModal("Remove house from tracking?", ImGuiWindowFlags.AlwaysAutoResize)) {
      ImGui.TextWrapped(pendingTrackingRemovalName);
      ImGui.TextWrapped("Stop tracking this house? Its address and recorded visits will be kept. Enable reminders again in character settings to resume tracking.");
      if (ImGui.Button("Remove from tracking")) {
        pendingTrackingRemoval!.Enabled = false;
        System.PluginConfig.Save();
        pendingTrackingRemoval = null;
        ImGui.CloseCurrentPopup();
      }
      ImGui.SameLine();
      if (ImGui.Button("Cancel")) { pendingTrackingRemoval = null; ImGui.CloseCurrentPopup(); }
      ImGui.EndPopup();
    }
    if (DiscordJoinButton.Draw()) Dalamud.Utility.Util.OpenLink("https://discord.gg/TTPZ82xaUd");
    using (var tabBar = ImRaii.TabBar("ConfigTabs")) {
      if (tabBar.Success) {
        if (System.IsLoggedIn) {
          using (var tabItem = ImRaii.TabItem("Current character")) {
            if (tabItem.Success && !DrawCurrentPlayerTab()) {
              ImGui.Text("Failed to draw current player settings tab.");
            }
          }
        }

        using (var tabItem = ImRaii.TabItem("Saved characters")) {
          if (tabItem.Success && !DrawOtherPlayersTab()) {
            ImGui.Text("Failed to draw other players settings tab.");
          }
        }
        using (var overview = ImRaii.TabItem("House overview")) {
          if (overview.Success) {
            ImGui.TextWrapped("All saved houses. Ages and reminders use recorded interior visits; they are not the game's demolition countdown.");
            if (ImGui.BeginTable("HouseOverview", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.ScrollX | ImGuiTableFlags.Resizable, new Vector2(0, Math.Max(200, ImGui.GetContentRegionAvail().Y - 100)))) {
              ImGui.TableSetupColumn("Character / server", ImGuiTableColumnFlags.WidthFixed, 180);
              ImGui.TableSetupColumn("House", ImGuiTableColumnFlags.WidthFixed, 180);
              ImGui.TableSetupColumn("Last interior visit", ImGuiTableColumnFlags.WidthFixed, 170);
              ImGui.TableSetupColumn("Days since visit", ImGuiTableColumnFlags.WidthFixed, 100);
              ImGui.TableSetupColumn("Tracking status", ImGuiTableColumnFlags.WidthStretch);
              ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 180);
              ImGui.TableHeadersRow();
              foreach (var player in System.PluginConfig.PlayerConfigs)
                foreach (var entry in new[] { (House: player.PrivateEstate, Fc: false), (House: player.FreeCompanyEstate, Fc: true) }) {
                  var house = entry.House;
                  if (house.RemovedFromOverview || !house.IsValid) continue;
                  var status = !house.IsValid ? "Not detected: log in on this character and enter its estate." : !house.Enabled ? "Reminders disabled" :
                    entry.Fc && !System.PluginConfig.EnableFcReminders ? "FC reminders disabled globally" : entry.Fc && player.FcEstateAccess == false ? "FC rank lacks estate access" :
                    entry.Fc && player.FcEstateAccess is null ? "FC access unknown: log in to check" : house.LastVisit == 0 ? "No interior visit recorded" :
                    house.SnoozedUntil > DateTimeOffset.UtcNow.ToUnixTimeSeconds() ? "Snoozed" : "Tracking";
                  ImGui.TableNextRow();
                  ImGui.TableNextColumn(); ImGui.TextWrapped(player.DisplayName);
                  ImGui.TableNextColumn(); ImGui.TextWrapped($"{(entry.Fc ? "FC" : "Personal")}: {house.District}, ward {house.Ward}, plot {house.Plot}");
                  ImGui.TableNextColumn(); ImGui.TextWrapped(house.LastVisit > 0 ? Config.FormatVisitDate(DateTimeOffset.FromUnixTimeSeconds(house.LastVisit)) : "Never recorded");
                  ImGui.TableNextColumn(); ImGui.TextUnformatted(house.LastVisit > 0 ? Math.Floor(Math.Max(0, (DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(house.LastVisit)).TotalDays)).ToString("0") : "—");
                  ImGui.TableNextColumn(); ImGui.TextWrapped(status);
                  ImGui.TableNextColumn();
                  ImGui.PushID($"{player.DisplayName}-{entry.Fc}");
                  ImGui.BeginDisabled(!house.Enabled);
                  if (ImGui.Button("Remove from tracking")) {
                    pendingTrackingRemoval = house;
                    pendingTrackingRemovalName = $"{player.DisplayName}: {(entry.Fc ? "FC" : "Personal")} house, {house.District}, ward {house.Ward}, plot {house.Plot}";
                  }
                  ImGui.EndDisabled();
                  if (ImGui.Button("Remove from overview")) {
                    pendingOverviewRemoval = house;
                    pendingOverviewRemovalName = $"{player.DisplayName}: {(entry.Fc ? "FC" : "Personal")} house, {house.District}, ward {house.Ward}, plot {house.Plot}";
                  }
                  ImGui.PopID();
                }
              ImGui.EndTable();
            }
          }
        }
        using (var history = ImRaii.TabItem("Visit history")) {
          if (history.Success) {
            ImGui.TextWrapped("Up to 500 recorded visit confirmations. Older visits before this feature are not reconstructed.");
            if (ImGui.BeginChild("VisitHistory", new Vector2(0, Math.Max(200, ImGui.GetContentRegionAvail().Y - 100)))) {
              foreach (var visit in System.PluginConfig.VisitHistory.OrderByDescending(entry => entry.RecordedAt))
                ImGui.TextWrapped($"{Config.FormatVisitDate(DateTimeOffset.FromUnixTimeSeconds(visit.RecordedAt))} · {visit.Character} · {visit.House}");
            }
            ImGui.EndChild();
          }
        }
        using (var tabItem = ImRaii.TabItem("Settings")) {
          if (tabItem.Success) {
            if (ImGui.BeginChild("GlobalSettingsScroll", new Vector2(0, Math.Max(200, ImGui.GetContentRegionAvail().Y - 100)))) {
              if (!DrawGlobalTab()) ImGui.Text("Failed to draw global settings tab.");
            }
            ImGui.EndChild();
          }
        }
      }
    }

    if (ImGui.Button("Save")) {
      HousingTimer.Update();
    }

    ImGui.SameLine();

    if (ImGui.Button("Save and close")) {
      HousingTimer.Update();
      this.IsOpen = false;
    }

    ImGui.Separator();
    var reposition = System.PluginInstance.Repositioning;
    if (ImGui.Checkbox("Move reminder window", ref reposition)) System.PluginInstance.Repositioning = reposition;
    if (ImGui.Button("Show dismissed reminders again")) {
      Plugin.CheckTimers();
      System.PluginConfig.ResetAll();
    }

#if DEBUG
    ImGuiRaii.VerticalSeparator();

    if (ImGui.Button("Debug")) {
      System.DebugUI.IsOpen = true;
    }
#endif
  }
}

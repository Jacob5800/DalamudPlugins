using System;

using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

using ECommons.DalamudServices;

using FFXIVClientStructs.FFXIV.Common.Math;

using Dalamud.Bindings.ImGui;

using NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Configuration;
using NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Handler;

namespace NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.UI;

/// <summary>
/// The class for the warning ui of the plugin.
/// </summary>
public class WarningUI : Window, IDisposable {
  /// <summary>
  /// The default window flags of the settings ui.
  /// </summary>
  private const ImGuiWindowFlags WindowFlags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse |
    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

  /// <summary>
  /// An instance of the <see cref="Pagination" /> to implement pages in the warning dialog.
  /// </summary>
  internal readonly Pagination Pagination = new();

  private const float INITIAL_WINDOW_HEIGHT = 130f;

  private float WindowHeight = INITIAL_WINDOW_HEIGHT;

  /// <summary>
  /// The name of the window.
  /// </summary>
  public static string Name { get => "Housing Timeout Reminder Warning"; }

  /// <summary>
  /// The default constructor.
  /// </summary>
  public WarningUI() : base(Name, WindowFlags) {
    this.AllowPinning = false;
    this.AllowClickthrough = false;
    this.Size = new Vector2(500f, INITIAL_WINDOW_HEIGHT) * ImGuiHelpers.GlobalScale;
    this.SizeCondition = ImGuiCond.Always;
  }

  /// <summary>
  /// Disposal of this class on unload.
  /// </summary>
  public void Dispose() {
    GC.SuppressFinalize(this);
  }

  /// <summary>
  /// Caching the old position when repositioning the locked window.
  /// </summary>
  private Vector2 _oldPostion = Vector2.Zero;
  private bool positionInitialized;
  private bool positionNeedsSaving;

  public override void PreDraw() {
    Flags = WindowFlags;
    BgAlpha = 1f;
    if (!positionInitialized) {
      Position = Configuration.Position.ToVector2(System.PluginConfig.WarningPosition);
      PositionCondition = ImGuiCond.Once;
      positionInitialized = true;
    }
    ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.09f, 0.09f, 0.11f, 1f));
    ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.12f, 0.12f, 0.15f, 1f));
    ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.17f, 0.17f, 0.21f, 1f));
  }

  public override void PostDraw() => ImGui.PopStyleColor(3);

  private void RememberPosition() {
    var current = ImGui.GetWindowPos();
    var saved = System.PluginConfig.WarningPosition;
    if (current.X != saved.X || current.Y != saved.Y) {
      System.PluginConfig.WarningPosition = Configuration.Position.FromVector2(current);
      positionNeedsSaving = true;
    }
    if (positionNeedsSaving && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) {
      System.PluginConfig.Save();
      positionNeedsSaving = false;
    }
  }

  // cSpell:ignore thicc
  /// <summary>
  /// Draw the window a specific way when it needs to be repositioned.
  /// TODO: Make it a clear window with thicc red border or something like that.
  /// </summary>
  public void DrawRepositioning() {
    Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

    this.BgAlpha = 1f;
    ImGui.TextWrapped("Drag this window by its title bar. Its position is saved automatically.");

    if (_oldPostion.Equals(ImGui.GetWindowPos())) {
      return;
    }

    System.PluginConfig.WarningPosition = Configuration.Position.FromVector2(ImGui.GetWindowPos());
    _oldPostion = ImGui.GetWindowPos();
  }

  private bool DisplayText(string text) {
    var height = ImGui.CalcTextSize("A").Y;
    var padding = ImGui.GetStyle().WindowPadding.X;
    var wrappedHeight = ImGui.CalcTextSize(text, false, ImGui.GetContentRegionAvail().X).Y;

    var desiredHeight = wrappedHeight + ImGui.GetFrameHeightWithSpacing() * 3 + ImGui.GetStyle().WindowPadding.Y * 2;
    if (WindowHeight != desiredHeight) {
      WindowHeight = desiredHeight;
      this.Size = new Vector2(500f * ImGuiHelpers.GlobalScale, WindowHeight);
    }

    ImGui.TextWrapped(text);

    if (height == wrappedHeight) {
      ImGui.SetCursorPosY(ImGui.GetCursorPosY() + height);
    }

    ImGui.Text("You can dismiss this at the button below.");
    return ImGui.Button("Dismiss");
  }

  /// <summary>
  /// Displays a warning for the player of a <see cref="PerPlayerConfig" />.
  /// </summary>
  /// <param name="type">The type of housing to display.</param>
  /// <param name="playerConfig">An instance of a <see cref="PerPlayerConfig" />.</param>
  public void DisplayForPlayer(HousingType type, PerPlayerConfig playerConfig) {
    var property = playerConfig.GetOfType(type);
    if (property is WardProperty reminder) {
      if (ImGui.Button($"Snooze {System.PluginConfig.SnoozeHours} hours")) {
        reminder.SnoozedUntil = DateTimeOffset.UtcNow.AddHours(Math.Clamp(System.PluginConfig.SnoozeHours, 1, 168)).ToUnixTimeSeconds();
        System.PluginConfig.Save(); Pagination.WrapPages(); return;
      }
    }
    if (property.LastVisit <= 0) {
      if (this.DisplayText($"No visit has been recorded for the {HousingTypeEnumHelper.GetDescriptor(type)} of {playerConfig.DisplayName}. Enter the property on that character to record a visit.")) {
        if (property is WardProperty wardProperty) wardProperty.NextReminderAt = DateTimeOffset.UtcNow.AddHours(System.PluginConfig.ReminderRepeatHours).ToUnixTimeSeconds();
        System.PluginConfig.Save();
        this.Pagination.WrapPages();
      }
      return;
    }
    DateTimeOffset dateTimeOffset = DateTime.Now;
    DateTimeOffset dateTimeOffsetLast;
    int pastDays;

    switch (type) {
      case HousingType.FreeCompanyEstate:
        dateTimeOffsetLast = DateTimeOffset.FromUnixTimeSeconds(playerConfig.FreeCompanyEstate.LastVisit);
        pastDays = (int)dateTimeOffset.Subtract(dateTimeOffsetLast).TotalDays;
        if (this.DisplayText($"The Free Company Estate of {playerConfig.DisplayName} has not been visited in, {pastDays} days.")) {
          playerConfig.FreeCompanyEstate.NextReminderAt = DateTimeOffset.UtcNow.AddHours(System.PluginConfig.ReminderRepeatHours).ToUnixTimeSeconds();
          System.PluginConfig.Save();
          this.Pagination.WrapPages();
        }

        break;
      case HousingType.PrivateEstate:
        dateTimeOffsetLast = DateTimeOffset.FromUnixTimeSeconds(playerConfig.PrivateEstate.LastVisit);
        pastDays = (int)dateTimeOffset.Subtract(dateTimeOffsetLast).TotalDays;
        if (this.DisplayText($"The Private Estate of {playerConfig.DisplayName} has not been visited in, {pastDays} days.")) {
          playerConfig.PrivateEstate.NextReminderAt = DateTimeOffset.UtcNow.AddHours(System.PluginConfig.ReminderRepeatHours).ToUnixTimeSeconds();
          System.PluginConfig.Save();
          this.Pagination.WrapPages();
        }

        break;
    }
  }

  /// <summary>
  /// Displays a warning for the player of a <see cref="PerPlayerConfig" />.
  /// </summary>
  /// <param name="type">The type of housing to display.</param>
  /// <param name="playerConfig">An instance of a <see cref="PerPlayerConfig" />.</param>
  /// <returns><see langword="true"/> if successfully drawn otherwise <see langword="false"/>.</returns>
  public bool DrawWarning(HousingType type, PerPlayerConfig playerConfig) {
    this.BgAlpha = 1f;
    Flags = WindowFlags;
    DisplayForPlayer(type, playerConfig);
    return false;
  }

  private Vector2 MoveCursorToBottomRight(string text) {
    var pos = ImGui.GetCursorPos();
    if (this.Size.HasValue) {
      var textSize = ImGui.CalcTextSize(text).X;
      var buttonSize = ImGuiHelpers.GetButtonSize("A");
      var padding = ImGui.GetStyle().WindowPadding;
      ImGui.SetCursorPos(new Vector2(this.Size.Value.X - (padding.X * 3) - (buttonSize.X * 2) - textSize, this.Size.Value.Y - (padding.Y * 2) - buttonSize.Y));
    }

    return pos;
  }

  /// <summary>
  /// Code to be executed every time the window renders.
  /// </summary>
  public override void Draw() {
    RememberPosition();
    if (System.PluginInstance.Repositioning) {
      this.DrawRepositioning();
    } else if (System.IsLoggedIn) {
      this.Pagination.WrapPages();
      if (this.Pagination.CurrentPlayerConfig is { } config) {
        if (this.Pagination.CurrentSubPage is HousingType.FreeCompanyEstate) {
          _ = this.DrawWarning(HousingType.FreeCompanyEstate, config);
        } else if (this.Pagination.CurrentSubPage is HousingType.PrivateEstate) {
          _ = this.DrawWarning(HousingType.PrivateEstate, config);
        }
      }

      var text = $"{this.Pagination.CurrentPage + 1}/{Pagination.TotalPages}";
      var pos = this.MoveCursorToBottomRight(text);
      ImGui.AlignTextToFramePadding();
      ImGui.Text(text);
      ImGui.SameLine();

      if (ImGui.ArrowButton("Previous##HousingTimeoutReminder.Warning", ImGuiDir.Left)) {
        this.Pagination.PreviousPage();
      }

      ImGui.SameLine();

      if (ImGui.ArrowButton("Next##HousingTimeoutReminder.Warning", ImGuiDir.Right)) {
        this.Pagination.NextPage();
      }

      ImGui.SetCursorPos(pos);
    }

    if (Position.HasValue) {
      Position = null;
    }
  }
}

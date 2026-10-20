using System;
using ECommons.DalamudServices;
using ECommons;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Configuration;
/// <summary>
/// Class containing a generic housing plot.
/// </summary>
[Serializable]
public class HousingPlot : WardProperty {
  public bool RemovedFromOverview { get; set; }

  /// <inheritdoc />
  public override short Room {
    get => 0;
    set => _ = value;
  }

  /// <inheritdoc />
  public override bool IsValid => this.District != District.Unknown && this.Ward is >= 1 and <= 32 && this.Plot is >= 1 and <= 60;

  /// <inheritdoc />
  public override void Reset() {
    this.Plot = default;
    this.Room = default;
    this.Enabled = default;
    this.LastVisit = default;
    this.District = default;
    this.Ward = default;
    this.IsDismissed = default;
  }

  [JsonExtensionData]
  [SuppressMessage("Roslynator", "RCS1169")]
  private IDictionary<string, JToken>? _additionalData = null;

  [OnDeserialized]
  private void OnDeserialized(StreamingContext context)
  {
    if (_additionalData is null) {
#if DEBUG
      Svc.Log.Warning("HousingPlot _additionalData is null");
#endif
      return;
    }
  }
}

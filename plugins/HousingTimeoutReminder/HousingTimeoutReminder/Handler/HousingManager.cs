using System;

using CSHousingManager = FFXIVClientStructs.FFXIV.Client.Game.HousingManager;

using ECommons.DalamudServices;
using NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Configuration;

namespace NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Handler;

public sealed class HousingManager: IEquatable<HousingManager> {
  /// <summary>
  /// A boolean determining if the player is inside.
  /// If this is not applicable it is null.
  /// </summary>
  public bool? IsInside { get; }
  /// <summary>
  /// A boolean determining if the house is an apartment.
  /// </summary>
  public bool IsApartment => ApartmentWing != 0;
  /// <summary>
  /// The apartment wing. (or division)
  /// Determines the division the house is in but only if it is an apartment.
  /// </summary>
  public int ApartmentWing => Plot switch { -127 => 1, -126 => 2, _ => 0, };
  /// <summary>
  /// The room of the apartment.
  /// </summary>
  public short Room { get; }
  /// <summary>
  /// The plot the house is in.
  /// </summary>
  public sbyte Plot { get; }
  /// <summary>
  /// The ward the house is in.
  /// </summary>
  public sbyte Ward { get; }
  /// <summary>
  /// The division of the ward the house is in.
  /// </summary>
  public int Division => ApartmentWing != 0 ? ApartmentWing : (Plot > 30 ? 2 : 1);
  /// <summary>
  /// The housing district of the house.
  /// </summary>
  public District District { get; }

  /// <summary>
  /// The default constructor.
  /// </summary>
  /// <param name="plot">
  ///   <para>The plot of the house.</para>
  ///   <para>Is -176 or -177 if is an apartment.</para>
  /// </param>
  /// <param name="ward">The ward the house is in.</param>
  /// <param name="district">A variable representing the housing district as an <see cref="District"/> enum.</param>
  /// <param name="room">
  ///   <para>The room of the apartment complex.</para>
  ///   <para>Is 0 if is not an apartment.</para>
  /// </param>
  /// <param name="isInside">Optional argument if the player is inside the house.</param>
  private HousingManager(sbyte plot, sbyte ward, District district, short room = 0, bool? isInside = null) {
    this.IsInside = isInside;
    this.Room = room;
    this.Plot = plot;
    this.Ward = ward;
    this.District = district;
  }

  /// <summary>
  /// The default constructor but with primitives.
  /// </summary>
  /// <param name="plot">
  ///   <para>The plot of the house.</para>
  ///   <para>Is -176 or -177 if is an apartment.</para>
  /// </param>
  /// <param name="ward">The ward the house is in.</param>
  /// <param name="district">A variable representing the housing district as an ushort.</param>
  /// <param name="room">
  ///   <para>The room of the apartment complex.</para>
  ///   <para>Is 0 if is not an apartment.</para>
  /// </param>
  /// <param name="isInside">Optional argument if the player is inside the house.</param>
  private HousingManager(sbyte plot, sbyte ward, uint district, short room = 0, bool? isInside = null)
    : this(plot, ward,ConvertToDistrict(district), room, isInside) { }

  /// <summary>
  /// The default constructor.
  /// </summary>
  private HousingManager() : this(0, 0, 0u, 0, null) { }

  /// <summary>
  /// Convert the <paramref name="territory" /> to the <see cref="District" />
  /// </summary>
  /// <param name="territory">The ID for the territory the player is in.</param>
  /// <return>The district the player is in.</return>
  public static District ConvertToDistrict(uint territory) {
    return territory switch {
      341 or 345 or 346 or 347 or 386 or 424 or 610 => District.Goblet,
      339 or 282 or 283 or 284 or 384 or 423 or 608 => District.Mist,
      340 or 342 or 343 or 344 or 385 or 425 or 609 => District.LavenderBeds,
      979 or 980 or 981 or 982 or 983 or 984 or 999 => District.Empyreum,
      641 or 649 or 650 or 651 or 652 or 653 or 655 => District.Shirogane,
      _ => District.Unknown,
    };
  }

  /// <summary>
  /// Gets a <see cref="HousingManager" /> instance for the current location.
  /// </summary>
  /// <param name="territory"></param>
  /// <returns>An instanced <see cref="HousingManager" /> if at a housing plot, otherwise a <see cref="Blank" /> instance.</returns>
  public static HousingManager? GetCurrentLocation(uint territory) {
    try {
      unsafe {
        var manager = CSHousingManager.Instance();
        if (manager == null || manager->CurrentTerritory == null) return null;
        var isInside = manager->IsInside();
        short room = manager->GetCurrentRoom();
        sbyte plot = manager->GetCurrentPlot();
        sbyte ward = manager->GetCurrentWard();
        if (isInside) {
          var originalTerritory = CSHousingManager.GetOriginalHouseTerritoryTypeId();
          if (ConvertToDistrict(originalTerritory) != District.Unknown) territory = originalTerritory;
        }
        return new HousingManager((sbyte)(plot + 1), (sbyte)(ward + 1), territory, room, isInside);
      }
    } catch (Exception exception) {
      Svc.Log.Error(exception, "Failed to get current housing location.");
    }

    return null;
  }

  /// <summary>Read only the logged-in character's owned properties, never the house being visited.</summary>
  internal static unsafe int DetectOwnedHousing(bool refreshExisting = false) {
    if (System.GetCurrentPlayerID() is not { } playerId) return 0;
    System.CachedCurrentPlayerId = playerId;
    var knownCharacter = Config.PlayerConfiguration is not null;
    var config = Config.GetPlayerConfig(playerId);
    var detected = 0;
    var changed = !knownCharacter;
    foreach (var type in new[] { HousingType.PrivateEstate, HousingType.FreeCompanyEstate }) {
      var estateType = type switch {
        HousingType.PrivateEstate => FFXIVClientStructs.FFXIV.Client.Game.EstateType.PersonalEstate,
        _ => FFXIVClientStructs.FFXIV.Client.Game.EstateType.FreeCompanyEstate,
      };
      // HouseId is a packed eight-byte value documented by FFXIVClientStructs.
      var raw = CSHousingManager.GetOwnedHouseId(estateType).Id;
      if (!TryDecodeOwnedHouse(raw, playerId.HomeWorld ?? 0, type, out var district, out var ward, out var plot, out var room)) continue;
      detected++;
      var property = config.GetOfType(type);
      if (property is HousingPlot { RemovedFromOverview: true }) continue;
      if (property.IsValid && !refreshExisting && type == HousingType.PrivateEstate) continue;
      if (property.District == district && property.Ward == ward && property.Plot == plot && property.Room == room) continue;
      var wasConfigured = property.IsValid;
      var hadAddress = property.District != District.Unknown || property.Ward != 0 || property.Plot != 0 || property.Room != 0;
      property.District = district;
      property.Ward = ward;
      property.Plot = plot;
      property.Room = room;
      // An old property's visit must not carry over to a different address.
      if (hadAddress) property.LastVisit = 0;
      if (type == HousingType.FreeCompanyEstate && hadAddress) {
        config.FreeCompanyId = 0;
        config.FcEstateAccess = null;
        config.FcRankName = string.Empty;
      }
      if (!wasConfigured) property.Enabled = true;
      if (property is WardProperty wardProperty) wardProperty.IsDismissed = false;
      changed = true;
    }
    if (changed) System.PluginConfig.Save();
    return detected;
  }

  internal static bool TryDecodeOwnedHouse(ulong raw, uint homeWorld, HousingType type,
      out District district, out sbyte ward, out sbyte plot, out short room) {
    district = District.Unknown;
    ward = plot = 0;
    room = 0;
    if (raw == 0 || raw == ulong.MaxValue || (ushort)(raw >> 48) != homeWorld) return false;
    district = ConvertToDistrict((ushort)(raw >> 32));
    var unit = (byte)raw;
    var data = (ushort)(raw >> 16);
    var apartment = (unit & 0x80) != 0;
    var wardIndex = data & 0x3F;
    if (district == District.Unknown || wardIndex >= 32 || apartment || type is not (HousingType.PrivateEstate or HousingType.FreeCompanyEstate)) return false;
    ward = (sbyte)(wardIndex + 1);
    if (unit >= 60 || (data >> 6) != 0) return false;
    plot = (sbyte)(unit + 1);
    return true;
  }

  /// <summary>
  /// Creates a new instance of a <see cref="HousingManager" /> from a two parameters.
  /// </summary>
  /// <param name="playerID">The instance of a player id to fetch the player config.</param>
  /// <param name="type">The type of house to manage.</param>
  /// <returns>A new instance of a <see cref="HousingManager" />.</returns>
  public static HousingManager? From(PlayerID playerID, HousingType type) {
    var playerConfig = System.PluginConfig.PlayerConfigs.Find(config => config.PlayerID == playerID);
    if (playerConfig is null) return null;

    return type switch {
      HousingType.FreeCompanyEstate when playerConfig.FreeCompanyEstate is IWardProperty fcHousingPlot =>
        new HousingManager(fcHousingPlot.Plot, fcHousingPlot.Ward, fcHousingPlot.District),
      HousingType.PrivateEstate when playerConfig.PrivateEstate is IWardProperty peHousingPlot => new HousingManager(
        peHousingPlot.Plot, peHousingPlot.Ward, peHousingPlot.District),
      _ => null
    };
  }

  /// <summary>
  /// Compares this instance of a <see cref="HousingManager"/> with another or null.
  /// </summary>
  /// <param name="otherManager">The other <see cref="HousingManager"/> or null</param>
  /// <returns><see langword="true"/> if they are the same and not null, otherwise <see langword="false"/></returns>
  public bool Equals(HousingManager? otherManager) {
      return this.Room == otherManager?.Room &&
        this.Plot == otherManager.Plot &&
        this.Ward == otherManager.Ward &&
        this.Division == otherManager.Division &&
        (int)this.District == (int)otherManager.District;
  }

  /// <inheritdoc/>
  public override bool Equals(object? obj) {
    if (obj is HousingManager otherManager) {
      return Equals(otherManager);
    }

    return false;
  }

  /// <inheritdoc/>
  public override int GetHashCode() {
    return HashCode.Combine((int)this.Room, (int)this.Plot, (int)this.Ward, (int)this.District);
  }
}


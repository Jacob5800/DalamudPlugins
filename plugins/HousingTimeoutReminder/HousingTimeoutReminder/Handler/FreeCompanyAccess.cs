using System;
using System.Collections.Generic;
using Dalamud.Utility;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using InteropGenerator.Runtime;
using NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Configuration;

namespace NekoBoiNick.FFXIV.DalamudPlugin.HousingTimeoutReminder.Handler;

internal static class FreeCompanyAccess {
  internal static bool ShouldShowReminder(bool enabled, bool? estateAccess, bool late) => enabled && estateAccess != false && late;
  internal static bool HasEstateAccess(uint housingPermissions) => (housingPermissions & 1u) != 0;

  internal static bool? MatchRankAccess(string[] rankNames, uint[] permissions, string[] memberFields, out string rankName) {
    rankName = string.Empty;
    bool? result = null;
    for (var index = 0; index < Math.Min(rankNames.Length, permissions.Length); index++) {
      if (string.IsNullOrWhiteSpace(rankNames[index]) || Array.IndexOf(memberFields, rankNames[index]) < 0) continue;
      if (result.HasValue) { rankName = string.Empty; return null; }
      rankName = rankNames[index];
      result = HasEstateAccess(permissions[index]);
    }
    return result;
  }

  internal static unsafe void Refresh() {
    if (!System.IsLoggedIn || Svc.Objects.LocalPlayer is not { } player || Config.PlayerConfiguration is not { } config) return;
    var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("FreeCompanyMember").Address;
    if (addon == null || !addon->IsVisible) return;
    var company = InfoProxyFreeCompany.Instance();
    var members = InfoProxyFreeCompanyMember.Instance();
    if (company == null || members == null || company->Id == 0 || company->Id != members->FreeCompanyId || company->HomeWorldId != player.HomeWorld.RowId) return;
    var data = members->InfoProxyCommonList.CharDataSpan;
    if (data.Length is < 1 or > 200) return;
    var selfPresent = false;
    foreach (var member in data) {
      if (member.HomeWorld == player.HomeWorld.RowId && member.NameString == player.Name.TextValue) { selfPresent = true; break; }
    }
    if (!selfPresent) return;
    var strings = FreeCompanyMemberStringArray.Instance();
    if (strings == null) return;
    var rankNames = new List<string>();
    var permissions = new List<uint>();
    foreach (var rank in company->Ranks) {
      if (string.IsNullOrWhiteSpace(rank.NameString)) continue;
      rankNames.Add(rank.NameString);
      permissions.Add((uint)rank.Housing);
    }
    if (!permissions.Exists(HasEstateAccess)) return; // Rank labels can arrive before the permissions table.
    foreach (var row in strings->Members) {
      if (Read(row.PlayerName) != player.Name.TextValue) continue;
      var fields = new[] { Read(row.Activity), Read(row.FcName), Read(row.Status), Read(row.GrandCompanyName) };
      var access = MatchRankAccess(rankNames.ToArray(), permissions.ToArray(), fields, out var rankName);
      var companyChanged = config.FreeCompanyId != company->Id;
      if (companyChanged) {
        config.FreeCompanyId = company->Id;
        config.FcEstateAccess = null;
        config.FcRankName = string.Empty;
      }
      if (!access.HasValue) {
        if (companyChanged) System.PluginConfig.Save();
        return;
      }
      if (config.FcEstateAccess == access && config.FcRankName == rankName) return;
      config.FcEstateAccess = access;
      config.FcRankName = rankName;
      System.PluginConfig.Save();
      return;
    }
  }

  private static unsafe string Read(CStringPointer value) => value.Value == null ? string.Empty : value.ExtractText();
}

using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;

namespace TataruBrawler;

internal static class BlockingCastReader
{
    // Cursed Gaze, Sensory Jammer, alternate Sensory Jammer. Corroborated with
    // Saucy's Keybound module; names/localization are deliberately not consulted.
    public static IEnumerable<BlockingCast> Read(IObjectTable objects)
    {
        foreach (var obj in objects)
            if (obj is IBattleChara battle && battle.ObjectKind == ObjectKind.BattleNpc && battle.IsCasting &&
                battle.CastActionId is 49529 or 49532 or 49702)
                yield return new BlockingCast(battle.CastActionId,
                    Math.Max(0, (battle.TotalCastTime - battle.CurrentCastTime) * 1000));
    }
}


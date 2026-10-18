namespace PortraitAtelier;

public sealed record PortraitCharacterOption(ushort Id, string Name);

public static class PortraitCharacterStyling
{
    public static PortraitPreset ApplyDesign(PortraitPreset preset, PortraitStyle style, int variation,
        IReadOnlyList<PortraitCharacterOption> backgrounds, IReadOnlyList<PortraitCharacterOption> frames,
        IReadOnlyList<PortraitCharacterOption> decorations, PluginConfig? preferences = null) => preset with
    {
        BackgroundId = preferences?.StyleBackground == false ? preset.BackgroundId : PickDesign(backgrounds, preferences?.PreferredBackgroundIds, preset.BackgroundId, style.Seed, variation),
        FrameId = preferences?.StyleFrame == false ? preset.FrameId : PickDesign(frames, preferences?.PreferredFrameIds, preset.FrameId, style.Seed * 3, variation),
        DecorationId = preferences?.StyleAccent == false ? preset.DecorationId : PickDesign(decorations, preferences?.PreferredAccentIds, preset.DecorationId, style.Seed * 7, variation),
    };

    public static PortraitPreset Apply(PortraitPreset preset, PortraitStyle style, int variation,
        IReadOnlyList<PortraitCharacterOption> poses, IReadOnlyList<PortraitCharacterOption> expressions)
    {
        var dramatic = style.Id is "weapon-showcase" or "ember-drama" or "hero-frame";
        var soft = style.Id is "rose-bloom" or "golden-hour" or "soft-studio";
        var poseTerms = dramatic ? new[] { "weapon", "battle", "victory", "determined" }
            : soft ? new[] { "welcome", "wave", "joy", "cheer" }
            : new[] { "standing", "pose", "welcome" };
        var expressionTerms = dramatic ? new[] { "determined", "confident", "serious" }
            : soft ? new[] { "smile", "beam", "happy" }
            : new[] { "smirk", "smile", "neutral" };
        (poseTerms, expressionTerms) = style.Id switch
        {
            "shoreline-daydream" => (["relax", "lean", "stretch", "hands"], ["smile", "content", "happy"]),
            "starlight-performer" => (["song", "sing", "dance", "perform"], ["smile", "confident", "serious"]),
            "playful-selfie" => (["selfie", "peace", "wave", "cheer"], ["smile", "beam", "happy"]),
            "gallery-closeup" or "silver-wink" => (["pose", "salute", "wave"], ["wink", "smirk", "smile"]),
            "gilded-muse" => (["pose", "think", "consider"], ["surprise", "confused", "smirk"]),
            "tea-break" => (["tea", "drink", "toast"], ["content", "smile", "relaxed"]),
            "noir-blade" or "aether-duelist" or "scarlet-and-cobalt" or "shadow-outline" or "victory-cut" => (["weapon", "battle", "victory", "draw"], ["serious", "determined", "confident"]),
            "sunset-confidence" => (["stretch", "pose", "relax"], ["smile", "grin", "confident"]),
            _ => (poseTerms, expressionTerms),
        };
        var pose = Pick(poses, poseTerms, preset.BannerTimeline, style.Seed, variation);
        var expression = Pick(expressions.Where(x => x.Id <= byte.MaxValue).ToArray(), expressionTerms,
            preset.Expression, style.Seed, variation);
        return preset with
        {
            BannerTimeline = pose,
            Expression = (byte)expression,
            AnimationProgress = pose == preset.BannerTimeline ? preset.AnimationProgress : 0f,
        };
    }

    private static ushort PickDesign(IReadOnlyList<PortraitCharacterOption> owned, IReadOnlyList<ushort>? preferences, ushort current, int seed, int variation)
    {
        var preferred = owned.Where(option => preferences?.Contains(option.Id) == true).ToArray();
        // Three out of four draws favor preferences; other draws retain all owned choices.
        var candidates = preferred.Length > 0 && Random.Shared.Next(4) < 3 ? preferred : owned;
        if (candidates.Count == 0) return current;
        var different = candidates.Where(option => option.Id != current).ToArray();
        return different.Length > 0 ? different[Random.Shared.Next(different.Length)].Id : candidates[Random.Shared.Next(candidates.Count)].Id;
    }

    private static ushort Pick(IReadOnlyList<PortraitCharacterOption> options, string[] terms,
        ushort current, int seed, int variation)
    {
        if (options.Count == 0) return current;
        var preferred = options.Where(x => !x.Name.Contains("fake", StringComparison.OrdinalIgnoreCase)
            && terms.Any(t => x.Name.Contains(t, StringComparison.OrdinalIgnoreCase))).ToArray();
        var candidates = preferred.Length > 0 ? preferred : options.ToArray();
        var different = candidates.Where(x => x.Id != current).ToArray();
        if (different.Length > 0) candidates = different;
        return candidates[(int)((uint)(seed + variation) % (uint)candidates.Length)].Id;
    }
}

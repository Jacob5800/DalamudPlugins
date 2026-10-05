namespace PortraitAtelier;

public sealed record PortraitStyle(
    int Seed,
    string Id,
    string Name,
    string Category,
    string Description,
    Rgb DirectionalColor,
    Rgb AmbientColor,
    float DirectionalBrightness,
    float AmbientBrightness,
    float CameraDistance)
{
    /// <summary>
    /// Small vertical framing adjustment, expressed as a fraction of the camera-to-target distance.
    /// Zero preserves the captured framing.
    /// </summary>
    public float CameraTargetOffset { get; init; }

    public static IReadOnlyList<PortraitStyle> Featured { get; } =
    [
        new PortraitStyle(9, "seamless-showcase", "Seamless Showcase", "Community-inspired", "Clean, balanced light and an open crop inspired by uncluttered portrait layouts.", new(250, 244, 232), new(145, 153, 166), 0.98f, 1.05f, 1.06f) { CameraTargetOffset = 0.015f },
        new PortraitStyle(10, "weapon-showcase", "Weapon Showcase", "Community-inspired", "A wider crop and clear neutral light keep the captured pose and equipment visible.", new(255, 225, 190), new(125, 137, 157), 1.02f, 1.00f, 1.20f) { CameraTargetOffset = -0.02f },
        new(1, "golden-hour", "Golden Hour", "Warm", "Warm side light with soft amber fill and a modest portrait crop.", new(255, 192, 126), new(112, 77, 61), 1.04f, 0.98f, 0.92f),
        new(2, "moonlit", "Moonlit", "Cool", "Cool blue highlights with a deeper, calm fill.", new(150, 190, 255), new(62, 80, 129), 1.06f, 0.94f, 0.94f),
        new(3, "soft-studio", "Soft Studio", "Clean", "Balanced neutral light designed to keep equipment colors readable.", new(248, 240, 222), new(156, 162, 176), 0.94f, 1.08f, 1.02f),
        new(4, "rose-bloom", "Rose Bloom", "Soft", "Blush key light and muted mauve fill for a gentle look.", new(255, 180, 205), new(128, 91, 126), 0.98f, 1.02f, 0.96f),
        new(5, "ember-drama", "Ember Drama", "Dramatic", "Warm red-orange highlights against a darker shadow tone.", new(255, 132, 91), new(83, 48, 52), 1.12f, 0.92f, 0.90f),
        new(6, "arcane-violet", "Arcane Violet", "Fantasy", "Violet highlights and blue-purple ambient fill.", new(206, 163, 255), new(78, 70, 131), 1.04f, 0.96f, 0.94f),
        new(7, "verdant-glow", "Verdant Glow", "Fantasy", "Fresh green highlights with a restrained forest fill.", new(167, 236, 190), new(74, 112, 89), 1.02f, 1.00f, 0.98f),
        new(8, "hero-frame", "Hero Frame", "Composition", "Slightly wider camera distance for a more open, full-character composition.", new(247, 220, 186), new(111, 120, 143), 1.00f, 1.02f, 1.16f),
    ];
}

public readonly record struct Rgb(byte Red, byte Green, byte Blue)
{
    public System.Numerics.Vector4 ToColor() => new(Red / 255f, Green / 255f, Blue / 255f, 1f);
    public override string ToString() => $"#{Red:X2}{Green:X2}{Blue:X2}";
}

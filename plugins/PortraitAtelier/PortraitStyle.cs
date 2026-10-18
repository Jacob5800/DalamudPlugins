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
    public short? DirectionalVerticalAngle { get; init; }
    public short? DirectionalHorizontalAngle { get; init; }

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
        new PortraitStyle(11, "shoreline-daydream", "Shoreline Daydream", "Relaxed", "Sunlit cream highlights, sea-blue fill and a wider crop inspired by relaxed beach portraits.", new(255, 237, 195), new(123, 177, 185), 1.02f, 1.08f, 1.2f) { CameraTargetOffset = -0.025f },
        new PortraitStyle(12, "starlight-performer", "Starlight Performer", "Fantasy", "A close performer portrait with cool starlight and a soft gold glow; owned performance poses are favored.", new(225, 232, 255), new(89, 76, 132), 1.1f, 0.88f, 0.91f) { CameraTargetOffset = 0.025f },
        new PortraitStyle(13, "playful-selfie", "Playful Selfie", "Playful", "Bright face lighting and a friendly close crop, favoring owned selfie, wave and peace poses.", new(255, 228, 210), new(145, 151, 164), 1.03f, 1.08f, 0.89f) { CameraTargetOffset = 0.035f },
        new PortraitStyle(14, "silver-wink", "Silver Wink", "Close-up", "Soft silver highlights and restrained shadows frame a playful close-up with owned wink expressions.", new(243, 246, 255), new(143, 140, 153), 0.98f, 1.02f, 0.82f) { CameraTargetOffset = 0.04f },
        new PortraitStyle(15, "gilded-muse", "Gilded Muse", "Elegant", "Warm ivory light and muted burgundy shadows inspired by expressive portraits in ornate frames.", new(255, 221, 173), new(120, 71, 83), 1.08f, 0.94f, 0.88f) { CameraTargetOffset = 0.025f },
        new PortraitStyle(16, "tea-break", "Tea Break", "Relaxed", "Warm porcelain light with cool blue fill for a cozy close portrait; tea and drink poses are favored when owned.", new(255, 235, 216), new(117, 149, 177), 1.0f, 1.04f, 0.87f) { CameraTargetOffset = 0.025f },
        new PortraitStyle(17, "noir-blade", "Noir Blade", "Dramatic", "Cool restrained face light and deep fill leave room for a captured glowing weapon in a wider crop.", new(164, 187, 215), new(42, 49, 64), 0.84f, 0.66f, 1.17f) { CameraTargetOffset = -0.025f },
        new PortraitStyle(18, "sunset-confidence", "Sunset Confidence", "Warm", "Peach-gold light and warm shadows for an open smile and a relaxed upper-body composition.", new(255, 183, 137), new(133, 81, 76), 1.08f, 0.97f, 0.94f) { CameraTargetOffset = 0.015f },
        new PortraitStyle(19, "aether-duelist", "Aether Duelist", "Fantasy", "Bright cyan-white light with blue ambient tones and wider framing for an owned battle pose.", new(206, 246, 255), new(86, 139, 181), 1.14f, 0.95f, 1.14f) { CameraTargetOffset = -0.015f },
        new PortraitStyle(20, "candlelit-study", "Candlelit Study", "Elegant", "Amber key light and muted violet fill for a quiet portrait with readable face detail.", new(255, 203, 139), new(104, 83, 121), 1.06f, 0.9f, 0.93f) { CameraTargetOffset = 0.02f },
        new PortraitStyle(21, "ice-crystal", "Ice Crystal", "Cool", "Crisp white-blue highlights and soft teal shadows for a clean winter-toned portrait.", new(203, 230, 255), new(86, 134, 147), 1.08f, 0.96f, 0.96f) { CameraTargetOffset = 0.015f },
        new PortraitStyle(22, "orchid-afterglow", "Orchid Afterglow", "Fantasy", "Pink-violet light with blue fill adds color contrast while keeping a comfortable upper-body crop.", new(255, 173, 224), new(80, 104, 166), 1.06f, 0.94f, 1.03f) { CameraTargetOffset = 0.015f },
        new PortraitStyle(23, "crimson-spotlight", "Crimson Spotlight", "Dramatic", "Saturated red directional light with a dark violet fill and close framing, inspired by bold community lighting experiments.", new(255, 62, 65), new(66, 40, 82), 1.22f, 0.74f, 0.86f) { CameraTargetOffset = 0.035f, DirectionalVerticalAngle = 25, DirectionalHorizontalAngle = 65 },
        new PortraitStyle(24, "azure-contour", "Azure Contour", "Cool", "Blue side light with a restrained warm fill shapes the face without washing out the equipment.", new(99, 171, 255), new(118, 77, 59), 1.12f, 0.82f, 0.93f) { CameraTargetOffset = 0.02f, DirectionalVerticalAngle = 15, DirectionalHorizontalAngle = -70 },
        new PortraitStyle(25, "scarlet-and-cobalt", "Scarlet and Cobalt", "Dramatic", "Red highlights and cobalt ambient color create a strong warm-cool contrast for battle or victory poses.", new(255, 85, 98), new(57, 89, 183), 1.18f, 0.86f, 1.05f) { CameraTargetOffset = 0f, DirectionalVerticalAngle = 20, DirectionalHorizontalAngle = 55 },
        new PortraitStyle(26, "shadow-outline", "Shadow Outline", "Dramatic", "Low-color face lighting and a wider crop favor a silhouette-like outline; the result depends on your owned background and gear.", new(46, 55, 74), new(23, 28, 42), 0.65f, 0.65f, 1.16f) { CameraTargetOffset = -0.02f, DirectionalVerticalAngle = 30, DirectionalHorizontalAngle = 120 },
        new PortraitStyle(27, "pale-blue-minimal", "Pale Blue Minimal", "Clean", "Soft pale-blue light and an open neutral crop complement uncluttered, seamless-style plates using your owned designs.", new(225, 241, 255), new(164, 184, 203), 0.98f, 1.12f, 1.07f) { CameraTargetOffset = 0.015f, DirectionalVerticalAngle = 20, DirectionalHorizontalAngle = -15 },
        new PortraitStyle(28, "smoke-and-silver", "Smoke and Silver", "Dramatic", "Cool silver face light over muted charcoal fill leaves space for a smoke accent if you choose one you own.", new(221, 231, 244), new(68, 76, 91), 1.1f, 0.8f, 0.92f) { CameraTargetOffset = 0.02f, DirectionalVerticalAngle = 35, DirectionalHorizontalAngle = 50 },
        new PortraitStyle(29, "victory-cut", "Victory Cut", "Composition", "A wider equipment-focused composition with clear directional light, favoring owned victory and battle poses.", new(255, 225, 184), new(113, 135, 163), 1.13f, 0.98f, 1.24f) { CameraTargetOffset = -0.035f, DirectionalVerticalAngle = 20, DirectionalHorizontalAngle = -40 },
        new PortraitStyle(30, "gallery-closeup", "Gallery Close-up", "Close-up", "A deliberate tight crop and clean neutral highlights favor expressive, face-focused character portraits.", new(255, 239, 220), new(131, 137, 153), 1.02f, 1.02f, 0.79f) { CameraTargetOffset = 0.045f, DirectionalVerticalAngle = 10, DirectionalHorizontalAngle = 20 },
    ];
}

public readonly record struct Rgb(byte Red, byte Green, byte Blue)
{
    public System.Numerics.Vector4 ToColor() => new(Red / 255f, Green / 255f, Blue / 255f, 1f);
    public override string ToString() => $"#{Red:X2}{Green:X2}{Blue:X2}";
}

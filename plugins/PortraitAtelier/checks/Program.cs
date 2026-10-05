using PortraitAtelier;

var source = new PortraitPreset
{
    CameraPosition = new((Half)0.1f, (Half)1.6f, (Half)3, (Half)1),
    CameraTarget = new((Half)0, (Half)1.3f, (Half)0, (Half)1),
    HeadDirection = new((Half)0.1f, (Half)(-0.2f)),
    EyeDirection = new((Half)0.2f, (Half)0.1f),
    ImageRotation = -12, CameraZoom = 160, BannerTimeline = 17, AnimationProgress = 0.7f,
    Expression = 4, DirectionalRed = 255, DirectionalGreen = 255, DirectionalBlue = 255,
    DirectionalBrightness = 127, DirectionalVerticalAngle = 133, DirectionalHorizontalAngle = -77,
    AmbientRed = 51, AmbientGreen = 51, AmbientBlue = 51, AmbientBrightness = 127,
    BackgroundId = 5, FrameId = 7, DecorationId = 9
};
var count = 0;
void Check(bool condition, string label)
{
    count++;
    if (!condition) throw new InvalidOperationException(label);
}

Check(PortraitPreset.TryParse(source.ToShareCode(), out var decoded, out _) && decoded == source, "Share code loses captured fields");
Check(Convert.FromBase64String(source.ToShareCode()).Length == PortraitPreset.EncodedLength, "Unexpected binary size");
foreach (var style in PortraitStyle.Featured)
{
    Check(source.WithStyle(style, 0, 0) == source, $"Zero strength changes {style.Name}");
    var styled = source.WithStyle(style, 1, 0);
    Check(styled.TryValidate(out _), $"Invalid generated values: {style.Name}");
    Check(styled != source && styled.CameraPosition != source.CameraPosition, $"Style does not change composition: {style.Name}");
    Check(styled.BannerTimeline == source.BannerTimeline && styled.AnimationProgress == source.AnimationProgress
        && styled.Expression == source.Expression && styled.HeadDirection == source.HeadDirection
        && styled.EyeDirection == source.EyeDirection && styled.BackgroundId == source.BackgroundId
        && styled.FrameId == source.FrameId && styled.DecorationId == source.DecorationId,
        $"Style changes protected portrait fields: {style.Name}");
    Check(PortraitPreset.TryParse(styled.ToShareCode(), out var result, out _) && result == styled, $"Generated code round trip: {style.Name}");
    var dark = (source with { DirectionalBrightness = 0, AmbientBrightness = 0 }).WithStyle(style, 1, 0);
    Check(dark.DirectionalBrightness > 0 && dark.AmbientBrightness > 0, $"Zero brightness stays invisible: {style.Name}");
    var extreme = (source with { CameraPosition = new(Half.MaxValue, Half.MaxValue, Half.MaxValue, (Half)1) }).WithStyle(style, 1, 0);
    Check(extreme.TryValidate(out _), $"Camera calculation overflows: {style.Name}");
}

var session = new PortraitDesignSession();
var current = source;
for (var round = 0; round < 100; round++)
{
    var baseline = session.BaseFor(current);
    Check(baseline == source, "Browsing accumulates camera or light changes");
    current = baseline.WithStyle(PortraitStyle.Featured[round % PortraitStyle.Featured.Count], 1, 0);
    session.Remember(baseline, current);
    current = current with { AnimationProgress = current.AnimationProgress + 0.01f };
}
var manualEdit = current with { CameraZoom = 100 };
Check(session.BaseFor(manualEdit) == manualEdit, "User edits are not used as a new base");
session.Reset();
Check(session.BaseFor(current) == current, "Reset retains the old base");
foreach (var bad in new[] { "", "not a code", Convert.ToBase64String(new byte[58]), source.ToShareCode()[..^4] })
    Check(!PortraitPreset.TryParse(bad, out _, out _), "Malformed code accepted");
foreach (var bad in new[]
{
    source with { CameraZoom = 201 }, source with { ImageRotation = 91 },
    source with { DirectionalVerticalAngle = -181 }, source with { AnimationProgress = float.NaN },
    source with { CameraTarget = new(Half.PositiveInfinity, (Half)0, (Half)0, (Half)1) }
})
    Check(!PortraitPreset.TryParse(bad.ToShareCode(), out _, out _), "Out-of-range native parameters accepted");

var ownedPoses = new PortraitCharacterOption[] { new(1, "Standing 1"), new(2, "Victory"), new(3, "Welcome") };
var ownedExpressions = new PortraitCharacterOption[] { new(1, "Smile"), new(2, "Determined"), new(300, "Invalid byte") };
var ownedDesigns = new PortraitCharacterOption[] { new(1, ""), new(2, ""), new(3, "") };
foreach (var style in PortraitStyle.Featured)
{
    var character = PortraitCharacterStyling.Apply(source, style, 0, ownedPoses, ownedExpressions);
    Check(ownedPoses.Any(x => x.Id == character.BannerTimeline), "Pose not from owned list");
    Check(ownedExpressions.Any(x => x.Id == character.Expression), "Expression not from owned list");
    Check(character.AnimationProgress == 0, "New pose retains previous animation time");
    var design = PortraitCharacterStyling.ApplyDesign(character, style, 0, ownedDesigns, ownedDesigns, ownedDesigns);
    Check(ownedDesigns.Any(x => x.Id == design.BackgroundId)
        && ownedDesigns.Any(x => x.Id == design.FrameId) && ownedDesigns.Any(x => x.Id == design.DecorationId), "Design not from owned list");
    Check(PortraitPreset.TryParse(design.ToShareCode(), out var full, out _) && full == design, "Full portrait round trip");
}
Check(PortraitCharacterStyling.Apply(source, PortraitStyle.Featured[0], 0, [], []) == source, "Empty owned character lists do not preserve source");
Check(PortraitCharacterStyling.ApplyDesign(source, PortraitStyle.Featured[0], 0, [], [], []) == source, "Empty owned design lists do not preserve source");
Console.WriteLine($"PASS: {count} checks across {PortraitStyle.Featured.Count} styles, 100 consecutive design changes, owned character/design selection, share codes and invalid inputs.");

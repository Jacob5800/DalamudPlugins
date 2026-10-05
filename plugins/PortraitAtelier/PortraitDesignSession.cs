namespace PortraitAtelier;

/// <summary>Reuse the original composition when browsing looks, rather than styling a styled result.</summary>
public sealed class PortraitDesignSession
{
    private PortraitPreset? original;
    private PortraitPreset? lastApplied;

    public PortraitPreset BaseFor(PortraitPreset current) => original is not null && lastApplied is not null
        && SameDesign(current, lastApplied) ? original : current;

    public void Remember(PortraitPreset baseline, PortraitPreset actual)
    {
        original = baseline;
        lastApplied = actual;
    }

    public void Reset() => (original, lastApplied) = (null, null);

    // A playing animation can advance between clicks without being an intentional edit.
    private static bool SameDesign(PortraitPreset left, PortraitPreset right) =>
        (left with { AnimationProgress = 0 }) == (right with { AnimationProgress = 0 });
}

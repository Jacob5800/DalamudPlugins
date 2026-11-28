namespace RetainerPricer;

/// <summary>A broken part (slot 0-3) and the repair materials it takes.</summary>
internal sealed record BrokenPart(int Slot, int Materials);

internal static class SubmarineRepairPlan
{
    public const uint RepairMaterialsItemId = 10373;

    /// <summary>Only parts at 0% are repaired. Zero when the carried materials cover all of them.</summary>
    public static int Shortfall(IReadOnlyList<BrokenPart> broken, int carried) =>
        Math.Max(0, broken.Sum(part => part.Materials) - carried);
}
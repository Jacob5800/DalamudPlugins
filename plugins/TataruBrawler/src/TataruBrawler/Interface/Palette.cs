
using System.Numerics;

namespace TataruBrawler.Interface;

// Default palette adapted from Auto Keybound Brawler by XeldarAlz.
// Glasswork theme derivation is not included in this layout-only port.
internal sealed class Palette
{
    private const float SoftLift = 0.30f;
    private const float DeepDrop = 0.30f;
    private const float Surface0Step = 0.033f;
    private const float Surface1Step = 0.061f;
    private const float Surface2Step = 0.098f;
    private const float Surface3Step = 0.139f;
    private const float SliderStep = 0.118f;
    private const float BorderStep = 0.198f;

    // Over real glass the window keeps a light wash of its background and the cards let the glass show through.
    private const float SolidWindowAlpha = 0.985f;
    private const float GlassWindowAlpha = 0.35f;
    private const float SolidCardAlpha = 0.90f;
    private const float GlassCardAlpha = 0.55f;

    public Vector4 AccentTeal;
    public Vector4 AccentTealSoft;
    public Vector4 AccentTealDeep;
    public Vector4 AccentMint;
    public Vector4 AccentMintSoft;
    public Vector4 AccentAmber;
    public Vector4 AccentAmberSoft;
    public Vector4 AccentRose;
    public Vector4 AccentRoseSoft;
    public Vector4 AccentBlue;
    public Vector4 AccentBlueSoft;

    public Vector4 WindowBg;
    public Vector4 Surface0;
    public Vector4 Surface1;
    public Vector4 Surface2;
    public Vector4 Surface3;
    public Vector4 CardBg;
    public Vector4 CardBgSoft;
    public Vector4 CardBgHover;
    public Vector4 SliderBg;
    public Vector4 BorderDim;

    public Vector4 TextStrong;
    public Vector4 TextSecondary;
    public Vector4 TextDim;
    public Vector4 TextMuted;

    // What hairlines, borders and scrollbars are drawn in: white over a dark theme, the text colour over a light one.
    public Vector4 Ink;

    public static Palette Grindless() => new()
    {
        AccentTeal = new(0.32f, 0.78f, 0.78f, 1.00f),
        AccentTealSoft = new(0.52f, 0.91f, 0.91f, 1.00f),
        AccentTealDeep = new(0.16f, 0.56f, 0.60f, 1.00f),
        AccentMint = new(0.46f, 0.86f, 0.66f, 1.00f),
        AccentMintSoft = new(0.66f, 0.96f, 0.80f, 1.00f),
        AccentAmber = new(0.92f, 0.74f, 0.34f, 1.00f),
        AccentAmberSoft = new(1.00f, 0.86f, 0.52f, 1.00f),
        AccentRose = new(0.93f, 0.42f, 0.50f, 1.00f),
        AccentRoseSoft = new(1.00f, 0.62f, 0.68f, 1.00f),
        AccentBlue = new(0.40f, 0.68f, 0.98f, 1.00f),
        AccentBlueSoft = new(0.62f, 0.82f, 1.00f, 1.00f),

        WindowBg = new(0.046f, 0.056f, 0.068f, SolidWindowAlpha),
        Surface0 = new(0.076f, 0.092f, 0.110f, 1.00f),
        Surface1 = new(0.102f, 0.122f, 0.145f, 1.00f),
        Surface2 = new(0.136f, 0.160f, 0.188f, 1.00f),
        Surface3 = new(0.174f, 0.202f, 0.236f, 1.00f),
        CardBg = new(0.076f, 0.092f, 0.110f, SolidCardAlpha),
        CardBgSoft = new(0.102f, 0.122f, 0.145f, 0.62f),
        CardBgHover = new(0.136f, 0.160f, 0.188f, 0.95f),
        SliderBg = new(0.154f, 0.180f, 0.212f, 1.00f),
        BorderDim = new(0.228f, 0.268f, 0.310f, 1.00f),

        TextStrong = new(0.965f, 0.968f, 0.975f, 1.00f),
        TextSecondary = new(0.780f, 0.805f, 0.840f, 1.00f),
        TextDim = new(0.560f, 0.595f, 0.640f, 1.00f),
        TextMuted = new(0.400f, 0.425f, 0.470f, 1.00f),
        Ink = Vector4.One,
    };

}

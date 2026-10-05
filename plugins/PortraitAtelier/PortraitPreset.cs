namespace PortraitAtelier;

/// <summary>
/// Portable portrait parameters. The binary layout is compatible with the
/// HaselTweaks Portrait Helper v1 preset string format.
/// </summary>
public sealed record PortraitPreset
{
    public const int Magic = 0x53505448; // HTPS in little-endian byte order
    public const ushort CurrentVersion = 1;
    public const int EncodedLength = 58;

    public required Half4 CameraPosition { get; init; }
    public required Half4 CameraTarget { get; init; }
    public short ImageRotation { get; init; }
    public byte CameraZoom { get; init; }
    public ushort BannerTimeline { get; init; }
    public float AnimationProgress { get; init; }
    public byte Expression { get; init; }
    public required Half2 HeadDirection { get; init; }
    public required Half2 EyeDirection { get; init; }
    public byte DirectionalRed { get; init; }
    public byte DirectionalGreen { get; init; }
    public byte DirectionalBlue { get; init; }
    public byte DirectionalBrightness { get; init; }
    public short DirectionalVerticalAngle { get; init; }
    public short DirectionalHorizontalAngle { get; init; }
    public byte AmbientRed { get; init; }
    public byte AmbientGreen { get; init; }
    public byte AmbientBlue { get; init; }
    public byte AmbientBrightness { get; init; }
    public ushort BackgroundId { get; init; }
    public ushort FrameId { get; init; }
    public ushort DecorationId { get; init; }

    public string ToShareCode()
    {
        using var stream = new MemoryStream(EncodedLength);
        using var writer = new BinaryWriter(stream);

        writer.Write(Magic);
        writer.Write(CurrentVersion);
        WriteHalf4(writer, CameraPosition);
        WriteHalf4(writer, CameraTarget);
        writer.Write(ImageRotation);
        writer.Write(CameraZoom);
        writer.Write(BannerTimeline);
        writer.Write(AnimationProgress);
        writer.Write(Expression);
        WriteHalf2(writer, HeadDirection);
        WriteHalf2(writer, EyeDirection);
        writer.Write(DirectionalRed);
        writer.Write(DirectionalGreen);
        writer.Write(DirectionalBlue);
        writer.Write(DirectionalBrightness);
        writer.Write(DirectionalVerticalAngle);
        writer.Write(DirectionalHorizontalAngle);
        writer.Write(AmbientRed);
        writer.Write(AmbientGreen);
        writer.Write(AmbientBlue);
        writer.Write(AmbientBrightness);
        writer.Write(BackgroundId);
        writer.Write(FrameId);
        writer.Write(DecorationId);

        writer.Flush();
        return Convert.ToBase64String(stream.ToArray());
    }

    public static bool TryParse(string? shareCode, out PortraitPreset? preset, out string error)
    {
        preset = null;
        error = "";
        if (string.IsNullOrWhiteSpace(shareCode))
        {
            error = "Paste a portrait preset code first.";
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(shareCode.Trim());
        }
        catch (FormatException)
        {
            error = "That is not a valid Base64 preset code.";
            return false;
        }

        if (bytes.Length != EncodedLength)
        {
            error = $"This preset has an unsupported size ({bytes.Length} bytes). Expected a version 1 portrait code.";
            return false;
        }

        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream);
        if (reader.ReadInt32() != Magic)
        {
            error = "This code is not a recognized Portrait Helper preset.";
            return false;
        }

        var version = reader.ReadUInt16();
        if (version != CurrentVersion)
        {
            error = $"Preset version {version} is not supported yet.";
            return false;
        }

        preset = new PortraitPreset
        {
            CameraPosition = ReadHalf4(reader),
            CameraTarget = ReadHalf4(reader),
            ImageRotation = reader.ReadInt16(),
            CameraZoom = reader.ReadByte(),
            BannerTimeline = reader.ReadUInt16(),
            AnimationProgress = reader.ReadSingle(),
            Expression = reader.ReadByte(),
            HeadDirection = ReadHalf2(reader),
            EyeDirection = ReadHalf2(reader),
            DirectionalRed = reader.ReadByte(),
            DirectionalGreen = reader.ReadByte(),
            DirectionalBlue = reader.ReadByte(),
            DirectionalBrightness = reader.ReadByte(),
            DirectionalVerticalAngle = reader.ReadInt16(),
            DirectionalHorizontalAngle = reader.ReadInt16(),
            AmbientRed = reader.ReadByte(),
            AmbientGreen = reader.ReadByte(),
            AmbientBlue = reader.ReadByte(),
            AmbientBrightness = reader.ReadByte(),
            BackgroundId = reader.ReadUInt16(),
            FrameId = reader.ReadUInt16(),
            DecorationId = reader.ReadUInt16(),
        };

        if (!preset.TryValidate(out error))
        {
            preset = null;
            return false;
        }

        return true;
    }

    public bool TryValidate(out string error)
    {
        error = "";
        if (!float.IsFinite(AnimationProgress) || AnimationProgress < 0
            || !IsFinite(CameraPosition) || !IsFinite(CameraTarget)
            || !IsFinite(HeadDirection) || !IsFinite(EyeDirection)
            || ImageRotation is < -90 or > 90 || CameraZoom > 200
            || DirectionalVerticalAngle is < -180 or > 180
            || DirectionalHorizontalAngle is < -180 or > 180)
        {
            error = "The preset contains an invalid camera, animation, rotation, zoom or lighting angle.";
            return false;
        }

        return true;
    }

    public PortraitPreset WithStyle(PortraitStyle style, float strength, int variation)
    {
        strength = Math.Clamp(strength, 0f, 1f);
        var random = new Random(unchecked(style.Seed * 7919 + variation * 104729));

        var directional = Jitter(style.DirectionalColor, random, variation == 0 ? 0 : 10);
        var ambient = Jitter(style.AmbientColor, random, variation == 0 ? 0 : 10);
        var position = ScaleCameraDistance(CameraPosition, CameraTarget, Lerp(1f, style.CameraDistance, strength));
        var target = OffsetCameraTarget(CameraTarget, CameraPosition, style.CameraTargetOffset * strength);

        return this with
        {
            CameraPosition = position,
            CameraTarget = target,
            DirectionalRed = (byte)Blend(DirectionalRed, directional.Red, strength),
            DirectionalGreen = (byte)Blend(DirectionalGreen, directional.Green, strength),
            DirectionalBlue = (byte)Blend(DirectionalBlue, directional.Blue, strength),
            DirectionalBrightness = StyleBrightness(DirectionalBrightness, style.DirectionalBrightness, strength),
            AmbientRed = (byte)Blend(AmbientRed, ambient.Red, strength),
            AmbientGreen = (byte)Blend(AmbientGreen, ambient.Green, strength),
            AmbientBlue = (byte)Blend(AmbientBlue, ambient.Blue, strength),
            AmbientBrightness = StyleBrightness(AmbientBrightness, style.AmbientBrightness, strength),
        };
    }

    private static Half4 ScaleCameraDistance(Half4 position, Half4 target, float scale) => new(
        SafeHalf((float)target.X + ((float)position.X - (float)target.X) * scale),
        SafeHalf((float)target.Y + ((float)position.Y - (float)target.Y) * scale),
        SafeHalf((float)target.Z + ((float)position.Z - (float)target.Z) * scale),
        position.W);

    private static Half4 OffsetCameraTarget(Half4 target, Half4 position, float distanceFraction)
    {
        var dx = (float)target.X - (float)position.X;
        var dy = (float)target.Y - (float)position.Y;
        var dz = (float)target.Z - (float)position.Z;
        var distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        var offset = Math.Clamp(distanceFraction, -0.08f, 0.08f) * distance;
        return target with { Y = SafeHalf((float)target.Y + offset) };
    }

    private static Half SafeHalf(float value) => (Half)Math.Clamp(value, (float)Half.MinValue, (float)Half.MaxValue);

    private static int Blend(byte from, byte to, float amount) =>
        Math.Clamp((int)MathF.Round(from + (to - from) * amount), 0, byte.MaxValue);

    private static byte StyleBrightness(byte value, float multiplier, float strength) =>
        (byte)Blend(value, (byte)Math.Clamp((int)MathF.Round(value * multiplier), 64, byte.MaxValue), strength);

    private static float Lerp(float from, float to, float amount) => from + (to - from) * amount;

    private static Rgb Jitter(Rgb color, Random random, int amount) => amount == 0
        ? color
        : new Rgb(
            (byte)Math.Clamp(color.Red + random.Next(-amount, amount + 1), 0, byte.MaxValue),
            (byte)Math.Clamp(color.Green + random.Next(-amount, amount + 1), 0, byte.MaxValue),
            (byte)Math.Clamp(color.Blue + random.Next(-amount, amount + 1), 0, byte.MaxValue));

    private static void WriteHalf4(BinaryWriter writer, Half4 value)
    {
        WriteHalf(writer, value.X);
        WriteHalf(writer, value.Y);
        WriteHalf(writer, value.Z);
        WriteHalf(writer, value.W);
    }

    private static void WriteHalf2(BinaryWriter writer, Half2 value)
    {
        WriteHalf(writer, value.X);
        WriteHalf(writer, value.Y);
    }

    private static void WriteHalf(BinaryWriter writer, Half value) =>
        writer.Write(BitConverter.HalfToUInt16Bits(value));

    private static Half4 ReadHalf4(BinaryReader reader) => new(
        ReadHalf(reader), ReadHalf(reader), ReadHalf(reader), ReadHalf(reader));

    private static Half2 ReadHalf2(BinaryReader reader) => new(ReadHalf(reader), ReadHalf(reader));

    private static Half ReadHalf(BinaryReader reader) => BitConverter.UInt16BitsToHalf(reader.ReadUInt16());

    private static bool IsFinite(Half2 value) => Half.IsFinite(value.X) && Half.IsFinite(value.Y);

    private static bool IsFinite(Half4 value) => IsFinite(new Half2(value.X, value.Y))
        && Half.IsFinite(value.Z)
        && Half.IsFinite(value.W);
}

public readonly record struct Half2(Half X, Half Y);
public readonly record struct Half4(Half X, Half Y, Half Z, Half W);

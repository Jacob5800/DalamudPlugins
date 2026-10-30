using System;

namespace TataruBrawler;

public sealed class Configuration
{
	public bool AutoLeaveDuty { get; set; }

    public bool IllegalMode { get; set; }
    public bool SkipOptionalPunctuation { get; set; }
    public bool WarnMgcCap { get; set; } = true;
    public int MgcWarningPercent { get; set; } = 90;

	public bool ShowServerBarButton { get; set; } = true;

	public bool RecordDiagnostics { get; set; } = true;

	public int LoopCount { get; set; } = 1;

	public bool AutoStart { get; set; } = true;

	public bool AutoEnableOnMap { get; set; } = true;

	public bool AutoQueueOnLobbyEntry { get; set; } = true;

	public bool BackgroundInput { get; set; } = true;

	public int Version { get; set; } = 1;

	public string? Language { get; set; }

	public int MinimumMs { get; set; } = 80;

	public int MaximumMs { get; set; } = 150;

	public int BetweenWordsMs { get; set; }

	public void SetRange(int min, int max)
	{
		MinimumMs = Math.Clamp(min, IllegalMode ? 0 : 20, 5000);
		MaximumMs = Math.Clamp(max, MinimumMs, 5000);
	}
}

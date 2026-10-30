namespace TataruBrawler;

internal static class StartupDefaults
{
	public static bool Apply(Configuration config)
	{
		bool result = config.IllegalMode || config.Language != "en" || !config.RecordDiagnostics || !config.AutoStart || !config.AutoEnableOnMap;
		config.IllegalMode = false;
        config.SetRange(config.MinimumMs, config.MaximumMs);
		config.Language = "en";
		config.RecordDiagnostics = true;
		config.AutoStart = true;
		config.AutoEnableOnMap = true;
		return result;
	}
}

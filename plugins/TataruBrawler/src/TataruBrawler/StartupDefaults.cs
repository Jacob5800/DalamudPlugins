namespace TataruBrawler;

internal static class StartupDefaults
{
	public static bool Apply(Configuration config)
	{
		bool result = config.Language != "en" || !config.RecordDiagnostics || !config.AutoStart || !config.AutoEnableOnMap;
		config.Language = "en";
		config.RecordDiagnostics = true;
		config.AutoStart = true;
		config.AutoEnableOnMap = true;
		return result;
	}
}

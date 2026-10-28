using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace TataruBrawler;

internal static class ContentActivity
{
	public unsafe static bool Touch()
	{
		InputTimerModule* ptr = InputTimerModule.Instance();
		if (ptr == null || ptr->TerritoryTypeId != 1359)
		{
			return false;
		}
		ptr->ContentInputTimer = 0f;
		ptr->InputTimer = 0f;
		if (ptr->AfkTimer > 0f)
		{
			ptr->AfkTimer = 0f;
		}
		return true;
	}
}

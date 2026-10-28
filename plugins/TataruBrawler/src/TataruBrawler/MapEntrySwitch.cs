namespace TataruBrawler;

public sealed class MapEntrySwitch
{
	private readonly uint targetTerritory;

	private uint previousTerritory;

	public MapEntrySwitch(uint targetTerritory)
	{
		this.targetTerritory = targetTerritory;
	}

	public bool Observe(bool loggedIn, uint territory)
	{
		uint num = (loggedIn ? territory : 0u);
		bool result = num == targetTerritory && previousTerritory != num;
		previousTerritory = num;
		return result;
	}
}

using System.Collections.Generic;
using System.Linq;

namespace TataruBrawler;

public sealed class SeenPrompts
{
	private readonly Dictionary<uint, string> seen = new Dictionary<uint, string>();

	public void Clear()
	{
		seen.Clear();
	}

	public void Update(IReadOnlyList<KtgWord> current, IReadOnlyList<PromptDisplay> display)
	{
		KeyValuePair<uint, string>[] array = seen.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			KeyValuePair<uint, string> pair = array[i];
			if (!current.Any((KtgWord x) => x.Visible && x.EntityId == pair.Key && x.Word == pair.Value))
			{
				seen.Remove(pair.Key);
			}
		}
		foreach (PromptDisplay observation in display)
		{
			if (observation.TextVisible && current.Any((KtgWord x) => x.Visible && x.EntityId == observation.EntityId && x.Word == observation.Word))
			{
				seen[observation.EntityId] = observation.Word;
			}
		}
	}

	public bool Contains(KtgWord word)
	{
		if (seen.TryGetValue(word.EntityId, out string value))
		{
			return value == word.Word;
		}
		return false;
	}
}

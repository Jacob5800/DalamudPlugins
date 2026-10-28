using System.Collections.Generic;
using System.Linq;

namespace TataruBrawler;

internal sealed class GlyphCoverage
{
	private readonly HashSet<char> characters = new HashSet<char>();

	public static string TypingCharacters => new string((from x in Enumerable.Range(32, 95)
		select (char)x).ToArray()) + "ぁあぃいぅうぇえぉおかがきぎくぐけげこごさざしじすずせぜそぞただちぢっつづてでとどなにぬねのはばぱひびぴふぶぷへべぺほぼぽまみむめもゃやゅゆょよらりるれろゎわゐゑをんゔゕゖー～〜";

	public int Count => characters.Count;

	public string Text => new string(characters.Order().ToArray());

	public bool Include(IEnumerable<string> text)
	{
		bool flag = false;
		foreach (string item in text)
		{
			foreach (char c in item)
			{
				if (!char.IsControl(c) && !char.IsSurrogate(c))
				{
					flag |= characters.Add(c);
				}
			}
		}
		return flag;
	}
}

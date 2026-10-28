using System;
using System.Collections.Generic;
using System.Linq;

namespace TataruBrawler;

public sealed class TargetMotion
{
	private readonly Dictionary<uint, (int X, int Y, long Moved)> positions = new Dictionary<uint, (int, int, long)>();

	public void Clear()
	{
		positions.Clear();
	}

	public IReadOnlyList<KtgWord> Update(long now, IReadOnlyList<KtgWord> words)
	{
		uint[] array = positions.Keys.ToArray();
		foreach (uint id in array)
		{
			if (!words.Any((KtgWord x) => x.Visible && x.EntityId == id))
			{
				positions.Remove(id);
			}
		}
		return words.Select((KtgWord word) =>
		{
			if (!word.Visible || word.ScreenY <= 0)
			{
				return word with
				{
					Moving = false
				};
			}
			long num = (positions.TryGetValue(word.EntityId, out (int, int, long) value) ? value.Item3 : long.MinValue);
			if (positions.ContainsKey(word.EntityId) && (Math.Abs(word.ScreenX - value.Item1) >= 2 || Math.Abs(word.ScreenY - value.Item2) >= 2))
			{
				num = now;
			}
			if (!positions.ContainsKey(word.EntityId) || num == now)
			{
				positions[word.EntityId] = (word.ScreenX, word.ScreenY, num);
			}
			return word with
			{
				Moving = (num != long.MinValue && now - num <= 2000)
			};
		}).ToArray();
	}
}

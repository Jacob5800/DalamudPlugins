using System;
using System.Collections.Generic;
using System.Linq;

namespace TataruBrawler;

public sealed class EncounterPolicy
{
	private readonly Dictionary<uint, int> jammerProgress = new Dictionary<uint, int>();

	public long PauseUntil { get; private set; }

	public bool Update(long now, IReadOnlyList<KtgWord> enemies)
	{
		bool result = false;
		KeyValuePair<uint, int>[] array = jammerProgress.ToArray();
		for (int i = 0; i < array.Length; i++)
		{
			KeyValuePair<uint, int> prior = array[i];
			KtgWord ktgWord = enemies.FirstOrDefault((KtgWord x) => x.EntityId == prior.Key);
			if (!(ktgWord?.Cast == "動体感知ジャマー"))
			{
				if (ktgWord != null && prior.Value >= 90)
				{
					PauseUntil = Math.Max(PauseUntil, now + 4000);
					result = true;
				}
				jammerProgress.Remove(prior.Key);
			}
		}
		foreach (KtgWord enemy in enemies)
		{
			if (enemy.Cast == "動体感知ジャマー")
			{
				jammerProgress[enemy.EntityId] = enemy.Progress;
			}
		}
		return result;
	}

	public bool Paused(long now)
	{
		return now < PauseUntil;
	}

	public void Reset()
	{
		jammerProgress.Clear();
		PauseUntil = 0L;
	}

	public static IEnumerable<KtgWord> Prioritize(IEnumerable<KtgWord> enemies)
	{
		KtgWord[] source = enemies.ToArray();
		KtgWord closest = (from x in source
			where x.Visible && x.Kind != 2 && x.Cast.Length == 0 && x.Moving && x.ScreenY > 0
			orderby x.ScreenY descending, x.Slot
			select x).FirstOrDefault();
		return from x in source
			orderby x.Kind == 2 descending, x.Cast.Length > 0 descending, (x.Cast.Length > 0) ? x.Progress : 0 descending, x == closest descending, x.Slot
			select x;
	}
}

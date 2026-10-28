using System;
using System.Collections.Generic;
using System.Linq;

namespace TataruBrawler;

public sealed class EncounterPolicy
{
    public long PauseUntil { get; private set; }

    public bool Update(long now, IEnumerable<BlockingCast> casts)
    {
        bool wasPaused = Paused(now);
        foreach (var cast in casts)
        {
            if (cast.ActionId is not (49529 or 49532 or 49702) ||
                !float.IsFinite(cast.RemainingMs) || cast.RemainingMs < 0 || cast.RemainingMs > 500)
                continue;
            // Keep the custom build's conservative four-second post-cast hold.
            // Also stop during the final half second so an Enter cannot cross it.
            PauseUntil = Math.Max(PauseUntil, now + (long)Math.Ceiling(cast.RemainingMs) + 4000);
        }
        return !wasPaused && Paused(now);
    }

    public bool Paused(long now) => now < PauseUntil;
    public void Reset() => PauseUntil = 0;

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

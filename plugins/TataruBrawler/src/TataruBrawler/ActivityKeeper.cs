using System;

namespace TataruBrawler;

public sealed class ActivityKeeper
{
	private long next;

	private int failures;

	public bool Failed => failures >= 3;

	public void Reset()
	{
		next = 0L;
		failures = 0;
	}

	public void Tick(long now, bool active, Func<bool> touch)
	{
		if (!active)
		{
			next = 0L;
		}
		else if (!Failed && now >= next)
		{
			next = now + 10000;
			if (touch())
			{
				failures = 0;
			}
			else
			{
				failures++;
			}
		}
	}
}

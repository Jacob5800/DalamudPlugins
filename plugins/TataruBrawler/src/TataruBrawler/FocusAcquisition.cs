using System;

namespace TataruBrawler;

public sealed class FocusAcquisition
{
	private long observeAfter;

	private long retryAfter;

	public int Attempts { get; private set; }

	public void Reset()
	{
		Attempts = 0;
		observeAfter = (retryAfter = 0L);
	}

	public FocusResult Step(long now, bool focused, Action request)
	{
		if (focused)
		{
			Reset();
			return FocusResult.Ready;
		}
		if (now < observeAfter)
		{
			return FocusResult.Waiting;
		}
		if (Attempts >= 3)
		{
			if (retryAfter == 0L)
			{
				retryAfter = now + 2000;
			}
			if (now < retryAfter)
			{
				return FocusResult.Exhausted;
			}
			Reset();
		}
		Attempts++;
		observeAfter = now + 250;
		request();
		return FocusResult.Requested;
	}
}

using System;

namespace TataruBrawler;

public sealed class RoundLoop
{
	private long since;

	private long blockedAt = -1L;

	private bool accepted;

	private bool left;

	private bool confirmed;

	public LoopPhase Phase { get; private set; }

	public bool Active
	{
		get
		{
			LoopPhase phase = Phase;
			if ((uint)(phase - 1) <= 4u)
			{
				return true;
			}
			return false;
		}
	}

	public bool Paused { get; private set; }

	public bool Unlimited { get; private set; }

	public int Total { get; private set; }

	public int Completed { get; private set; }

	public int Current
	{
		get
		{
			if (Unlimited)
			{
				return (Phase == LoopPhase.Leaving) ? Completed : (Completed + 1);
			}
			int result;
			if (Total == 0)
			{
				result = 0;
			}
			else
			{
				int total = Total;
				LoopPhase phase = Phase;
				bool flag = (uint)(phase - 5) <= 1u;
				result = Math.Min(total, flag ? Completed : (Completed + 1));
			}
			return result;
		}
	}

	public void Start(long now, int count, bool inGame, bool unlimited = false)
	{
		Unlimited = unlimited;
		Total = ((!unlimited) ? Math.Clamp(count, 1, 9999) : 0);
		Completed = 0;
		Paused = false;
		blockedAt = -1L;
		accepted = (left = (confirmed = false));
		Set((!inGame) ? LoopPhase.Requesting : LoopPhase.Playing, now);
	}

	public void Stop(bool error = false)
	{
		Phase = (error ? LoopPhase.Error : LoopPhase.Stopped);
		Paused = false;
		blockedAt = -1L;
	}

	public void Pause()
	{
		if (Active)
		{
			Paused = true;
		}
	}

	public void Resume()
	{
		Paused = false;
	}

	private void Set(LoopPhase phase, long now)
	{
		Phase = phase;
		since = now;
	}

	public LoopAction Tick(long now, LoopSnapshot state, bool blocked = false)
	{
		if (!Active)
		{
			return LoopAction.None;
		}
		if (!state.LoggedIn || state.OtherQueue)
		{
			Stop(error: true);
			return LoopAction.None;
		}
		if ((Paused | blocked) || state.Loading)
		{
			if (blockedAt < 0)
			{
				blockedAt = now;
			}
			return LoopAction.None;
		}
		if (blockedAt >= 0)
		{
			since += now - blockedAt;
			blockedAt = -1L;
		}
		switch (Phase)
		{
		case LoopPhase.Requesting:
			if (!state.Lobby || state.InGame || state.OtherQueue)
			{
				Stop(error: true);
				break;
			}
			if (state.Queued)
			{
				Stop(error: true);
				break;
			}
			if (state.RegistrationReady)
			{
				accepted = (left = (confirmed = false));
				Set(LoopPhase.Waiting, now);
				return LoopAction.Register;
			}
			if (!state.RegistrarInRange)
			{
				if (now - since > 120000)
				{
					Stop(error: true);
				}
				break;
			}
			accepted = (left = (confirmed = false));
			Set(LoopPhase.OpeningRegistrar, now);
			return LoopAction.InteractRegistrar;
		case LoopPhase.OpeningRegistrar:
			if (!state.Lobby || state.InGame)
			{
				Stop(error: true);
				break;
			}
			if (state.Queued)
			{
				Set(LoopPhase.Waiting, now);
				break;
			}
			if (state.RegistrationReady)
			{
				Set(LoopPhase.Waiting, now);
				return LoopAction.Register;
			}
			if (now - since > 10000)
			{
				Stop(error: true);
			}
			break;
		case LoopPhase.Waiting:
			if (state.InGame)
			{
				Set(LoopPhase.Playing, now);
				return LoopAction.StartTyping;
			}
			if (state.EntryReady && state.Queued && !accepted)
			{
				accepted = true;
				return LoopAction.Accept;
			}
			if ((!state.Queued && !accepted && now - since > 15000) || now - since > 1800000)
			{
				Stop(error: true);
			}
			break;
		case LoopPhase.Playing:
			if (state.Result)
			{
				Completed++;
				Set(LoopPhase.Leaving, now);
				if (state.LeaveReady)
				{
					left = true;
					return LoopAction.Leave;
				}
			}
			else if (!state.InGame)
			{
				Stop(error: true);
			}
			break;
		case LoopPhase.Leaving:
			if (!state.InGame && state.Lobby && !state.Queued)
			{
				Set((Unlimited || Completed < Total) ? LoopPhase.Requesting : LoopPhase.Complete, now);
				break;
			}
			if (state.ExitConfirm && left && !confirmed)
			{
				confirmed = true;
				return LoopAction.ConfirmExit;
			}
			if (state.Result && state.LeaveReady && !left)
			{
				left = true;
				return LoopAction.Leave;
			}
			if (now - since > 120000)
			{
				Stop(error: true);
			}
			break;
		}
		return LoopAction.None;
	}
}

using System;

namespace TataruBrawler;

public sealed class TypingSession
{
	public const char HiraganaKey = '\u0006';

	private readonly Func<int, int, int> random;

	private long due;

	private long pausedAt = -1L;

	private long commitAt;

	private long nextWordAt;

	private TypingPlan? plan;

	private int index;

	private int clearKeys;

	private bool submitted;

	private bool committing;

	private bool hiraganaRequested;

	private bool clearing;

	public string AnswerCheck { get; private set; } = "None";

	public bool Running { get; private set; }

	public bool ManualPaused { get; private set; }

	public string Status { get; private set; } = "Stopped";

	public string CurrentWord => plan?.Original ?? "";
    public string ExpectedAnswer => plan?.Expected ?? "";
    public bool SkipOptionalPunctuation { get; set; }

	public bool Japanese => plan?.Japanese ?? false;

	public bool NeedsPreparation
	{
		get
		{
			if (plan != null && index == 0)
			{
				return !submitted;
			}
			return false;
		}
	}

	public bool Submitted => submitted;

	public bool Recovering => clearing;

	public int Position => index;

	public long Sent { get; private set; }

	public void RecoverIme(long now)
	{
		if (Running)
		{
			clearing = true;
			submitted = false;
			index = Math.Max(1, index);
			due = now;
			Status = "Clearing";
		}
	}

	public TypingSession(Func<int, int, int>? sample = null)
	{
		random = sample ?? new Func<int, int, int>(Random.Shared.Next);
	}

	public void Start(long now)
	{
		Stop();
		Running = true;
		Status = "Waiting";
		due = now;
		Sent = 0L;
	}

	public void Stop(string reason = "Stopped")
	{
		Running = false;
		ManualPaused = false;
		Status = reason;
		plan = null;
		index = (clearKeys = 0);
		submitted = (committing = (hiraganaRequested = (clearing = false)));
		pausedAt = -1L;
		nextWordAt = 0L;
	}

	public void PauseByUser()
	{
		if (Running)
		{
			ManualPaused = true;
			Status = "UserPaused";
		}
	}

	public void ResumeByUser()
	{
		if (Running)
		{
			ManualPaused = false;
			Status = "Waiting";
		}
	}

	public void Tick(long now, bool ready, string? candidate, bool currentStillVisible, int min, int max, Func<char, bool> send, bool paused = false, bool composing = false, string? evaluated = null, int betweenWordsMs = 0)
	{
		if (!Running)
		{
			return;
		}
		if (paused || !ready || ManualPaused)
		{
			if (pausedAt < 0)
			{
				pausedAt = now;
			}
			Status = (ManualPaused ? "UserPaused" : "Paused");
			return;
		}
		if (pausedAt >= 0)
		{
			long num = now - pausedAt;
			due += num;
			commitAt += num;
			pausedAt = -1L;
		}
		if (now < due)
		{
			return;
		}
		if (clearing || (plan != null && !currentStillVisible))
		{
			if (index > 0 && !submitted && (composing || evaluated == null || evaluated.Length > 0))
			{
				clearing = true;
				if (evaluated == null && !composing)
				{
					Status = "Clearing";
					return;
				}
				if (clearKeys >= 384)
				{
					Stop("NoProgress");
					return;
				}
				if (!send('\b'))
				{
					Stop("SendFailed");
					return;
				}
				clearKeys++;
				due = now + max;
				Status = "Clearing";
			}
			else
			{
				plan = null;
				index = (clearKeys = 0);
				submitted = (committing = (hiraganaRequested = (clearing = false)));
				due = Math.Max(now, nextWordAt);
				Status = ((now < nextWordAt) ? "BetweenWords" : "Waiting");
			}
		}
		else if (plan != null && index >= plan.Keys.Length)
		{
			if (!submitted)
			{
				if (plan.Japanese & composing)
				{
					if (committing)
					{
						if (now - commitAt > 2000)
						{
							RecoverIme(now);
						}
					}
					else if (!hiraganaRequested && plan.Expected.Contains('ゔ'))
					{
						if (!send('\u0006'))
						{
							Stop("SendFailed");
							return;
						}
						hiraganaRequested = true;
						due = now + Math.Max(max, 150);
						Status = "Confirming";
					}
					else if (!send('\r'))
					{
						Stop("SendFailed");
					}
					else
					{
						committing = true;
						commitAt = now;
						due = now + Math.Max(max, 150);
						Status = "Confirming";
					}
					return;
				}
				if (plan.Japanese)
				{
					TypingSession typingSession = this;
					string answerCheck;
					if (evaluated == null)
					{
						answerCheck = "Unavailable";
					}
					else if (TypingPlan.NormalizeAnswer(evaluated) != TypingPlan.NormalizeAnswer(plan.Expected))
					{
						answerCheck = "DifferentText";
					}
					else
					{
						answerCheck = ((evaluated == plan.Expected) ? "Exact" : "Equivalent");
					}
					typingSession.AnswerCheck = answerCheck;
					string answerCheck2 = AnswerCheck;
					if ((answerCheck2 == "Unavailable" || answerCheck2 == "DifferentText") ? true : false)
					{
						RecoverIme(now);
						return;
					}
				}
				if (!send('\r'))
				{
					Stop("SendFailed");
					return;
				}
				submitted = true;
				nextWordAt = now + Math.Clamp(betweenWordsMs, 0, 60000);
				due = now + max;
				Status = "Waiting";
			}
			else
			{
				Status = "Waiting";
			}
		}
		else if (plan == null)
		{
			if (string.IsNullOrEmpty(candidate))
			{
				Status = "Waiting";
				due = now + 100;
				return;
			}
			plan = TypingPlan.Create(candidate, SkipOptionalPunctuation);
			if (plan == null)
			{
				Stop("Unsupported");
				return;
			}
			index = 0;
			submitted = (committing = (hiraganaRequested = (clearing = false)));
			AnswerCheck = "None";
			due = now + random(min, max + 1);
			Status = "Typing";
		}
		else if (!send(plan.Keys[index]))
		{
			Stop("SendFailed");
		}
		else
		{
			index++;
			Sent++;
			Status = "Typing";
			due = now + random(min, max + 1);
		}
	}
}

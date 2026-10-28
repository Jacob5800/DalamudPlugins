namespace TataruBrawler;

public sealed class AutomationLifecycle
{
	public bool Consumed { get; private set; }

	public void Start(long now)
	{
		Consumed = true;
	}

	public void Suppress()
	{
		Consumed = true;
	}

	public void Reset()
	{
		Consumed = false;
	}

	public bool TryAutoStart(bool inContent, bool enabled, bool playable, bool eligible)
	{
		if (!inContent)
		{
			Reset();
			return false;
		}
		if (Consumed || !enabled || !playable || !eligible)
		{
			return false;
		}
		Consumed = true;
		return true;
	}

	public RunGate Evaluate(long now, bool inContent, bool escape, bool foreground, bool modifier, bool inputReady, bool uiCapture, bool otherTextInput)
	{
		if (!inContent)
		{
			return RunGate.OutsideContent;
		}
		if (escape)
		{
			return RunGate.Escape;
		}
		if (!foreground)
		{
			return RunGate.ForegroundLost;
		}
		if (modifier)
		{
			return RunGate.Modifier;
		}
		if (!inputReady)
		{
			return RunGate.InputUnavailable;
		}
		if (uiCapture)
		{
			return RunGate.UiCapture;
		}
		if (otherTextInput)
		{
			return RunGate.OtherTextInput;
		}
		return RunGate.Run;
	}
}

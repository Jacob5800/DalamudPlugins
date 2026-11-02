namespace KeyboundAutomation;

public enum LoopPhase
{
	Idle,
	Requesting,
	OpeningRegistrar,
	Waiting,
	Playing,
	Leaving,
	Complete,
	Stopped,
	Error
}

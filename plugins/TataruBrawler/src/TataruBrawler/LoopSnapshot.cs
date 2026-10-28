namespace TataruBrawler;

public readonly record struct LoopSnapshot(bool LoggedIn, bool Lobby, bool InGame, bool Loading, bool OtherQueue, bool Queued, bool EntryReady, bool Result, bool LeaveReady, bool ExitConfirm, bool RegistrarInRange = false, bool RegistrationReady = false);

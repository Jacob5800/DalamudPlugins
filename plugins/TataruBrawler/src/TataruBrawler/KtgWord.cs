namespace TataruBrawler;

public sealed record KtgWord(uint EntityId, string Word, int Slot, string Cast, int Progress, int Special, int Kind = 0, bool Visible = true, int ScreenX = 0, int ScreenY = 0, bool Moving = false);

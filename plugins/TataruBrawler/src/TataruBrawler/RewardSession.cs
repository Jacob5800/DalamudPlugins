using System;

namespace TataruBrawler;

public sealed class RewardTotals
{
    public long Games { get; set; }
    public long Mgp { get; set; }
    public long Mgc { get; set; }
}

// Keeps a single result screen from counting repeatedly and permits delayed
// inventory updates for 15 seconds, including the immediate return to the lobby.
public sealed class RewardSession
{
    private bool wasInGame, tracking, counted;
    private long? lastMgp, lastMgc;
    private long rewardDeadline;
    public RewardTotals Session { get; } = new();
    public RewardTotals Total { get; }
    public RewardSession(RewardTotals total) => Total = total;

    public bool Observe(long now, bool inGame, bool resultVisible, long? mgp, long? mgc)
    {
        if (inGame && !wasInGame)
        {
            tracking = true;
            counted = false;
            rewardDeadline = 0;
            lastMgp = mgp;
            lastMgc = mgc;
        }
        wasInGame = inGame;
        if (!tracking) return false;
        bool changed = false;
        if (inGame && resultVisible && !counted)
        {
            counted = true;
            rewardDeadline = now + 15000;
            Session.Games++;
            Total.Games++;
            changed = true;
        }
        if (!counted)
        {
            // An aborted visit must not produce a completed game or rewards.
            if (!inGame) EndVisit();
            else { lastMgp ??= mgp; lastMgc ??= mgc; }
            return false;
        }
        if (now > rewardDeadline) { tracking = false; return changed; }
        changed |= AddCurrency(mgp, ref lastMgp, true);
        changed |= AddCurrency(mgc, ref lastMgc, false);
        return changed;
    }

    private bool AddCurrency(long? value, ref long? previous, bool isMgp)
    {
        if (value == null) return false;
        long gain = previous.HasValue ? Math.Max(0, value.Value - previous.Value) : 0;
        previous = value;
        if (isMgp) { Session.Mgp += gain; Total.Mgp += gain; }
        else { Session.Mgc += gain; Total.Mgc += gain; }
        return gain > 0;
    }

    public void EndVisit()
    {
        wasInGame = tracking = counted = false;
        lastMgp = lastMgc = null;
        rewardDeadline = 0;
    }
}

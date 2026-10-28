using System;
namespace TataruBrawler;

public sealed class MgcWarning
{
    private int level;
    public string? Message { get; private set; }
    public bool Update(long? balance, long? cap, bool enabled, int percent)
    {
        if (enabled && (balance == null || cap == null || cap <= 0))
        { Message = null; return false; }
        int next = !enabled || balance == null || cap == null || cap <= 0 ? 0 :
            balance >= cap ? 2 : balance.Value >= Math.Ceiling(cap.Value * (Math.Clamp(percent, 1, 100) / 100.0)) ? 1 : 0;
        Message = next == 2 ? $"MGC cap reached ({balance:N0}/{cap:N0}). Further MGC rewards may be lost." :
            next == 1 ? $"MGC near cap ({balance:N0}/{cap:N0}). Spend MGC before more runs." : null;
        bool notify = next > level;
        level = next;
        return notify;
    }
}

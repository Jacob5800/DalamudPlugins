using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace TataruBrawler;

internal static class ContentActivity
{
    public static unsafe bool Touch()
    {
        var timers = InputTimerModule.Instance();
        if (timers == null || timers->TerritoryTypeId != 1359)
            return false;

        // Keybound has a separate input-agent inactivity counter in addition to
        // the general client timers. Layout corroborated with Saucy (see NOTICE).
        var agents = AgentModule.Instance();
        if (agents == null)
            return false;
        var hud = agents->GetAgentByInternalId((AgentId)397);
        var input = agents->GetAgentByInternalId((AgentId)398);
        if (hud == null || input == null || *(nint*)((byte*)input + 0x28) != (nint)hud)
            return false;

        *(ulong*)((byte*)input + 0x50) = 0;
        timers->ContentInputTimer = 0f;
        timers->InputTimer = 0f;
        if (timers->AfkTimer > 0f)
            timers->AfkTimer = 0f;
        return true;
    }
}

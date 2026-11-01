using System;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace TataruBrawler;

internal sealed class CofferOpener
{
    private const uint ItemId = 52450;
    private static readonly InventoryType[] Bags =
        [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4];
    private int? requestedCount;
    private long requestDeadline, nextUse;
    public bool Running { get; private set; }
    public int Opened { get; private set; }
    public string Status { get; private set; } = "Opens Keybound Coffers in carried inventory.";

    public void Start()
    {
        Running = true;
        Opened = 0;
        requestedCount = null;
        nextUse = 0;
        Status = "Checking carried coffers…";
    }

    public void Stop(string message = "Stopped. The current opening, if any, will finish.")
    {
        Running = false;
        requestedCount = null;
        Status = message;
    }

    public unsafe void Tick(long now, bool available, bool casting, bool competingAutomation)
    {
        if (!Running) return;
        if (!available || competingAutomation)
        {
            Stop("Stopped: finish loading, leave combat/duty and stop the queue before opening coffers.");
            return;
        }
        var inventory = InventoryManager.Instance();
        if (inventory == null) { Stop("Inventory unavailable. Try again once loaded."); return; }
        int count = 0, free = 0;
        foreach (var type in Bags)
        {
            var bag = inventory->GetInventoryContainer(type);
            if (bag == null || !bag->IsLoaded) { Stop("Carried inventory is loading."); return; }
            for (int i = 0; i < bag->Size; i++)
            {
                var slot = bag->GetInventorySlot(i);
                if (slot == null) continue;
                if (slot->IsEmpty()) free++;
                else if (slot->GetBaseItemId() == ItemId) count += (int)slot->Quantity;
            }
        }
        if (requestedCount is int before)
        {
            if (count < before)
            {
                Opened += before - count;
                requestedCount = null;
                nextUse = Math.Max(nextUse, now + 300);
            }
            else
            {
                if (now >= requestDeadline) Stop("Opening was not confirmed. Check the game message, then press Start to retry.");
                return;
            }
        }
        if (count == 0) { Stop($"Finished: opened {Opened} Keybound Coffers."); return; }
        if (casting || now < nextUse) { Status = $"Opening coffers… {Opened} opened, {count} remaining."; return; }
        if (free == 0) { Stop("Stopped: make space in your carried inventory."); return; }
        var actions = ActionManager.Instance();
        if (actions == null || !actions->UseAction(ActionType.Item, ItemId, 0xE0000000, 65535))
        {
            Stop("Coffer could not be used. Check the game message and press Start to retry.");
            return;
        }
        requestedCount = count;
        requestDeadline = now + 10000;
        nextUse = now + 2250;
        Status = $"Opening coffers… {Opened} opened, {count} remaining.";
    }
}
